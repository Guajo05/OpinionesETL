using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Domain.Models;
using OpinionesETL.Infrastructure.Common;

namespace OpinionesETL.Infrastructure.Staging;

public sealed class StagingWriter
{
    private readonly StagingOptions _options;
    private readonly ILogger<StagingWriter> _logger;

    public StagingWriter(IOptions<StagingOptions> options, ILogger<StagingWriter> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task WriteAsync(ExtractionResult result, CancellationToken cancellationToken)
    {
        var folder = PathResolver.Resolve(_options.OutputFolder);

        // Crear la carpeta si no existe
        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // Nombre del archivo: reemplazamos ':' por '_' para que sea válido en Windows (ej. csv_clients.json)
        var safeSourceName = result.SourceName.Replace(":", "_");
        var filePath = Path.Combine(folder, $"{safeSourceName}.json");

        var options = new JsonSerializerOptions { WriteIndented = true };

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, result, options, cancellationToken);

        _logger.LogInformation("[Staging] Guardado {Source} con {Count} registros en {Path}",
            result.SourceName, result.Count, filePath);
    }
}