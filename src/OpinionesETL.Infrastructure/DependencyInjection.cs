using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Infrastructure.Extractors;
using OpinionesETL.Infrastructure.Staging;

namespace OpinionesETL.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpClient(ApiExtractor.HttpClientName, (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ApiSourceOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                client.BaseAddress = new Uri(options.BaseUrl);

            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
                client.DefaultRequestHeaders.Add("X-API-KEY", options.ApiKey);
        });

        services.AddSingleton<IExtractor, CsvExtractor>();
        services.AddSingleton<IExtractor, DatabaseExtractor>();
        services.AddSingleton<IExtractor, ApiExtractor>();

        services.AddSingleton<StagingWriter>(); // <-- NUEVO REGISTRO

        return services;
    }
}