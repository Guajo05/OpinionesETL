using System.Data;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Domain.Models;

namespace OpinionesETL.Infrastructure.Extractors;

/// <summary>
/// Extrae las reseñas web desde la base de datos relacional configurada.
/// El query y la cadena de conexión vienen de la configuración (User Secrets en desarrollo).
/// </summary>
public sealed class DatabaseExtractor : IExtractor
{
    private readonly DatabaseSourceOptions _options;
    private readonly ILogger<DatabaseExtractor> _logger;

    public DatabaseExtractor(IOptions<DatabaseSourceOptions> options, ILogger<DatabaseExtractor> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public string SourceName => "Database";

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("[DB] Fuente deshabilitada en la configuración.");
            return Array.Empty<ExtractionResult>();
        }

        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            _logger.LogError("[DB] No hay cadena de conexión configurada. Revisa User Secrets: Sources:Database:ConnectionString");
            return Array.Empty<ExtractionResult>();
        }

        if (string.IsNullOrWhiteSpace(_options.Query))
        {
            _logger.LogError("[DB] No hay query configurado en Sources:Database:Query");
            return Array.Empty<ExtractionResult>();
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = new SqlConnection(_options.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            _logger.LogInformation("[DB] Conectado a la base {Database} en {Server}", connection.Database, connection.DataSource);

            await using var command = new SqlCommand(_options.Query, connection)
            {
                CommandType = CommandType.Text,
                CommandTimeout = _options.CommandTimeoutSeconds
            };

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            var records = new List<IReadOnlyDictionary<string, string?>>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, string?>(columns.Length);

                for (var i = 0; i < columns.Length; i++)
                {
                    row[columns[i]] = reader.IsDBNull(i) ? null : FormatValue(reader.GetValue(i));
                }

                records.Add(row);
            }

            stopwatch.Stop();
            _logger.LogInformation("[DB] web_reviews: {Count} registros en {Elapsed} ms", records.Count, stopwatch.ElapsedMilliseconds);

            var result = new ExtractionResult("db:web_reviews", records, DateTime.UtcNow, stopwatch.Elapsed);
            return new[] { result };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException ex)
        {
            // Nunca se imprime la cadena de conexión en el log por seguridad.
            _logger.LogError(ex, "[DB] Error de SQL Server al extraer (número {Number})", ex.Number);
            return Array.Empty<ExtractionResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[DB] Error inesperado al extraer desde la base de datos");
            return Array.Empty<ExtractionResult>();
        }
    }

    // Convierte fechas y otros tipos a string para el staging crudo
    private static string FormatValue(object value) => value switch
    {
        DateTime dt => dt.TimeOfDay == TimeSpan.Zero
            ? dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateTimeOffset dto => dto.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty
    };
}