using OpinionesETL.Application.Configuration;
using OpinionesETL.Infrastructure;
using OpinionesETL.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<CsvSourceOptions>(builder.Configuration.GetSection(CsvSourceOptions.SectionName));
builder.Services.Configure<DatabaseSourceOptions>(builder.Configuration.GetSection(DatabaseSourceOptions.SectionName));
builder.Services.Configure<ApiSourceOptions>(builder.Configuration.GetSection(ApiSourceOptions.SectionName));
builder.Services.Configure<StagingOptions>(builder.Configuration.GetSection(StagingOptions.SectionName));

builder.Services.AddInfrastructure(); // <-- NUEVA LÍNEA
builder.Services.AddHostedService<EtlWorker>();

var host = builder.Build();
host.Run();