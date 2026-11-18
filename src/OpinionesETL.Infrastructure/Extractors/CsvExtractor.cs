using System.Diagnostics;
using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Domain.Models;
using OpinionesETL.Infrastructure.Common;

namespace OpinionesETL.Infrastructure.Extractors;

public sealed class CsvExtractor : IExtractor
{
    private readonly CsvSourceOptions _options;
    private readonly ILogger<CsvExtractor> _logger;

    public CsvExtractor(IOptions<CsvSourceOptions> options, ILogger<CsvExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string SourceName => "CSV";

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[CSV] Fuente deshabilitada en la configuración.");
            return Array.Empty<ExtractionResult>();
        }

        var folder = PathResolver.Resolve(_options.Folder);
        _logger.LogInformation("[CSV] Leyendo {Count} archivos desde {Folder}", _options.Files.Count, folder);

        var tasks = _options.Files.Select(file => ReadFileAsync(folder, file, cancellationToken));
        var results = await Task.WhenAll(tasks);

        return results.Where(r => r is not null).Select(r => r!).ToList();
    }

    private async Task<ExtractionResult?> ReadFileAsync(string folder, CsvFileOptions file, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, file.FileName);
        var stopwatch = Stopwatch.StartNew();

        if (!File.Exists(path))
        {
            _logger.LogWarning("[CSV] No se encontró el archivo {FileName} en {Path}", file.FileName, path);
            return null;
        }

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = _options.Delimiter,
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = args => _logger.LogWarning("[CSV] Dato mal formado en {FileName}, fila {Row}", file.FileName, args.Context.Parser?.Row)
            };

            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            using var csv = new CsvReader(reader, config);

            var records = new List<IReadOnlyDictionary<string, string?>>();

            if (!await csv.ReadAsync())
            {
                _logger.LogWarning("[CSV] El archivo {FileName} está vacío", file.FileName);
                return new ExtractionResult($"csv:{file.Name}", records, DateTime.UtcNow, stopwatch.Elapsed);
            }

            csv.ReadHeader();
            var headers = csv.HeaderRecord ?? Array.Empty<string>();

            while (await csv.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = new Dictionary<string, string?>(headers.Length);
                for (var i = 0; i < headers.Length; i++)
                {
                    row[headers[i]] = csv.GetField(i);
                }
                records.Add(row);
            }

            stopwatch.Stop();
            _logger.LogInformation("[CSV] {Name}: {Count} registros en {Elapsed} ms", file.Name, records.Count, stopwatch.ElapsedMilliseconds);

            return new ExtractionResult($"csv:{file.Name}", records, DateTime.UtcNow, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CSV] Error leyendo el archivo {FileName}", file.FileName);
            return null;
        }
    }
}