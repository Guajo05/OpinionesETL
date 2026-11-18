namespace OpinionesETL.Domain.Models;

/// <summary>
/// Resultado crudo de una extracción: cada registro es un diccionario columna -> valor.
/// </summary>
public sealed record ExtractionResult(
    string SourceName,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Records,
    DateTime ExtractedAtUtc,
    TimeSpan Duration)
{
    public int Count => Records.Count;
}