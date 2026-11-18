using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Mantener los nombres de las propiedades exactos (IdComment, IdCliente, etc.)
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = null);

var app = builder.Build();

var apiKey = app.Configuration["ApiKey"] ?? "clave-de-prueba-123";
var csvPath = ResolvePath(app.Configuration["CsvPath"] ?? "data/input/social_comments.csv");

if (!File.Exists(csvPath))
{
    throw new FileNotFoundException($"No se encontró el archivo de comentarios: {csvPath}");
}

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    MissingFieldFound = null,
    HeaderValidated = null
};

List<SocialComment> comments;
using (var reader = new StreamReader(csvPath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
using (var csv = new CsvReader(reader, csvConfig))
{
    comments = csv.GetRecords<SocialComment>().ToList();
}

app.Logger.LogInformation("API simulada lista: {Count} comentarios cargados desde {Path}", comments.Count, csvPath);

app.MapGet("/api/social-comments", (HttpContext context, int? page, int? pageSize) =>
{
    // Validación de Seguridad (API Key)
    if (!context.Request.Headers.TryGetValue("X-API-KEY", out var provided) || provided != apiKey)
    {
        return Results.Unauthorized();
    }

    var currentPage = Math.Max(page ?? 1, 1);
    var size = Math.Clamp(pageSize ?? 100, 1, 500);

    var items = comments.Skip((currentPage - 1) * size).Take(size).ToList();

    return Results.Ok(new
    {
        Page = currentPage,
        PageSize = size,
        TotalCount = comments.Count,
        TotalPages = (int)Math.Ceiling(comments.Count / (double)size),
        Items = items
    });
});

static string ResolvePath(string path)
{
    if (Path.IsPathRooted(path)) return path;
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null)
    {
        if (dir.GetFiles("*.sln").Length > 0)
            return Path.GetFullPath(Path.Combine(dir.FullName, path));
        dir = dir.Parent;
    }
    return Path.GetFullPath(path);
}

app.Run();

public class SocialComment
{
    public string IdComment { get; set; } = string.Empty;
    public string? IdCliente { get; set; }
    public string IdProducto { get; set; } = string.Empty;
    public string Fuente { get; set; } = string.Empty;
    public string Fecha { get; set; } = string.Empty;
    public string Comentario { get; set; } = string.Empty;
}