using System.Text.Json;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// Loads the mock inbound emails (with attached customer documents) that seed the intake tray.
/// Backed by <c>Data/mock-emails.json</c> shipped with the app. Read-only, cached in memory.
/// </summary>
public sealed class MockEmailStore
{
    private readonly List<InboundEmail> _emails;

    public MockEmailStore(IWebHostEnvironment env, ILogger<MockEmailStore> logger)
    {
        var path = Path.Combine(env.ContentRootPath, "Data", "mock-emails.json");
        try
        {
            var json = File.ReadAllText(path);
            _emails = JsonSerializer.Deserialize<List<InboundEmail>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                      ?? new List<InboundEmail>();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load mock emails from {Path}; inbox will be empty.", path);
            _emails = new List<InboundEmail>();
        }
    }

    public IReadOnlyList<InboundEmail> All => _emails;

    public InboundEmail? GetById(string id) =>
        _emails.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
}
