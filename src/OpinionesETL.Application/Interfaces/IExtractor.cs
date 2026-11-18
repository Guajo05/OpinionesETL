using OpinionesETL.Domain.Models;

namespace OpinionesETL.Application.Interfaces;

/// <summary>
/// Contrato común para todas las fuentes de datos (CSV, base de datos, API).
/// </summary>
public interface IExtractor
{
    string SourceName { get; }
    Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken);
}