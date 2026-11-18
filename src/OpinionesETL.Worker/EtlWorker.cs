using System.Diagnostics;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Infrastructure.Staging;

namespace OpinionesETL.Worker;

public sealed class EtlWorker : BackgroundService
{
    private readonly ILogger<EtlWorker> _logger;
    private readonly IEnumerable<IExtractor> _extractors;
    private readonly StagingWriter _stagingWriter;
    private readonly IHostApplicationLifetime _lifetime;

    public EtlWorker(
        ILogger<EtlWorker> logger,
        IEnumerable<IExtractor> extractors,
        StagingWriter stagingWriter,
        IHostApplicationLifetime lifetime)
    {
        _logger = logger;
        _extractors = extractors;
        _stagingWriter = stagingWriter;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var totalStopwatch = Stopwatch.StartNew();
        _logger.LogInformation("=== INICIANDO PROCESO DE EXTRACCIÓN ETL ===");

        try
        {
            // 1. Ejecutamos TODOS los extractores en paralelo para maximizar el rendimiento
            var extractionTasks = _extractors.Select(async extractor =>
            {
                _logger.LogInformation("Iniciando extractor: {Source}", extractor.SourceName);
                var results = await extractor.ExtractAsync(stoppingToken);

                // 2. Guardamos los resultados en Staging en cuanto cada extractor termine
                foreach (var result in results)
                {
                    await _stagingWriter.WriteAsync(result, stoppingToken);
                }

                return results;
            });

            // Esperamos a que todas las fuentes terminen
            var allResults = await Task.WhenAll(extractionTasks);

            totalStopwatch.Stop();

            // Resumen final
            var totalRecords = allResults.SelectMany(r => r).Sum(r => r.Count);
            _logger.LogInformation("=== EXTRACCIÓN COMPLETADA ===");
            _logger.LogInformation("Total de conjuntos de datos extraídos: {Sets}", allResults.SelectMany(r => r).Count());
            _logger.LogInformation("Total de registros extraídos: {TotalRecords}", totalRecords);
            _logger.LogInformation("Tiempo total del proceso: {Elapsed} ms", totalStopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crítico durante el proceso de extracción");
        }
        finally
        {
            // Detiene la aplicación (es un proceso batch, no un servicio en escucha)
            _lifetime.StopApplication();
        }
    }
}