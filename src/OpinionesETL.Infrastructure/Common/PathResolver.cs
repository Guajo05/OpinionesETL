namespace OpinionesETL.Infrastructure.Common;

public static class PathResolver
{
    /// <summary>
    /// Si la ruta es relativa, la resuelve contra la carpeta que contiene el archivo .sln.
    /// </summary>
    public static string Resolve(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            // Busca el archivo .sln para saber dónde está la raíz del proyecto
            if (dir.GetFiles("*.sln").Length > 0)
            {
                return Path.GetFullPath(Path.Combine(dir.FullName, path));
            }

            dir = dir.Parent;
        }

        return Path.GetFullPath(path);
    }
}