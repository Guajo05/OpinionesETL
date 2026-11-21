using Microsoft.Extensions.Configuration; // <-- NUEVO
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpinionesETL.Application.Configuration;
using OpinionesETL.Application.Interfaces;
using OpinionesETL.Infrastructure.Extractors;
using OpinionesETL.Infrastructure.Loaders; // <-- NUEVO
using OpinionesETL.Infrastructure.Staging;

namespace OpinionesETL.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration) // <-- Agregamos IConfiguration
    {
        // ... (Tu configuración de HttpClient se queda igual) ...
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
        services.AddSingleton<StagingWriter>();

        // NUEVO REGISTRO PARA EL LOADER
        services.AddSingleton<DimensionLoader>(sp =>
        {
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DimensionLoader>>();
            var connString = configuration["Sources:DataWarehouse:ConnectionString"]
                ?? "Server=(localdb)\\MSSQLLocalDB;Database=OpinionesDW;Trusted_Connection=True;TrustServerCertificate=True";
            return new DimensionLoader(connString, logger);
        });

        return services;
    }
}