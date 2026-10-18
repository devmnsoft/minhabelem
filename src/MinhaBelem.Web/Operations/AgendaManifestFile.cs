namespace MinhaBelem.Web.Operations;

public static class AgendaManifestFile
{
    public static string Resolve(string requested, string workingDirectory, string contentRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);
        if (Path.IsPathRooted(requested))
        {
            var absolute = Path.GetFullPath(requested);
            if (File.Exists(absolute)) return absolute;
        }
        else
        {
            // dotnet run changes the working directory to the web project.
            var roots = new List<string> { workingDirectory, contentRoot };
            foreach (var start in new[] { workingDirectory, contentRoot })
                for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
                    if (File.Exists(Path.Combine(directory.FullName, "MinhaBelem.sln")))
                    {
                        roots.Add(directory.FullName);
                        break;
                    }
            foreach (var root in roots.Distinct())
            {
                var candidate = Path.GetFullPath(requested, root);
                if (File.Exists(candidate)) return candidate;
            }
        }
        throw new FileNotFoundException($"Manifesto não encontrado: {requested}. Informe um caminho absoluto ou relativo ao projeto Web ou à raiz do repositório.");
    }
}
