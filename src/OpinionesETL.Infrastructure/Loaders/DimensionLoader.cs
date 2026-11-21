using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpinionesETL.Domain.Models;
using OpinionesETL.Infrastructure.Common;

namespace OpinionesETL.Infrastructure.Loaders;

public sealed class DimensionLoader
{
    private readonly string _connectionString;
    private readonly ILogger<DimensionLoader> _logger;

    public DimensionLoader(string connectionString, ILogger<DimensionLoader> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task LoadDimensionsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("=== INICIANDO CARGA DE DIMENSIONES ===");

        var stagingFolder = PathResolver.Resolve("data/staging");

        await LoadClientesAsync(stagingFolder, cancellationToken);
        await LoadProductosAsync(stagingFolder, cancellationToken);
        await LoadFuentesAsync(stagingFolder, cancellationToken);

        _logger.LogInformation("=== CARGA DE DIMENSIONES COMPLETADA ===");
    }

    private async Task LoadClientesAsync(string folder, CancellationToken cancellationToken)
    {
        var file = Path.Combine(folder, "csv_clients.json");
        if (!File.Exists(file)) return;

        var result = await ReadStagingFileAsync(file, cancellationToken);
        if (result is null) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        int insertados = 0;
        foreach (var row in result.Records)
        {
            // Asumiendo que tu CSV tiene columnas como IdCliente, Nombre, Email
            var idOrigen = row.GetValueOrDefault("IdCliente") ?? row.GetValueOrDefault("id");
            var nombre = row.GetValueOrDefault("Nombre") ?? "Desconocido";
            var email = row.GetValueOrDefault("Email") ?? row.GetValueOrDefault("Correo");

            if (string.IsNullOrEmpty(idOrigen)) continue;
            // Limpiamos la 'C' del ID si viene como "C001" para que sea un número entero
            var idInt = int.TryParse(idOrigen.Replace("C", ""), out var val) ? val : (int?)null;
            if (idInt is null) continue;

            var query = @"
                IF NOT EXISTS (SELECT 1 FROM Dim_Cliente WHERE IdClienteOrigen = @IdOrigen)
                BEGIN
                    INSERT INTO Dim_Cliente (IdClienteOrigen, Nombre, Email)
                    VALUES (@IdOrigen, @Nombre, @Email)
                END";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@IdOrigen", idInt);
            cmd.Parameters.AddWithValue("@Nombre", nombre);
            cmd.Parameters.AddWithValue("@Email", (object?)email ?? DBNull.Value);

            var filasAfectadas = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (filasAfectadas > 0)
            {
                insertados++;
            }
        }
        _logger.LogInformation("[DW] Dim_Cliente: {Count} nuevos registros insertados", insertados);
    }

    private async Task LoadProductosAsync(string folder, CancellationToken cancellationToken)
    {
        var file = Path.Combine(folder, "csv_products.json");
        if (!File.Exists(file)) return;

        var result = await ReadStagingFileAsync(file, cancellationToken);
        if (result is null) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        int insertados = 0;
        foreach (var row in result.Records)
        {
            var idOrigen = row.GetValueOrDefault("IdProducto") ?? row.GetValueOrDefault("id");
            var nombre = row.GetValueOrDefault("Nombre") ?? "Desconocido";
            var categoria = row.GetValueOrDefault("Categoria") ?? "General";

            if (string.IsNullOrEmpty(idOrigen)) continue;
            var idInt = int.TryParse(idOrigen.Replace("P", ""), out var val) ? val : (int?)null;
            if (idInt is null) continue;

            var query = @"
                IF NOT EXISTS (SELECT 1 FROM Dim_Producto WHERE IdProductoOrigen = @IdOrigen)
                BEGIN
                    INSERT INTO Dim_Producto (IdProductoOrigen, Nombre, Categoria)
                    VALUES (@IdOrigen, @Nombre, @Categoria)
                END";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@IdOrigen", idInt);
            cmd.Parameters.AddWithValue("@Nombre", nombre);
            cmd.Parameters.AddWithValue("@Categoria", categoria);

            var filasAfectadas = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (filasAfectadas > 0)
            {
                insertados++;
            }
        }
        _logger.LogInformation("[DW] Dim_Producto: {Count} nuevos registros insertados", insertados);
    }

    private async Task LoadFuentesAsync(string folder, CancellationToken cancellationToken)
    {
        var file = Path.Combine(folder, "csv_fuente_datos.json");
        if (!File.Exists(file)) return;

        var result = await ReadStagingFileAsync(file, cancellationToken);
        if (result is null) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        int insertados = 0;
        foreach (var row in result.Records)
        {
            // En tu CSV de fuente_datos, busca las columnas de Tipo y Canal
            var tipo = row.GetValueOrDefault("TipoFuente") ?? "CSV";
            var canal = row.GetValueOrDefault("Canal") ?? "General";

            var query = @"
                IF NOT EXISTS (SELECT 1 FROM Dim_Fuente WHERE TipoFuente = @Tipo AND Canal = @Canal)
                BEGIN
                    INSERT INTO Dim_Fuente (TipoFuente, Canal)
                    VALUES (@Tipo, @Canal)
                END";

            await using var cmd = new SqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@Tipo", tipo);
            cmd.Parameters.AddWithValue("@Canal", canal);

            var filasAfectadas = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (filasAfectadas > 0)
            {
                insertados++;
            }
        }
        _logger.LogInformation("[DW] Dim_Fuente: {Count} nuevos registros insertados", insertados);
    }

    private async Task<ExtractionResult?> ReadStagingFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<ExtractionResult>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leyendo el archivo de staging {File}", filePath);
            return null;
        }
    }
}