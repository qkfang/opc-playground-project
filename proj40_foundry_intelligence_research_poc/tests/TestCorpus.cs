using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Proj40.IntelligenceResearch.Web.Services;

namespace Proj40.IntelligenceResearch.Tests;

/// <summary>
/// Builds a real <see cref="SourceCorpus"/> for unit tests by pointing a minimal fake
/// <see cref="IWebHostEnvironment"/> at the web project's content root (which contains Data/).
/// </summary>
internal static class TestCorpus
{
    public static SourceCorpus Load() => new(FakeEnv(), NullLogger<SourceCorpus>.Instance);

    public static MockEmailStore Emails() => new(FakeEnv(), NullLogger<MockEmailStore>.Instance);

    /// <summary>Build a ResearchCaseService with explicit storage options and content root (for persistence tests).</summary>
    public static ResearchCaseService Cases(Proj40.IntelligenceResearch.Web.Models.StorageOptions storage, string? contentRoot = null)
    {
        var env = new FakeWebHostEnvironment(contentRoot ?? ResolveWebContentRoot());
        return new ResearchCaseService(env, storage, NullLogger<ResearchCaseService>.Instance);
    }

    private static IWebHostEnvironment FakeEnv() => new FakeWebHostEnvironment(ResolveWebContentRoot());

    /// <summary>Find apps/web relative to the test output dir, walking up to the project root.</summary>
    private static string ResolveWebContentRoot()
    {
        // Test runs from tests/bin/<cfg>/net10.0; the web project is at ../../apps/web.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "apps", "web", "Data");
            if (Directory.Exists(candidate)) return Path.Combine(dir.FullName, "apps", "web");

            // When running from tests/bin/..., the repo project folder is two levels up from tests/.
            var sibling = Path.Combine(dir.FullName, "..", "apps", "web", "Data");
            if (Directory.Exists(sibling)) return Path.GetFullPath(Path.Combine(dir.FullName, "..", "apps", "web"));
        }
        throw new DirectoryNotFoundException("Could not locate apps/web/Data relative to the test assembly.");
    }

    private sealed class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public FakeWebHostEnvironment(string contentRoot)
        {
            ContentRootPath = contentRoot;
            WebRootPath = Path.Combine(contentRoot, "wwwroot");
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
            WebRootFileProvider = Directory.Exists(WebRootPath)
                ? new PhysicalFileProvider(WebRootPath)
                : new NullFileProvider();
        }

        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Proj40.IntelligenceResearch.Tests";
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
    }
}
