using System.Text.Json;
using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services;

/// <summary>
/// Supplies the mock inbound mailbox that drives the POC. Emails are loaded from a bundled JSON seed
/// (Data/seed-mailbox.json) with a hard-coded fallback so the demo always has realistic, varied traffic
/// across segments, industries, and intent levels (including a spam case for the disqualification path).
/// </summary>
public sealed class MailboxService
{
    private readonly List<InboundEmail> _seed;

    public MailboxService(IWebHostEnvironment env, ILogger<MailboxService> logger)
    {
        _seed = LoadSeed(env, logger);
    }

    public IReadOnlyList<InboundEmail> Inbox() => _seed;

    public InboundEmail? Get(string id) => _seed.FirstOrDefault(e => e.Id == id);

    private static List<InboundEmail> LoadSeed(IWebHostEnvironment env, ILogger logger)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "Data", "seed-mailbox.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var list = JsonSerializer.Deserialize<List<InboundEmail>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (list is { Count: > 0 }) return list;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not load seed mailbox; using built-in fallback.");
        }
        return BuiltInFallback();
    }

    private static List<InboundEmail> BuiltInFallback() => new()
    {
        new InboundEmail
        {
            Id = "demo-strategic",
            From = "priya.nair@globalbankcorp.com",
            FromName = "Priya Nair",
            Subject = "RFP: Enterprise AI & data platform for fraud analytics",
            Channel = "email",
            Body = "Hi, I'm the Chief Data Officer at GlobalBank Corp, a multinational bank with 18,000 employees headquartered in Singapore. " +
                   "We're running an RFP this quarter to modernise our fraud analytics with an enterprise AI & ML platform. Budget approved (~$1.2M). " +
                   "We're currently evaluating Databricks and Snowflake too. Can we book a discovery call urgently? Company: GlobalBank Corp."
        },
        new InboundEmail
        {
            Id = "demo-enterprise",
            From = "tom.reilly@meridian-health.com",
            FromName = "Tom Reilly",
            Subject = "Exploring analytics modernisation for patient engagement",
            Channel = "web-form",
            Body = "Hello, I'm a Director of Engineering at Meridian Health (a healthcare provider, ~3,500 staff, United Kingdom). " +
                   "We're exploring an advanced analytics suite for patient engagement and would like to understand pricing. Timeline is this year. Early stage / still researching."
        },
        new InboundEmail
        {
            Id = "demo-midmarket",
            From = "sara.lopez@brightretail.com.au",
            FromName = "Sara Lopez",
            Subject = "Demand forecasting pilot - growing retailer",
            Channel = "email",
            Body = "Hi team, Sara here, IT Manager at BrightRetail, a growing mid-market retailer in Australia (around 600 employees). " +
                   "We'd like to pilot a demand forecasting / analytics solution. We have some budget for a POC this quarter. Company: BrightRetail Pty."
        },
        new InboundEmail
        {
            Id = "demo-expansion",
            From = "james.okafor@contoso-manufacturing.com",
            FromName = "James Okafor",
            Subject = "Renew + expand our current data platform contract",
            Channel = "email",
            Body = "Hello, James here (VP of Engineering, Contoso Manufacturing). We're an existing customer and want to renew and extend our contract, " +
                   "plus add predictive maintenance. Global manufacturer, 12,000 employees. Let's get this moving this quarter."
        },
        new InboundEmail
        {
            Id = "demo-spam",
            From = "deals@cheap-seo-now.biz",
            FromName = "SEO Deals",
            Subject = "Boost your ranking - buy followers + SEO services!!!",
            Channel = "email",
            Body = "Special offer!!! We provide SEO services and can buy followers for your brand. Unsubscribe anytime. Crypto giveaway included!"
        },
    };
}
