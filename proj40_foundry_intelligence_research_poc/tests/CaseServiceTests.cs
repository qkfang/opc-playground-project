using Proj40.IntelligenceResearch.Web.Models;
using Xunit;

namespace Proj40.IntelligenceResearch.Tests;

/// <summary>
/// Persistence/regression tests for <see cref="Web.Services.ResearchCaseService"/>.
/// Covers the Azure App Service read-only-filesystem case (WEBSITE_RUN_FROM_PACKAGE=1) that must
/// degrade to in-memory instead of throwing in the constructor (which 400'd every pipeline request).
/// </summary>
public class CaseServiceTests
{
    private static ResearchCase Sample(string id) => new()
    {
        CaseId = id,
        CreatedUtc = DateTime.UtcNow,
        EmailId = "eml-test",
        Engine = "offline",
    };

    [Fact]
    public async Task Persists_And_Reads_Back_When_Folder_Is_Writable()
    {
        var tmp = Path.Combine(Path.GetTempPath(), "proj40-cases-" + Guid.NewGuid().ToString("N"));
        try
        {
            var svc = TestCorpus.Cases(new StorageOptions { Mode = "local", LocalDataFolder = tmp });
            var c = Sample("writable-1");
            await svc.SaveAsync(c);

            // Round-trips via cache.
            Assert.NotNull(svc.Get("writable-1"));
            Assert.Contains(svc.List(), x => x.CaseId == "writable-1");

            // And actually hit disk under <tmp>/cases.
            Assert.True(File.Exists(Path.Combine(tmp, "cases", "writable-1.json")));

            // A fresh instance over the same folder reloads the persisted case.
            var svc2 = TestCorpus.Cases(new StorageOptions { Mode = "local", LocalDataFolder = tmp });
            Assert.NotNull(svc2.Get("writable-1"));
        }
        finally
        {
            if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true);
        }
    }

    [Fact]
    public async Task Degrades_To_InMemory_When_Folder_Is_Not_Writable()
    {
        // Simulate a read-only target: point LocalDataFolder under an existing *file* so
        // Directory.CreateDirectory throws (IOException), like /home/site/wwwroot/App_Data on App Service.
        var filePath = Path.Combine(Path.GetTempPath(), "proj40-notadir-" + Guid.NewGuid().ToString("N"));
        await File.WriteAllTextAsync(filePath, "x");
        try
        {
            var badFolder = Path.Combine(filePath, "App_Data"); // child of a file => cannot be created

            // Constructor must NOT throw (this is the production defect being regressed).
            var svc = TestCorpus.Cases(new StorageOptions { Mode = "local", LocalDataFolder = badFolder });

            // Save + Get still work via the in-memory cache; no exception surfaces to the request.
            var c = Sample("inmem-1");
            await svc.SaveAsync(c);
            Assert.NotNull(svc.Get("inmem-1"));
            Assert.Contains(svc.List(), x => x.CaseId == "inmem-1");
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }
}
