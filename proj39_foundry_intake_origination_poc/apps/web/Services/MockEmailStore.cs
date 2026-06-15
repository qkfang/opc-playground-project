using System.Text.Json;
using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services;

/// <summary>
/// In-memory store of mocked inbound emails that act as the trigger source for the pipeline.
/// Seeds from Data/mock-emails.json on startup; supports listing, fetching, and adding a custom
/// email (so the demo "compose your own inbound email" flow works).
/// </summary>
public sealed class MockEmailStore
{
    private readonly List<InboundEmail> _emails = new();
    private readonly object _gate = new();

    public MockEmailStore(IWebHostEnvironment env, ILogger<MockEmailStore> logger)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "Data", "mock-emails.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var seed = JsonSerializer.Deserialize<List<InboundEmail>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (seed is not null) _emails.AddRange(seed.OrderByDescending(e => e.ReceivedUtc));
            }
            else
            {
                logger.LogWarning("mock-emails.json not found at {Path}; inbox will start empty.", path);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed mock emails.");
        }
    }

    public IReadOnlyList<InboundEmail> List()
    {
        lock (_gate) return _emails.OrderByDescending(e => e.ReceivedUtc).ToList();
    }

    public InboundEmail? Get(string id)
    {
        lock (_gate) return _emails.FirstOrDefault(e => e.Id == id);
    }

    public InboundEmail Add(InboundEmail email)
    {
        if (string.IsNullOrWhiteSpace(email.Id)) email.Id = Guid.NewGuid().ToString("n");
        if (email.ReceivedUtc == default) email.ReceivedUtc = DateTimeOffset.UtcNow;
        lock (_gate) _emails.Add(email);
        return email;
    }
}
