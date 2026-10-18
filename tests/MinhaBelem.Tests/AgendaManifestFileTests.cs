using MinhaBelem.Web.Operations;

namespace MinhaBelem.Tests;

public sealed class AgendaManifestFileTests
{
    [Fact]
    public void ResolvesRepositoryRelativeManifestFromDotnetRunProjectDirectory()
    {
        var root=Directory.CreateTempSubdirectory("minhabelem-manifest-").FullName;
        var src=Path.Combine(root,"src");var web=Path.Combine(src,"MinhaBelem.Web");
        var data=Path.Combine(root,"data");var agenda=Path.Combine(data,"agenda");
        var solution=Path.Combine(root,"MinhaBelem.sln");var manifest=Path.Combine(agenda,"events.json");
        Directory.CreateDirectory(web);Directory.CreateDirectory(agenda);
        try
        {
            File.WriteAllText(solution,"");File.WriteAllText(manifest,"{}");
            Assert.Equal(manifest,AgendaManifestFile.Resolve("data/agenda/events.json",web,web));
            Assert.Equal(manifest,AgendaManifestFile.Resolve("data/agenda/events.json",root,web));
            Assert.Equal(manifest,AgendaManifestFile.Resolve(manifest,web,web));
            Assert.Equal(manifest,AgendaManifestFile.Resolve("../../data/agenda/events.json",web,web));
            var missing=Assert.Throws<FileNotFoundException>(()=>AgendaManifestFile.Resolve("data/agenda/missing.json",web,web));
            Assert.Contains("caminho absoluto",missing.Message);
            Assert.Throws<FileNotFoundException>(()=>AgendaManifestFile.Resolve(Path.Combine(root,"missing.json"),web,web));
        }
        finally
        {
            // Delete only the exact fixture files and empty directories; never recurse.
            File.Delete(manifest);File.Delete(solution);
            Directory.Delete(agenda);Directory.Delete(data);Directory.Delete(web);Directory.Delete(src);Directory.Delete(root);
        }
    }
}
