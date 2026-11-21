using OpinionesETL.Domain.Models;

namespace OpinionesETL.Application.Interfaces;
public interface IExtractor
{
    string SourceName { get; }
    Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken);
}