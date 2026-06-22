using System.Runtime.CompilerServices;
using System.Text;
using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// Deterministic, grounded FinOps assistant. Classifies the user's intent, runs the matching
/// <see cref="FinOpsAnalytics"/> query, and renders a concise Markdown answer (headline + table + next
/// step). Streams the answer word-by-word so the SSE UI feels live. No AI dependency — always available,
/// fully unit-testable, and the exact behaviour the live Foundry+Fabric agent reproduces over real data.
/// </summary>
public sealed class OfflineFinOpsAgent : IFinOpsAgent
{
    private readonly FinOpsAnalytics _analytics;
    private readonly ConversationStore _store;
    private readonly MarkdownRenderer _md;

    public OfflineFinOpsAgent(FinOpsAnalytics analytics, ConversationStore store, MarkdownRenderer md)
    {
        _analytics = analytics;
        _store = store;
        _md = md;
    }

    public string Name => "offline";

    private string Cur => _analytics.Currency;

    public async Task<ChatResponse> ReplyAsync(string conversationId, string message, CancellationToken ct = default)
    {
        var id = _store.EnsureConversation(conversationId);
        _store.Add(id, "user", message);
        var (intent, markdown) = Answer(message);
        _store.Add(id, "assistant", markdown);
        return new ChatResponse
        {
            ConversationId = id,
            Reply = markdown,
            ReplyHtml = _md.ToHtml(markdown),
            Engine = Name,
            Tool = "offline-finops",
            Intent = intent,
        };
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string conversationId, string message, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var id = _store.EnsureConversation(conversationId);
        _store.Add(id, "user", message);
        var (intent, markdown) = Answer(message);

        yield return new ChatStreamEvent { Type = "meta", ConversationId = id, Engine = Name, Tool = "offline-finops", Data = intent };
        yield return new ChatStreamEvent { Type = "status", Data = "Querying FinOps dataset…", ConversationId = id };

        // Stream by word for a responsive feel.
        var sb = new StringBuilder();
        foreach (var token in Tokenize(markdown))
        {
            ct.ThrowIfCancellationRequested();
            sb.Append(token);
            yield return new ChatStreamEvent { Type = "token", Data = token, ConversationId = id };
            await Task.Delay(8, ct);
        }

        _store.Add(id, "assistant", markdown);
        yield return new ChatStreamEvent { Type = "done", ConversationId = id, Engine = Name, Tool = "offline-finops", Data = _md.ToHtml(markdown) };
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        // Preserve whitespace/newlines while emitting word-sized chunks.
        int i = 0;
        while (i < text.Length)
        {
            int j = i;
            bool ws = char.IsWhiteSpace(text[i]);
            while (j < text.Length && char.IsWhiteSpace(text[j]) == ws) j++;
            yield return text[i..j];
            i = j;
        }
    }

    // ---------------- Intent classification + answers ----------------

    public enum Intent { Greeting, Help, TotalSpend, Trend, TopServices, TopResourceGroups, TopSubscriptions,
        Anomalies, Coverage, Recommendations, Showback, Forecast, Unknown }

    public (string Intent, string Markdown) Answer(string message)
    {
        var intent = Classify(message);
        string md = intent switch
        {
            Intent.Greeting => Greeting(),
            Intent.Help => Help(),
            Intent.TotalSpend => TotalSpend(message),
            Intent.Trend => Trend(),
            Intent.TopServices => Top(FinOpsAnalytics.Dimension.Service, "services"),
            Intent.TopResourceGroups => Top(FinOpsAnalytics.Dimension.ResourceGroup, "resource groups"),
            Intent.TopSubscriptions => Top(FinOpsAnalytics.Dimension.Subscription, "subscriptions"),
            Intent.Anomalies => Anomalies(),
            Intent.Coverage => Coverage(),
            Intent.Recommendations => Recommendations(),
            Intent.Showback => Showback(message),
            Intent.Forecast => Forecast(),
            _ => Unknown(),
        };
        return (intent.ToString(), md);
    }

    private static bool Has(string m, params string[] words) =>
        words.Any(w => m.Contains(w, StringComparison.OrdinalIgnoreCase));

    public Intent Classify(string message)
    {
        var m = (message ?? "").Trim();
        if (m.Length == 0) return Intent.Help;
        if (Has(m, "hello", "hi ", "hey", "good morning", "good afternoon") && m.Length < 20) return Intent.Greeting;
        if (Has(m, "help", "what can you", "capab", "how do you", "who are you")) return Intent.Help;

        if (Has(m, "anomal", "spike", "unexpected", "jump", "unusual", "surge")) return Intent.Anomalies;
        if (Has(m, "coverage", "reservation", "savings plan", "committed", "commitment")) return Intent.Coverage;
        if (Has(m, "save", "saving", "optimi", "reduce cost", "cut cost", "recommend", "waste", "idle")) return Intent.Recommendations;
        if (Has(m, "forecast", "predict", "projection", "next month", "run rate", "run-rate")) return Intent.Forecast;
        if (Has(m, "trend", "over time", "last 6", "six month", "monthly", "history", "trending")) return Intent.Trend;

        bool top = Has(m, "top", "biggest", "largest", "highest", "most expensive", "break down", "breakdown", "by ");
        if (Has(m, "resource group", "rg ", "resourcegroup")) return Intent.TopResourceGroups;
        if (Has(m, "subscription", "sub ")) return Intent.TopSubscriptions;
        if (Has(m, "team", "cost center", "cost centre", "tag", "environment", "showback", "chargeback")) return Intent.Showback;
        if (top && Has(m, "service")) return Intent.TopServices;
        if (top) return Intent.TopServices;

        if (Has(m, "spend", "cost", "spent", "bill", "total", "how much")) return Intent.TotalSpend;
        return Intent.Unknown;
    }

    // ---------------- Answer renderers ----------------

    private string Greeting() =>
        "Hi — I'm **FinOps Copilot**. I can break down your Azure spend, spot anomalies, check commitment " +
        "coverage, and find savings (grounded in your governed Fabric data). Ask me something like " +
        "*\"What did we spend last month?\"* or *\"Where can we save money?\"*";

    private string Help()
    {
        var sb = new StringBuilder();
        sb.AppendLine("**I'm FinOps Copilot.** I answer cloud-cost questions over your governed Microsoft Fabric data. Try:");
        sb.AppendLine();
        foreach (var s in AgentPersona.Suggestions) sb.AppendLine($"- {s}");
        sb.AppendLine();
        sb.AppendLine("_All figures are in USD, amortised, from the FinOps dataset._");
        return sb.ToString();
    }

    private string TotalSpend(string message)
    {
        var (y, mo) = _analytics.LatestFullMonth();
        decimal month = _analytics.SpendForMonth(y, mo);
        var trend = _analytics.MonthlyTrend(2);
        decimal prev = trend.Count == 2 ? trend[0].Cost : month;
        decimal delta = prev > 0 ? (month - prev) / prev * 100m : 0;
        string arrow = delta > 0 ? "▲" : delta < 0 ? "▼" : "→";
        decimal ytd = _analytics.TotalSpend();

        var sb = new StringBuilder();
        sb.AppendLine($"**{Cur} {month:N0}** in {y:0000}-{mo:00} (latest full month).");
        sb.AppendLine();
        sb.AppendLine($"- Month-over-month: {arrow} {Math.Abs(delta):N1}% vs previous month ({Cur} {prev:N0})");
        sb.AppendLine($"- Trailing 12 months total: {Cur} {ytd:N0}");
        sb.AppendLine();
        sb.AppendLine("_Window: full calendar month, USD amortised. Ask for a **trend** or **top services** to drill in._");
        return sb.ToString();
    }

    private string Trend()
    {
        var trend = _analytics.MonthlyTrend(6);
        var sb = new StringBuilder();
        sb.AppendLine("**6-month spend trend** (USD, amortised):");
        sb.AppendLine();
        sb.AppendLine("| Month | Spend | MoM |");
        sb.AppendLine("| --- | ---: | ---: |");
        decimal? prev = null;
        foreach (var (mon, cost) in trend)
        {
            string mom = prev is { } p && p > 0 ? $"{(cost - p) / p * 100m:+0.0;-0.0;0.0}%" : "—";
            sb.AppendLine($"| {mon} | {Cur} {cost:N0} | {mom} |");
            prev = cost;
        }
        if (trend.Count >= 2)
        {
            decimal first = trend[0].Cost, last = trend[^1].Cost;
            decimal change = first > 0 ? (last - first) / first * 100m : 0;
            sb.AppendLine();
            sb.AppendLine($"Net change over the window: **{change:+0.0;-0.0;0.0}%** ({Cur} {first:N0} → {Cur} {last:N0}).");
        }
        return sb.ToString();
    }

    private string Top(FinOpsAnalytics.Dimension dim, string label)
    {
        var (y, mo) = _analytics.LatestFullMonth();
        var rows = _analytics.TopBy(dim, 5, (y, mo));
        var sb = new StringBuilder();
        sb.AppendLine($"**Top {rows.Count} {label} by cost** — {y:0000}-{mo:00} (USD):");
        sb.AppendLine();
        sb.AppendLine($"| {Capitalize(label).TrimEnd('s')} | Spend | Share |");
        sb.AppendLine("| --- | ---: | ---: |");
        foreach (var b in rows)
            sb.AppendLine($"| {b.Name} | {Cur} {b.Cost:N0} | {b.Share * 100m:N1}% |");
        sb.AppendLine();
        var lead = rows.FirstOrDefault();
        if (lead is not null)
            sb.AppendLine($"**{lead.Name}** leads at {lead.Share * 100m:N0}% of spend — the first place to look for savings.");
        return sb.ToString();
    }

    private string Anomalies()
    {
        var anomalies = _analytics.DetectAnomalies();
        if (anomalies.Count == 0)
            return "No month-over-month anomalies above the 25% threshold in the latest month. Spend looks stable. ✅";

        var sb = new StringBuilder();
        sb.AppendLine($"**{anomalies.Count} cost anomal{(anomalies.Count == 1 ? "y" : "ies")} detected** (latest month, MoM ≥ 25%):");
        sb.AppendLine();
        sb.AppendLine("| Dimension | Prev | Current | Change | Severity |");
        sb.AppendLine("| --- | ---: | ---: | ---: | :--- |");
        foreach (var a in anomalies.Take(8))
        {
            string sev = a.Severity switch { "critical" => "🔴 critical", "warning" => "🟠 warning", _ => "🟡 info" };
            sb.AppendLine($"| {a.Dimension} | {Cur} {a.PreviousCost:N0} | {Cur} {a.CurrentCost:N0} | {a.ChangePercent:+0.0;-0.0}% | {sev} |");
        }
        var top = anomalies[0];
        sb.AppendLine();
        sb.AppendLine($"Biggest mover: **{top.Dimension}** ({top.ChangePercent:+0.0;-0.0}%). Confirm whether it's an intended new workload or a regression (e.g. runaway query, missing autoscale floor).");
        return sb.ToString();
    }

    private string Coverage()
    {
        var c = _analytics.Coverage();
        var sb = new StringBuilder();
        sb.AppendLine($"**Commitment coverage: {c.CoveragePercent:N0}%** (latest full month).");
        sb.AppendLine();
        sb.AppendLine("| Metric | Amount |");
        sb.AppendLine("| --- | ---: |");
        sb.AppendLine($"| Total spend | {Cur} {c.TotalCost:N0} |");
        sb.AppendLine($"| Covered (reservation/SP) | {Cur} {c.CommittedCost:N0} |");
        sb.AppendLine($"| On-demand (uncovered) | {Cur} {c.OnDemandCost:N0} |");
        sb.AppendLine();
        if (c.CoveragePercent < 75m)
            sb.AppendLine($"Coverage is below the typical 75-85% target. ~{Cur} {c.OnDemandCost * 0.5m:N0}/mo of on-demand looks steady-state and could move to a 1-year savings plan (~30% rate cut on eligible compute).");
        else
            sb.AppendLine("Coverage is healthy. Keep an eye on commitment utilisation so you don't over-commit on declining workloads.");
        return sb.ToString();
    }

    private string Recommendations()
    {
        var recs = _analytics.Recommendations();
        decimal total = recs.Sum(r => r.EstimatedMonthlySavings);
        var sb = new StringBuilder();
        sb.AppendLine($"**Top savings opportunities — est. {Cur} {total:N0}/mo** ({Cur} {total * 12m:N0}/yr):");
        sb.AppendLine();
        foreach (var r in recs)
        {
            sb.AppendLine($"**{r.Title}** · _{r.Category}, {r.Effort} effort_ — est. **{Cur} {r.EstimatedMonthlySavings:N0}/mo**");
            sb.AppendLine($"  {r.Detail}");
            sb.AppendLine();
        }
        sb.AppendLine("_Estimates are directional; validate utilisation before purchasing commitments._");
        return sb.ToString();
    }

    private string Showback(string message)
    {
        var dim = Has(message, "team") ? FinOpsAnalytics.Dimension.Team
            : Has(message, "environment", "prod") ? FinOpsAnalytics.Dimension.Environment
            : Has(message, "subscription") ? FinOpsAnalytics.Dimension.Subscription
            : FinOpsAnalytics.Dimension.CostCenter;
        string label = dim switch
        {
            FinOpsAnalytics.Dimension.Team => "team",
            FinOpsAnalytics.Dimension.Environment => "environment",
            FinOpsAnalytics.Dimension.Subscription => "subscription",
            _ => "cost center",
        };
        var (y, mo) = _analytics.LatestFullMonth();
        var rows = _analytics.TopBy(dim, 20, (y, mo));
        var sb = new StringBuilder();
        sb.AppendLine($"**Showback by {label}** — {y:0000}-{mo:00} (USD):");
        sb.AppendLine();
        sb.AppendLine($"| {Capitalize(label)} | Spend | Share |");
        sb.AppendLine("| --- | ---: | ---: |");
        foreach (var b in rows)
            sb.AppendLine($"| {b.Name} | {Cur} {b.Cost:N0} | {b.Share * 100m:N1}% |");
        return sb.ToString();
    }

    private string Forecast()
    {
        var (next, last, slope) = _analytics.Forecast();
        string dir = slope > 0 ? "rising" : slope < 0 ? "falling" : "flat";
        var sb = new StringBuilder();
        sb.AppendLine($"**Next-month forecast: {Cur} {next:N0}** (run-rate from the last 3 months).");
        sb.AppendLine();
        sb.AppendLine($"- Latest month: {Cur} {last:N0}");
        sb.AppendLine($"- Monthly trend: {dir} (~{Cur} {Math.Abs(slope):N0}/mo)");
        sb.AppendLine();
        sb.AppendLine("_Simple linear run-rate; doesn't model planned launches or commitment purchases. Treat as a directional baseline._");
        return sb.ToString();
    }

    private string Unknown() =>
        "I'm focused on **cloud FinOps** — spend, trends, cost drivers, anomalies, commitment coverage, " +
        "showback and savings. I couldn't map that to a FinOps query. Try *\"top services by cost\"*, " +
        "*\"any anomalies?\"*, or *\"where can we save money?\"* — or type **help** for the full list.";

    private static string Capitalize(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s[1..];
}
