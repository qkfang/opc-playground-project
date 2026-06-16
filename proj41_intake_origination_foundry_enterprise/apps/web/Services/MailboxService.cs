using System.Text.Json;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// Provides the mock broker-submission mailbox that triggers the underwriting pipeline.
/// Seeds from Data/seed-submissions.json when present, otherwise from a built-in fallback set.
/// </summary>
public sealed class MailboxService
{
    private readonly List<SubmissionEmail> _inbox;

    public MailboxService(IWebHostEnvironment env, ILogger<MailboxService> log)
    {
        _inbox = LoadSeed(env, log);
    }

    public IReadOnlyList<SubmissionEmail> Inbox() => _inbox;

    public SubmissionEmail? Get(string id) =>
        _inbox.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    private static List<SubmissionEmail> LoadSeed(IWebHostEnvironment env, ILogger log)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "Data", "seed-submissions.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<SubmissionEmail>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items is { Count: > 0 })
                {
                    log.LogInformation("Loaded {Count} seed submissions.", items.Count);
                    return items;
                }
            }
            else log.LogWarning("Seed submissions file not found at {Path}; using fallback.", path);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to load seed submissions; using fallback.");
        }
        return Fallback();
    }

    private static List<SubmissionEmail> Fallback() => new()
    {
        new SubmissionEmail
        {
            Id = "fallback1",
            From = "j.okafor@summit-risk.com",
            FromName = "James Okafor",
            Subject = "Submission: Property + BI for Atlas Steel Fabrication Inc",
            Body = "Hi, please find a new business submission for our client, Atlas Steel Fabrication Inc, a metal fabrication " +
                   "manufacturer established 2004 with 320 employees across 3 locations in Houston, Texas. TIV is approximately " +
                   "$85M. They are seeking commercial property cover with a $50M limit, effective 01/08/2026. Currently with Travelers. " +
                   "Brokerage: Summit Risk Partners. Regards, James Okafor, Account Executive",
            Channel = "email"
        }
    };
}
