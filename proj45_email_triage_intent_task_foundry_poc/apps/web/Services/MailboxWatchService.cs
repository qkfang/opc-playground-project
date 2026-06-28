using System.Text.Json;
using Proj45.RelayDesk.Web.Models;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>
/// Mock mailbox-watch surface. Serves the seed inbox (Data/seed-emails.json) that simulates a
/// shared service mailbox being watched. "Watch" is modelled by exposing the unread items and a
/// per-item ingest; there is no real Microsoft Graph connection in this POC.
/// </summary>
public sealed class MailboxWatchService
{
    private readonly List<IncomingEmail> _inbox;

    public MailboxWatchService(IWebHostEnvironment env, ILogger<MailboxWatchService> log)
    {
        _inbox = LoadSeed(env, log);
    }

    public IReadOnlyList<IncomingEmail> Inbox() => _inbox;

    public IReadOnlyList<IncomingEmail> Unread() => _inbox.Where(e => e.Unread).ToList();

    public IncomingEmail? Get(string id) =>
        _inbox.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Marks an email as read once it has been ingested by the pipeline.</summary>
    public void MarkRead(string id)
    {
        var e = Get(id);
        if (e is not null) e.Unread = false;
    }

    private static List<IncomingEmail> LoadSeed(IWebHostEnvironment env, ILogger log)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "Data", "seed-emails.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<IncomingEmail>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items is { Count: > 0 })
                {
                    foreach (var e in items)
                        if (e.ReceivedUtc == default) e.ReceivedUtc = DateTimeOffset.UtcNow;
                    log.LogInformation("Loaded {Count} seed emails for the watched mailbox.", items.Count);
                    return items;
                }
            }
            else log.LogWarning("Seed emails file not found at {Path}; using fallback.", path);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to load seed emails; using fallback.");
        }
        return Fallback();
    }

    private static List<IncomingEmail> Fallback() => new()
    {
        new IncomingEmail
        {
            Id = "fallback1",
            From = "priya.nair@brightwave-retail.com",
            FromName = "Priya Nair",
            Subject = "Overcharged on invoice INV-44821 - urgent",
            Body = "We were charged $4,800 but our contract is $3,200/month. Please issue a credit for the difference. " +
                   "We are a long-standing Brightwave Retail customer.",
            Channel = "email",
            Mailbox = "billing@relay-desk.example",
            Attachments = new() { "INV-44821.pdf" }
        }
    };
}
