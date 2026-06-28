using System.Diagnostics;
using System.Text.Json;
using Proj45.RelayDesk.Web.Models;

namespace Proj45.RelayDesk.Web.Services.Mcp;

/// <summary>Describes one mock D365 MCP tool for the catalog surface (Task page + /api/mcp/tools).</summary>
public sealed record McpToolDescriptor(string Name, string Category, string Description, string[] Args);

/// <summary>
/// Mock "Dynamics 365" MCP server. Exposes a small tool catalog the Task agent can call to look up
/// customer/account/contact/opportunity/service context and to simulate downstream operations
/// (open a case, raise a credit memo, schedule a callback, flag churn). Backed by an in-memory
/// dataset seeded from Data/seed-d365.json. Every invocation returns a recorded <see cref="McpToolCall"/>.
///
/// This stands in for a real Model Context Protocol server fronting Dataverse/D365; the shape
/// (named tools + JSON args -> JSON result) mirrors how an agent would call MCP tools for real.
/// </summary>
public interface ID365McpServer
{
    IReadOnlyList<McpToolDescriptor> Catalog { get; }
    McpToolCall Invoke(string tool, IDictionary<string, string> args);
    /// <summary>Resolve the best-matching account for a set of hints (domain/company/name).</summary>
    D365Account? ResolveAccount(IEnumerable<string> hints, string? fromEmail);
}

public sealed class MockD365McpServer : ID365McpServer
{
    private readonly List<D365Account> _accounts;
    private readonly ILogger<MockD365McpServer> _log;
    private static int _opSeq = 9000;

    private static readonly JsonSerializerOptions JsonOut = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public MockD365McpServer(IWebHostEnvironment env, ILogger<MockD365McpServer> log)
    {
        _log = log;
        _accounts = LoadSeed(env, log);
    }

    public IReadOnlyList<McpToolDescriptor> Catalog { get; } = new List<McpToolDescriptor>
    {
        new("customer.search", "lookup", "Search customers/accounts by company name, domain or contact.", new[] { "query" }),
        new("account.get", "lookup", "Get a full account profile (industry, tier, ARR, owner, status).", new[] { "accountId" }),
        new("contact.get", "lookup", "Get the primary contacts for an account.", new[] { "accountId" }),
        new("opportunity.list", "lookup", "List open sales opportunities for an account.", new[] { "accountId" }),
        new("service.cases.list", "lookup", "List existing service cases for an account.", new[] { "accountId" }),
        new("case.create", "operation", "Open a new service case against an account.", new[] { "accountId", "title", "priority" }),
        new("creditmemo.raise", "operation", "Raise a credit memo (billing adjustment) for an account.", new[] { "accountId", "amount", "reason" }),
        new("callback.create", "operation", "Schedule a sales/support callback for an account.", new[] { "accountId", "when", "topic" }),
        new("churn.flag", "operation", "Flag an account as a churn/retention risk.", new[] { "accountId", "severity", "reason" })
    };

    public D365Account? ResolveAccount(IEnumerable<string> hints, string? fromEmail)
    {
        var domain = ExtractDomain(fromEmail);
        if (domain is not null)
        {
            var byDomain = _accounts.FirstOrDefault(a => a.Domains.Any(d => string.Equals(d, domain, StringComparison.OrdinalIgnoreCase)));
            if (byDomain is not null) return byDomain;
        }
        foreach (var hint in hints.Where(h => !string.IsNullOrWhiteSpace(h)))
        {
            var h = hint.Trim();
            var match = _accounts.FirstOrDefault(a =>
                a.Name.Contains(h, StringComparison.OrdinalIgnoreCase) ||
                a.Domains.Any(d => d.Contains(h, StringComparison.OrdinalIgnoreCase)) ||
                a.Contacts.Any(c => c.Name.Contains(h, StringComparison.OrdinalIgnoreCase)));
            if (match is not null) return match;
        }
        return null;
    }

    public McpToolCall Invoke(string tool, IDictionary<string, string> args)
    {
        var sw = Stopwatch.StartNew();
        var call = new McpToolCall
        {
            Tool = tool,
            Arguments = new Dictionary<string, string>(args, StringComparer.OrdinalIgnoreCase)
        };
        try
        {
            object payload = tool switch
            {
                "customer.search" => CustomerSearch(Arg(args, "query")),
                "account.get" => AccountGet(Arg(args, "accountId")),
                "contact.get" => ContactGet(Arg(args, "accountId")),
                "opportunity.list" => OpportunityList(Arg(args, "accountId")),
                "service.cases.list" => ServiceCasesList(Arg(args, "accountId")),
                "case.create" => CaseCreate(Arg(args, "accountId"), Arg(args, "title"), Arg(args, "priority")),
                "creditmemo.raise" => CreditMemoRaise(Arg(args, "accountId"), Arg(args, "amount"), Arg(args, "reason")),
                "callback.create" => CallbackCreate(Arg(args, "accountId"), Arg(args, "when"), Arg(args, "topic")),
                "churn.flag" => ChurnFlag(Arg(args, "accountId"), Arg(args, "severity"), Arg(args, "reason")),
                _ => new { error = $"Unknown tool '{tool}'." }
            };
            sw.Stop();
            call.Ok = payload is not { } p || p.GetType().GetProperty("error") is null;
            call.ResultJson = JsonSerializer.Serialize(payload, JsonOut);
            call.ResultSummary = Summarise(tool, payload);
            call.DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            call.Ok = false;
            call.ResultSummary = $"Tool '{tool}' failed: {ex.GetType().Name}";
            call.ResultJson = JsonSerializer.Serialize(new { error = ex.Message }, JsonOut);
            call.DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds);
            _log.LogWarning(ex, "Mock MCP tool '{Tool}' threw.", tool);
        }
        return call;
    }

    // ---------------- Tool implementations ----------------

    private object CustomerSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return new { matches = Array.Empty<object>(), count = 0 };
        var q = query.Trim();
        var matches = _accounts.Where(a =>
                a.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                a.Domains.Any(d => d.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                a.Contacts.Any(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Email.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Select(a => new { a.AccountId, a.Name, a.Tier, a.Industry, primaryContact = a.Contacts.FirstOrDefault(c => c.Primary)?.Name })
            .ToList();
        return new { query = q, count = matches.Count, matches };
    }

    private object AccountGet(string accountId)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        return new
        {
            a.AccountId, a.Name, a.Tier, a.Industry, a.AnnualValue, a.Owner, a.Status, a.ContractMonthly,
            openOpportunities = a.Opportunities.Count,
            openServiceCases = a.ServiceCases.Count(c => c.Status is "Open")
        };
    }

    private object ContactGet(string accountId)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        return new { a.AccountId, contacts = a.Contacts };
    }

    private object OpportunityList(string accountId)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        return new { a.AccountId, count = a.Opportunities.Count, opportunities = a.Opportunities };
    }

    private object ServiceCasesList(string accountId)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        return new { a.AccountId, count = a.ServiceCases.Count, serviceCases = a.ServiceCases };
    }

    private object CaseCreate(string accountId, string title, string priority)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        var id = $"CASE-{Interlocked.Increment(ref _opSeq)}";
        return new { created = true, caseId = id, accountId = a.AccountId, title, priority = string.IsNullOrWhiteSpace(priority) ? "P3" : priority, status = "Open" };
    }

    private object CreditMemoRaise(string accountId, string amount, string reason)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        var id = $"CM-{Interlocked.Increment(ref _opSeq)}";
        var amt = LenientAmount(amount);
        return new { created = true, creditMemoId = id, accountId = a.AccountId, amount = amt, reason, status = "Pending approval" };
    }

    private object CallbackCreate(string accountId, string when, string topic)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        var id = $"CB-{Interlocked.Increment(ref _opSeq)}";
        return new { created = true, callbackId = id, accountId = a.AccountId, when = string.IsNullOrWhiteSpace(when) ? "within 1 business day" : when, topic, owner = a.Owner };
    }

    private object ChurnFlag(string accountId, string severity, string reason)
    {
        var a = Find(accountId);
        if (a is null) return new { error = $"Account '{accountId}' not found." };
        var id = $"CHURN-{Interlocked.Increment(ref _opSeq)}";
        return new { created = true, churnSignalId = id, accountId = a.AccountId, severity = string.IsNullOrWhiteSpace(severity) ? "Medium" : severity, reason, routedTo = a.Owner };
    }

    // ---------------- Helpers ----------------

    private D365Account? Find(string accountId) =>
        _accounts.FirstOrDefault(a => string.Equals(a.AccountId, accountId, StringComparison.OrdinalIgnoreCase));

    private static string Arg(IDictionary<string, string> args, string key) =>
        args.TryGetValue(key, out var v) ? v ?? "" : "";

    private static decimal? LenientAmount(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var cleaned = new string(s.Where(c => char.IsDigit(c) || c == '.' ).ToArray());
        return decimal.TryParse(cleaned, out var d) ? d : null;
    }

    private static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
    }

    private static string Summarise(string tool, object payload)
    {
        try
        {
            var json = JsonSerializer.SerializeToElement(payload);
            if (json.TryGetProperty("error", out var err)) return err.GetString() ?? "error";
            return tool switch
            {
                "customer.search" => $"{json.GetProperty("count").GetInt32()} match(es) for '{json.GetProperty("query").GetString()}'.",
                "account.get" => $"{json.GetProperty("Name").GetString()} · {json.GetProperty("Tier").GetString()} · {json.GetProperty("Status").GetString()}.",
                "contact.get" => $"{json.GetProperty("contacts").GetArrayLength()} contact(s).",
                "opportunity.list" => $"{json.GetProperty("count").GetInt32()} open opportunity(ies).",
                "service.cases.list" => $"{json.GetProperty("count").GetInt32()} service case(s).",
                "case.create" => $"Opened {json.GetProperty("caseId").GetString()} ({json.GetProperty("priority").GetString()}).",
                "creditmemo.raise" => $"Raised {json.GetProperty("creditMemoId").GetString()} — {json.GetProperty("status").GetString()}.",
                "callback.create" => $"Scheduled {json.GetProperty("callbackId").GetString()} ({json.GetProperty("when").GetString()}).",
                "churn.flag" => $"Flagged {json.GetProperty("churnSignalId").GetString()} ({json.GetProperty("severity").GetString()}).",
                _ => "ok"
            };
        }
        catch { return "ok"; }
    }

    private static List<D365Account> LoadSeed(IWebHostEnvironment env, ILogger log)
    {
        try
        {
            var path = Path.Combine(env.ContentRootPath, "Data", "seed-d365.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var doc = JsonSerializer.Deserialize<D365Seed>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (doc?.Accounts is { Count: > 0 })
                {
                    log.LogInformation("Loaded {Count} mock D365 accounts.", doc.Accounts.Count);
                    return doc.Accounts;
                }
            }
            else log.LogWarning("Seed D365 file not found at {Path}; using fallback.", path);
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Failed to load D365 seed; using fallback.");
        }
        return new List<D365Account>
        {
            new()
            {
                AccountId = "ACC-2001", Name = "Brightwave Retail", Domains = new() { "brightwave-retail.com" },
                Tier = "Gold", Industry = "Retail", AnnualValue = 384000, Owner = "Dana Whitfield", Status = "Active", ContractMonthly = 3200,
                Contacts = new() { new D365Contact { Name = "Priya Nair", Title = "Head of Finance", Email = "priya.nair@brightwave-retail.com", Primary = true } }
            }
        };
    }
}

// ---- Seed-backed data shapes ----

public sealed class D365Seed { public List<D365Account> Accounts { get; set; } = new(); }

public sealed class D365Account
{
    public string AccountId { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Domains { get; set; } = new();
    public string Tier { get; set; } = "";
    public string Industry { get; set; } = "";
    public decimal? AnnualValue { get; set; }
    public string Owner { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal? ContractMonthly { get; set; }
    public List<D365Contact> Contacts { get; set; } = new();
    public List<D365Opportunity> Opportunities { get; set; } = new();
    public List<D365ServiceCase> ServiceCases { get; set; } = new();
}

public sealed class D365Contact
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Email { get; set; } = "";
    public bool Primary { get; set; }
}

public sealed class D365Opportunity
{
    public string OpportunityId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Stage { get; set; } = "";
    public decimal? Amount { get; set; }
}

public sealed class D365ServiceCase
{
    public string CaseId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
}
