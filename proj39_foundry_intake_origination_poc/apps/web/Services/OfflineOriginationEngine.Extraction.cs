using System.Diagnostics;
using System.Text.RegularExpressions;
using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services;

/// <summary>
/// Deterministic, dependency-free implementation of the full intake &amp; origination pipeline.
/// Always available (no Azure/Foundry required) so the POC is demonstrable anywhere and so the
/// Foundry engine has a reliable fallback.
///
/// Stages: 1) Extraction -> Account/Lead/Opportunity, 2) Triage (transparent scoring),
/// 3) Lead research + demand signals, 4) Origination report/study.
/// Split across two files via partial class. This file: pipeline + extraction.
/// </summary>
public sealed partial class OfflineOriginationEngine : IOriginationEngine
{
    public string Name => "offline";

    public Task<OriginationCase> ProcessAsync(InboundEmail email, CancellationToken ct = default)
    {
        var c = new OriginationCase { Email = email, Engine = Name, Status = "running" };

        c.Extraction = TimeStep(c, "Extraction", "extract", () => Extract(email),
            r => $"Extracted Account '{r.Account.Name}', Lead '{r.Lead.FullName}', Opportunity '{r.Opportunity.Name}' (confidence {r.Confidence:P0}).");

        c.Triage = TimeStep(c, "Triage", "classify", () => Triage(email, c.Extraction),
            r => $"Classified {r.Classification} (score {r.Score}/100) -> {r.RoutedTo}, SLA {r.SlaTarget}.");

        c.Research = TimeStep(c, "LeadResearch", "research", () => Research(c.Extraction, c.Triage),
            r => $"Captured {r.DemandSignals.Count} demand signal(s); {r.RecommendedActions.Count} action(s).");

        c.Report = TimeStep(c, "Report", "report", () => BuildReport(c),
            r => $"Generated origination study '{r.Title}' — disposition: {r.Disposition}.");

        c.Status = "completed";
        return Task.FromResult(c);
    }

    private static T TimeStep<T>(OriginationCase c, string agent, string step, Func<T> work, Func<T, string> summarise)
    {
        var sw = Stopwatch.StartNew();
        var result = work();
        sw.Stop();
        c.AgentSteps.Add(new AgentStepLog { Agent = agent, Step = step, Engine = "offline", DurationMs = (int)sw.ElapsedMilliseconds, Summary = summarise(result) });
        return result;
    }

    // ===================================================== 1. EXTRACTION =====================================================

    public static ExtractionResult Extract(InboundEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}";
        var res = new ExtractionResult();

        var company = GuessCompany(email);
        res.Account.Name = company.name;
        res.Account.Domain = company.domain;
        res.Account.Website = company.domain is null ? null : $"www.{company.domain}";
        res.Account.Industry = GuessIndustry(text);
        res.Account.EmployeeBand = ExtractEmployeeBand(text);
        res.Account.AnnualRevenueBand = ExtractRevenueBand(text);
        res.Account.Country = text.Contains("Australia", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, @"\b(WA|QLD|NSW|VIC|Perth|Sydney|Melbourne|Brisbane|Newcastle)\b") ? "Australia" : null;

        res.Lead.FullName = string.IsNullOrWhiteSpace(email.FromName) ? GuessNameFromSignature(email.Body) : email.FromName;
        res.Lead.Email = email.From;
        res.Lead.AccountName = res.Account.Name;
        res.Lead.Title = ExtractTitle(email.Body);
        res.Lead.Phone = ExtractPhone(text);
        res.Lead.Seniority = ClassifySeniority(res.Lead.Title);
        res.Lead.IsDecisionMaker = Regex.IsMatch(text, @"\b(decision maker|budget owner|i'?m the budget|final call|i approve|i own the budget)\b", RegexOptions.IgnoreCase)
            || res.Lead.Seniority is "C-Level" or "VP";
        res.Lead.PreferredContactMethod = res.Lead.Phone is not null && text.Contains("call", StringComparison.OrdinalIgnoreCase) ? "Phone call" : "Email";

        res.Opportunity.Name = BuildOpportunityName(res.Account.Name, text);
        res.Opportunity.ProductInterest = GuessProductInterest(text);
        res.Opportunity.Summary = FirstMeaningfulSentence(email.Body);
        (res.Opportunity.EstimatedValue, res.Opportunity.Currency) = ExtractMoney(text);
        res.Opportunity.Timeline = ExtractTimeline(text);
        res.Opportunity.BudgetStatus = ExtractBudgetStatus(text);
        res.Opportunity.Drivers = ExtractDrivers(email.Body);
        res.Opportunity.Stage = "New";

        int present = 0, total = 8;
        void Check(bool ok, string field) { if (ok) present++; else res.MissingFields.Add(field); }
        Check(!string.IsNullOrWhiteSpace(res.Account.Name) && res.Account.Name != "Unknown", "Account.Name");
        Check(res.Account.Industry is not null, "Account.Industry");
        Check(res.Account.EmployeeBand is not null, "Account.EmployeeBand");
        Check(!string.IsNullOrWhiteSpace(res.Lead.FullName) && res.Lead.FullName != "Unknown Contact", "Lead.FullName");
        Check(res.Lead.Title is not null, "Lead.Title");
        Check(res.Opportunity.EstimatedValue is not null, "Opportunity.EstimatedValue");
        Check(res.Opportunity.Timeline is not null, "Opportunity.Timeline");
        Check(res.Opportunity.Drivers.Count > 0, "Opportunity.Drivers");
        res.Confidence = Math.Round((decimal)present / total, 2);
        return res;
    }

    private static readonly Dictionary<string, string[]> IndustryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Mining & Resources"] = new[] { "mining", "processing site", "resources", "ore", "production throughput" },
        ["Logistics & Transport"] = new[] { "logistics", "fleet", "telematics", "freight", "route optimis", "vehicle", "driver" },
        ["Healthcare"] = new[] { "hospital", "patient", "clinical", "dental", "clinic", "fhir", "hl7", "health network" },
        ["Financial Services"] = new[] { "bank", "insurance", "lending", "trading", "payments" },
        ["Retail"] = new[] { "retail", "store", "ecommerce", "point of sale", "merchandis" },
        ["Manufacturing"] = new[] { "manufactur", "factory", "assembly", "production line" },
        ["Education"] = new[] { "university", "school", "student", "campus" },
    };

    private static string? GuessIndustry(string text)
    {
        foreach (var (industry, kws) in IndustryKeywords)
            if (kws.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase))) return industry;
        return null;
    }

    private static (string name, string? domain) GuessCompany(InboundEmail email)
    {
        var domain = email.From.Contains('@') ? email.From.Split('@')[1].Trim().ToLowerInvariant() : null;
        var generic = new[] { "gmail.com", "outlook.com", "hotmail.com", "yahoo.com", "icloud.com", "proton.me" };
        if (domain is not null && generic.Contains(domain)) domain = null;

        var sig = Regex.Match(email.Body,
            @"(?<name>[A-Z][A-Za-z0-9&'\.\- ]{2,60}(?:Corporation|Corp|Company|Inc|Ltd|Pty|Group|Logistics|Health Network|Health|Network|Mining|Dental|Industries|Solutions|Technologies))\b");
        if (sig.Success)
        {
            var name = CleanCompanyName(sig.Groups["name"].Value);
            return (name, domain);
        }
        if (domain is not null)
        {
            var core = domain.Split('.')[0];
            var name = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Regex.Replace(core, @"[-_]+", " "));
            return (name, domain);
        }
        return ("Unknown", null);
    }

    /// <summary>Strip leading role/intro fragments like "I'm the VP of X at " or "..., " from a captured company name.</summary>
    private static string CleanCompanyName(string raw)
    {
        var name = raw.Trim();
        var atIdx = name.LastIndexOf(" at ", StringComparison.OrdinalIgnoreCase);
        if (atIdx >= 0) name = name[(atIdx + 4)..];
        name = Regex.Replace(name, @"^.*,\s*", "");
        return name.Trim();
    }

    private static string? ExtractEmployeeBand(string text)
    {
        var m = Regex.Match(text, @"(?<n>[\d,]{2,})\s*[-\s]?\s*\+?\s*(?:staff|employees|people|person|headcount)", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(text, @"(?:staff|employees|team) of\s*(?:about\s*)?(?<n>[\d,]{2,})", RegexOptions.IgnoreCase);
        if (!m.Success) m = Regex.Match(text, @"(?:about|around)\s*(?<n>[\d,]{3,})\s*(?:staff|employees|people)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups["n"].Value.Replace(",", ""), out var n))
            return BandFor(n, new[] { (50, "1-50"), (250, "51-250"), (1000, "251-1,000"), (5000, "1,001-5,000"), (10000, "5,001-10,000") }, "10,000+");
        return null;
    }

    private static string? ExtractRevenueBand(string text)
    {
        var m = Regex.Match(text, @"revenue[^.\n]*?(?<cur>AUD|USD|A\$|US\$|\$)?\s*\$?\s*(?<num>\d+(?:\.\d+)?)\s*(?<mag>k|m|b|million|billion)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        if (!decimal.TryParse(m.Groups["num"].Value, out var num)) return null;
        decimal mult = m.Groups["mag"].Value.ToLowerInvariant() switch { "k" => 1_000m, "m" or "million" => 1_000_000m, "b" or "billion" => 1_000_000_000m, _ => 1m };
        var v = num * mult;
        var cur = m.Groups["cur"].Value.ToUpperInvariant().Contains("US") ? "USD" : "AUD";
        string band = v switch { < 10_000_000m => "<$10M", < 50_000_000m => "$10M-$50M", < 200_000_000m => "$50M-$200M", < 1_000_000_000m => "$200M-$1B", _ => "$1B+" };
        return $"{cur} {band}";
    }

    private static string BandFor(int n, (int max, string label)[] bands, string overflow)
    {
        foreach (var (max, label) in bands) if (n <= max) return label;
        return overflow;
    }

    private static (decimal? value, string currency) ExtractMoney(string text)
    {
        // For deal/opportunity value, prefer an amount in BUDGET context and ignore amounts in REVENUE
        // context (company revenue is captured separately). Falls back to the largest non-revenue amount.
        decimal? best = null; string currency = "AUD";
        decimal? budgetVal = null; string budgetCur = "AUD";
        foreach (var (value, cur, ctx) in MoneyMatches(text))
        {
            if (ctx == "revenue") continue;              // never treat company revenue as deal value
            if (ctx == "budget" && (budgetVal is null || value > budgetVal)) { budgetVal = value; budgetCur = cur; }
            if (best is null || value > best) { best = value; currency = cur; }
        }
        return budgetVal is not null ? (budgetVal, budgetCur) : (best, currency);
    }

    /// <summary>All money amounts with a small context tag: "budget" | "revenue" | "other".</summary>
    private static IEnumerable<(decimal value, string currency, string context)> MoneyMatches(string text)
    {
        var rx = new Regex(@"(?<cur>AUD|USD|A\$|US\$)?\s*\$\s*(?<num>\d+(?:\.\d+)?)\s*(?<mag>k|m|b|million|billion|thousand)?|(?<cur2>AUD|USD)\s*\$?\s*(?<num2>\d+(?:\.\d+)?)\s*(?<mag2>k|m|b|million|billion)", RegexOptions.IgnoreCase);
        foreach (Match m in rx.Matches(text))
        {
            var numStr = m.Groups["num"].Success ? m.Groups["num"].Value : m.Groups["num2"].Value;
            var magStr = (m.Groups["mag"].Success ? m.Groups["mag"].Value : m.Groups["mag2"].Value).ToLowerInvariant();
            var curStr = (m.Groups["cur"].Success ? m.Groups["cur"].Value : m.Groups["cur2"].Value).ToUpperInvariant();
            if (!decimal.TryParse(numStr, out var num)) continue;
            decimal mult = magStr switch { "k" or "thousand" => 1_000m, "m" or "million" => 1_000_000m, "b" or "billion" => 1_000_000_000m, _ => 1m };
            var value = num * mult;
            if (value < 1000m) continue;                 // ignore tiny/no-magnitude $ values
            var cur = curStr.Contains("US") ? "USD" : "AUD";
            int s = Math.Max(0, m.Index - 40), e = Math.Min(text.Length, m.Index + m.Length + 20);
            var window = text[s..e].ToLowerInvariant();
            string ctx = Regex.IsMatch(window, @"revenue|turnover") ? "revenue"
                       : Regex.IsMatch(window, @"budget|approv|allocated|program|invest|deal|contract|range of") ? "budget"
                       : "other";
            yield return (value, cur, ctx);
        }
    }

    private static string? ExtractTimeline(string text)
    {
        foreach (var p in new[]
        {
            @"\bQ[1-4]\s*FY?\s*\d{2,4}\b", @"\bbefore\s+Q[1-4][^.\n]{0,10}", @"\bnext financial year\b", @"\bthis financial year\b",
            @"\bwithin\s+\d+\s+days\b", @"\bover two quarters\b", @"\bthis week\b", @"\blater this year\b", @"\bnext quarter\b", @"\bnext 60 days\b"
        })
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase);
            if (m.Success) return m.Value.Trim();
        }
        return null;
    }

    private static string? ExtractBudgetStatus(string text)
    {
        if (Regex.IsMatch(text, @"\b(budget approved|board approval|approved budget|allocated budget|have allocated|funding approved|budget owner)\b", RegexOptions.IgnoreCase)) return "Budget approved";
        if (Regex.IsMatch(text, @"\b(no firm budget|building the business case|no budget yet|exploring|business case)\b", RegexOptions.IgnoreCase)) return "Building business case";
        if (Regex.IsMatch(text, @"\b(small budget|big budget|few thousand|don'?t have a big)\b", RegexOptions.IgnoreCase)) return "Limited budget";
        return null;
    }

    private static List<string> ExtractDrivers(string body)
    {
        var drivers = new List<string>();
        foreach (var line in body.Split('\n'))
        {
            var t = line.Trim();
            if (Regex.IsMatch(t, @"^[-•\*]\s+") && t.Length > 6)
                drivers.Add(Regex.Replace(t, @"^[-•\*]\s+", "").Trim());
        }
        return drivers.Take(6).ToList();
    }

    private static string BuildOpportunityName(string account, string text)
    {
        var interest = GuessProductInterest(text);
        var acct = account == "Unknown" ? "Prospect" : account;
        return interest is null ? $"{acct} — Inbound enquiry" : $"{acct} — {interest}";
    }

    private static string? GuessProductInterest(string text)
    {
        foreach (var (kw, label) in new (string kw, string label)[]
        {
            ("telematics", "Fleet telematics & route optimisation"), ("route optimis", "Fleet telematics & route optimisation"),
            ("data modernis", "Data modernisation program"), ("patient data", "Healthcare data integration platform"),
            ("analytics", "Data & analytics platform"), ("data platform", "Data & analytics platform"),
            ("integration", "Data integration platform"), ("scheduling", "Scheduling & reminders product"),
            ("appointment", "Scheduling & reminders product"), ("reporting", "Reporting & dashboards"),
        })
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase)) return label;
        return null;
    }

    private static string? ExtractTitle(string body)
    {
        var m = Regex.Match(body, @"\b(VP|Vice President|Chief \w+ Officer|C[A-Z]O|Director(?: of [A-Za-z ]+?)?|Head of [A-Za-z ]+|Practice Manager|Procurement Lead|IT Manager)\b");
        return m.Success ? m.Value.Trim() : null;
    }

    private static string ClassifySeniority(string? title)
    {
        title ??= "";
        if (Regex.IsMatch(title, @"\b(Chief|C[A-Z]O|CEO|CIO|CTO|CFO)\b", RegexOptions.IgnoreCase)) return "C-Level";
        if (Regex.IsMatch(title, @"\b(VP|Vice President)\b", RegexOptions.IgnoreCase)) return "VP";
        if (title.Contains("Director", StringComparison.OrdinalIgnoreCase) || title.Contains("Head of", StringComparison.OrdinalIgnoreCase)) return "Director";
        if (title.Contains("Manager", StringComparison.OrdinalIgnoreCase) || title.Contains("Lead", StringComparison.OrdinalIgnoreCase)) return "Manager";
        return "Individual Contributor";
    }

    private static string? ExtractPhone(string text)
    {
        var m = Regex.Match(text, @"(\+?\d[\d\s\-]{7,}\d)");
        return m.Success ? m.Value.Trim() : null;
    }

    private static string GuessNameFromSignature(string body)
    {
        var lines = body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        for (int i = lines.Length - 1; i >= 0 && i >= lines.Length - 6; i--)
            if (Regex.IsMatch(lines[i], @"^[A-Z][a-z]+ [A-Z][a-z']+$")) return lines[i];
        return "Unknown Contact";
    }

    private static string FirstMeaningfulSentence(string body)
    {
        foreach (var raw in Regex.Split(body, @"(?<=[.!?])\s+"))
        {
            var s = raw.Trim();
            if (s.Length > 25 && !s.StartsWith("Hi", StringComparison.OrdinalIgnoreCase) && !s.StartsWith("Hello", StringComparison.OrdinalIgnoreCase))
                return s.Length > 220 ? s[..220] + "…" : s;
        }
        return body.Length > 220 ? body[..220] + "…" : body.Trim();
    }
}
