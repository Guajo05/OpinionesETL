namespace OpinionesETL.Application.Configuration;

public sealed class CsvFileOptions
{
    public string Name { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public sealed class CsvSourceOptions
{
    public const string SectionName = "Sources:Csv";
    public bool Enabled { get; set; } = true;
    public string Folder { get; set; } = "data/input";
    public string Delimiter { get; set; } = ",";
    public List<CsvFileOptions> Files { get; set; } = new();
}

public sealed class DatabaseSourceOptions
{
    public const string SectionName = "Sources:Database";
    public bool Enabled { get; set; } = true;
    public string ConnectionString { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public int CommandTimeoutSeconds { get; set; } = 60;
}

public sealed class ApiSourceOptions
{
    public const string SectionName = "Sources:Api";
    public bool Enabled { get; set; } = true;
    public string BaseUrl { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int PageSize { get; set; } = 100;
    public int MaxParallelRequests { get; set; } = 4;
}

public sealed class StagingOptions
{
    public const string SectionName = "Staging";
    public string OutputFolder { get; set; } = "data/staging";
}