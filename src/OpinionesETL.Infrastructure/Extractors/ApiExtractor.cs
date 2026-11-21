using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Domain.Models;

namespace OpinionesETL.Infrastructure.Extractors;
public sealed class ApiExtractor : IExtractor
{
    public const string HttpClientName = "SocialApi";
    private const int MaxRetries = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ApiSourceOptions _options;
    private readonly ILogger<ApiExtractor> _logger;

    public ApiExtractor(IHttpClientFactory httpClientFactory, IOptions<ApiSourceOptions> options, ILogger<ApiExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public string SourceName => "API";

    public async Task<IReadOnlyList<ExtractionResult>> ExtractAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return Array.Empty<ExtractionResult>();

        var stopwatch = Stopwatch.StartNew();
        var client = _httpClientFactory.CreateClient(HttpClientName);

        _logger.LogInformation("[API] Consultando {Url}{Endpoint}", _options.BaseUrl, _options.Endpoint);

        var firstPage = await FetchPageAsync(client, 1, cancellationToken);
        if (firstPage is null) return Array.Empty<ExtractionResult>();

        _logger.LogInformation("[API] {Total} comentarios en {Pages} páginas", firstPage.TotalCount, firstPage.TotalPages);

        var records = new List<IReadOnlyDictionary<string, string?>>(firstPage.TotalCount);
        records.AddRange(firstPage.Items);

        if (firstPage.TotalPages > 1)
        {
            // Limita cuántas peticiones HTTP se hacen al mismo tiempo para no saturar la API
            using var gate = new SemaphoreSlim(Math.Max(_options.MaxParallelRequests, 1));

            var tasks = Enumerable.Range(2, firstPage.TotalPages - 1).Select(async pageNumber =>
            {
                await gate.WaitAsync(cancellationToken);
                try { return await FetchPageAsync(client, pageNumber, cancellationToken); }
                finally { gate.Release(); }
            });

            var pages = await Task.WhenAll(tasks);

            foreach (var page in pages.Where(p => p is not null))
            {
                records.AddRange(page!.Items);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation("[API] social_comments: {Count} registros en {Elapsed} ms", records.Count, stopwatch.ElapsedMilliseconds);

        return new[] { new ExtractionResult("api:social_comments", records, DateTime.UtcNow, stopwatch.Elapsed) };
    }

    private async Task<PageData?> FetchPageAsync(HttpClient client, int page, CancellationToken cancellationToken)
    {
        var url = $"{_options.Endpoint}?page={page}&pageSize={_options.PageSize}";

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(url, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("[API] Acceso denegado (401). Revisa la API Key.");
                    return null;
                }

                response.EnsureSuccessStatusCode();
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

                return ParsePage(document.RootElement);
            }
            catch (Exception) when (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // 2s, 4s...
                _logger.LogWarning("[API] Intento {Attempt} falló en página {Page}. Reintentando en {Delay}s", attempt, page, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API] La página {Page} falló tras {Max} intentos", page, MaxRetries);
                return null;
            }
        }
        return null;
    }

    private static PageData ParsePage(JsonElement root)
    {
        var totalCount = root.GetProperty("TotalCount").GetInt32();
        var totalPages = root.GetProperty("TotalPages").GetInt32();
        var items = new List<IReadOnlyDictionary<string, string?>>();

        foreach (var item in root.GetProperty("Items").EnumerateArray())
        {
            var row = new Dictionary<string, string?>();
            foreach (var property in item.EnumerateObject())
            {
                row[property.Name] = property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
            }
            items.Add(row);
        }
        return new PageData(totalCount, totalPages, items);
    }

    private sealed record PageData(int TotalCount, int TotalPages, IReadOnlyList<IReadOnlyDictionary<string, string?>> Items);
}