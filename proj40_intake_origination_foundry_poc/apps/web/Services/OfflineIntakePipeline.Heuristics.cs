using System.Text.RegularExpressions;
using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services;

/// <summary>Heuristic helpers for <see cref="OfflineIntakePipeline"/> (regex + keyword dictionaries).</summary>
public sealed partial class OfflineIntakePipeline
{
    // ---------------- Regex (source-generated) ----------------

    [GeneratedRegex(@"(?:\+?\d[\d\-\s().]{7,}\d)", RegexOptions.CultureInvariant)]
    private static partial Regex PhoneRegex();

    // Matches a company mention in a signature line, e.g. "Company: Contoso Group" or "at Contoso Ltd".
    [GeneratedRegex(@"(?:Company|Organisation|Organization|Employer)\s*[:\-]\s*([A-Z][A-Za-z0-9&.,'\- ]{2,60})", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyLabelRegex();

    // Matches "<role>, Acme Health\n" sign-off lines (company appears AFTER a comma following a role word).
    [GeneratedRegex(@"(?:Officer|Engineer|Engineering|Manager|Director|President|Architect|Scientist|Lead|Analyst|Consultant|Founder|CEO|CTO|CIO|CDO|COO|CFO|VP)\s*,\s*([A-Z][A-Za-z0-9&.'][A-Za-z0-9&.'\- ]{2,45}?)\s*(?:[\.,]|\r|\n|$)", RegexOptions.CultureInvariant)]
    private static partial Regex CompanySignoffRegex();

    // Matches an explicit company suffix anywhere, e.g. "Contoso Manufacturing Ltd", "GlobalBank Corp".
    [GeneratedRegex(@"\b([A-Z][A-Za-z0-9&.'\- ]{1,45}?(?:Inc|Ltd|LLC|GmbH|Group|Corp|Corporation|Bank|Holdings|Industries|Manufacturing|Health|Retail|Technologies|Systems|Solutions|Pty|plc|AG|SA))\b", RegexOptions.CultureInvariant)]
    private static partial Regex CompanySuffixRegex();

    // ---------------- String helpers ----------------

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(n => haystack.Contains(n, StringComparison.OrdinalIgnoreCase));

    private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max].TrimEnd() + "…");

    private static int StableSeed(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (var ch in s) h = h * 31 + ch;
            return Math.Abs(h == int.MinValue ? 0 : h);
        }
    }

    private static string DomainFromEmail(string email)
    {
        var at = email.IndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : "";
    }

    private static string GuessNameFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var parts = local.Split('.', '_', '-').Where(p => p.Length > 1).ToArray();
        return parts.Length == 0
            ? (local.Length > 0 ? char.ToUpperInvariant(local[0]) + local[1..] : local)
            : string.Join(' ', parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
    }

    private static readonly string[] PublicDomains =
        { "gmail.com", "outlook.com", "hotmail.com", "yahoo.com", "icloud.com", "live.com", "protonmail.com", "me.com", "aol.com" };

    private static string CompanyFromSignatureOrDomain(string text, string domain)
    {
        // 1) Explicit "Company: X" label wins.
        var label = CompanyLabelRegex().Match(text);
        if (label.Success && !string.IsNullOrWhiteSpace(label.Groups[1].Value))
            return TidyCompany(label.Groups[1].Value);

        // 2) A token ending in a known company suffix (e.g. "Meridian Health", "Contoso Manufacturing").
        foreach (Match m in CompanySuffixRegex().Matches(text))
        {
            var cand = TidyCompany(StripLeadingRolePhrase(m.Groups[1].Value));
            if (LooksLikeCompany(cand) && cand.Length <= 45) return cand;
        }

        // 3) "<role>, Acme Corp" sign-off (company AFTER the comma).
        foreach (Match m in CompanySignoffRegex().Matches(text))
        {
            var cand = TidyCompany(StripLeadingRolePhrase(m.Groups[1].Value));
            if (LooksLikeCompany(cand)) return cand;
        }

        // 4) Prettify the email domain (split hyphens, drop the TLD, title-case words).
        if (!string.IsNullOrWhiteSpace(domain) && !PublicDomains.Contains(domain))
            return PrettifyDomain(domain);

        return "Unknown";
    }

    private static readonly HashSet<string> CompanyStopWords = new(StringComparer.OrdinalIgnoreCase)
    { "the", "our", "your", "this", "a", "data", "engineering", "sales", "marketing", "team", "we", "i" };

    // Strip a leading role phrase that a greedy match may have captured, e.g.
    // "VP of Engineering at Contoso Manufacturing" -> "Contoso Manufacturing".
    private static string StripLeadingRolePhrase(string s)
    {
        // Cut everything up to and including the last " at " / " of " / ", " connector.
        var connectors = new[] { " at ", " of ", ", " };
        foreach (var conn in connectors)
        {
            int idx = s.LastIndexOf(conn, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0) s = s[(idx + conn.Length)..];
        }
        return s.Trim();
    }

    private static bool LooksLikeCompany(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 3) return false;
        if (CompanyStopWords.Contains(s.Trim())) return false;
        return char.IsUpper(s[0]);
    }

    private static readonly HashSet<string> GenericSubdomains = new(StringComparer.OrdinalIgnoreCase)
    { "digital", "mail", "info", "contact", "hello", "team", "www", "go", "my", "app", "portal", "get" };

    private static string PrettifyDomain(string domain)
    {
        // Drop the TLD, then skip a generic leading label (digital., mail., www.) when a better one follows.
        var labels = domain.Split('.').Where(l => l.Length > 0).ToList();
        if (labels.Count > 1) labels.RemoveAt(labels.Count - 1); // drop TLD
        // Also drop a country-style second-level like co/com/gov/org/net if present before TLD.
        if (labels.Count > 1 && labels[^1].Length <= 3 &&
            (labels[^1] is "co" or "com" or "gov" or "org" or "net" or "ac" or "edu"))
            labels.RemoveAt(labels.Count - 1);
        var core = labels.FirstOrDefault(l => !GenericSubdomains.Contains(l)) ?? labels.FirstOrDefault() ?? "";
        var words = core.Split('-', '_').Where(w => w.Length > 0)
            .Select(w => char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..] : ""));
        var name = string.Join(' ', words);
        return string.IsNullOrWhiteSpace(name) ? "Unknown" : name;
    }

    private static string TidyCompany(string s)
    {
        s = s.Trim().Trim('.', ',', ';');
        return s.Length > 60 ? s[..60] : s;
    }

    // ---------------- Lead helpers ----------------

    private static readonly (string token, string normalized)[] KnownTitles =
    {
        ("Chief Executive Officer", "Chief Executive Officer"), ("CEO", "CEO"),
        ("Chief Technology Officer", "Chief Technology Officer"), ("CTO", "CTO"),
        ("Chief Information Officer", "Chief Information Officer"), ("CIO", "CIO"),
        ("Chief Data Officer", "Chief Data Officer"), ("CDO", "CDO"),
        ("Chief Operating Officer", "Chief Operating Officer"), ("COO", "COO"),
        ("Chief Financial Officer", "Chief Financial Officer"), ("CFO", "CFO"),
        ("VP of Engineering", "VP of Engineering"), ("VP Engineering", "VP of Engineering"),
        ("Vice President", "Vice President"), ("Head of Data", "Head of Data"), ("Head of AI", "Head of AI"),
        ("Director of Engineering", "Director of Engineering"), ("Director", "Director"),
        ("Engineering Manager", "Engineering Manager"), ("Product Manager", "Product Manager"),
        ("Solutions Architect", "Solutions Architect"), ("Data Scientist", "Data Scientist"),
        ("Procurement Manager", "Procurement Manager"), ("IT Manager", "IT Manager"),
    };

    private static string ExtractTitle(string text)
    {
        foreach (var (token, normalized) in KnownTitles)
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(token)}\b", RegexOptions.IgnoreCase))
                return normalized;
        return "";
    }

    private static string SeniorityFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Unknown";
        if (title.StartsWith("Chief", StringComparison.OrdinalIgnoreCase) || title.Length <= 3) return "C-Level"; // CEO/CTO/etc
        if (title.Contains("VP", StringComparison.OrdinalIgnoreCase) || title.Contains("Vice President", StringComparison.OrdinalIgnoreCase) || title.StartsWith("Head", StringComparison.OrdinalIgnoreCase)) return "VP";
        if (title.Contains("Director", StringComparison.OrdinalIgnoreCase)) return "Director";
        if (title.Contains("Manager", StringComparison.OrdinalIgnoreCase)) return "Manager";
        return "IC";
    }

    // ---------------- Account helpers ----------------

    private static readonly Dictionary<string, string[]> IndustryKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Financial Services"] = new[] { "bank", "insurance", "fintech", "trading", "wealth", "capital", "payments", "lending" },
        ["Healthcare & Life Sciences"] = new[] { "hospital", "clinic", "health", "pharma", "biotech", "medical", "patient", "life sciences" },
        ["Retail & CPG"] = new[] { "retail", "ecommerce", "consumer goods", "store", "merchand", "fmcg", "cpg" },
        ["Manufacturing"] = new[] { "manufactur", "factory", "industrial", "supply chain", "plant", "assembly" },
        ["Energy & Utilities"] = new[] { "energy", "utility", "oil", "gas", "power grid", "renewable", "utilities" },
        ["Public Sector"] = new[] { "government", "ministry", "council", "public sector", "agency", "defence", "defense" },
        ["Telecommunications"] = new[] { "telecom", "carrier", "network operator", "5g", "broadband" },
        ["Technology"] = new[] { "software", "saas", "platform", "developer", "cloud", "data platform", "ai" },
    };

    private static string ClassifyIndustry(string text, string domain)
    {
        foreach (var (industry, kws) in IndustryKeywords)
            if (kws.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return industry;
        return "Technology";
    }

    private static readonly Dictionary<string, string> CountryRegions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Australia"] = "APAC", ["Singapore"] = "APAC", ["Japan"] = "APAC", ["India"] = "APAC", ["New Zealand"] = "APAC", ["Hong Kong"] = "APAC",
        ["United States"] = "AMER", ["USA"] = "AMER", ["Canada"] = "AMER", ["Brazil"] = "AMER", ["Mexico"] = "AMER",
        ["United Kingdom"] = "EMEA", ["UK"] = "EMEA", ["Germany"] = "EMEA", ["France"] = "EMEA", ["Netherlands"] = "EMEA", ["Spain"] = "EMEA", ["UAE"] = "EMEA", ["Ireland"] = "EMEA",
    };

    private static string ExtractCountry(string text)
    {
        foreach (var country in CountryRegions.Keys)
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(country)}\b", RegexOptions.IgnoreCase))
                return country.Length <= 3 ? country.ToUpperInvariant() : country;
        // City -> country fallback for a few well-known hubs.
        if (ContainsAny(text, "Sydney", "Melbourne", "Brisbane")) return "Australia";
        if (ContainsAny(text, "London", "Manchester")) return "United Kingdom";
        if (ContainsAny(text, "New York", "San Francisco", "Seattle", "Chicago")) return "United States";
        return "";
    }

    private static string RegionFromCountry(string country)
        => string.IsNullOrWhiteSpace(country) ? "" : (CountryRegions.TryGetValue(country, out var r) ? r : "Global");

    private static (string segment, string band) SegmentFromText(string text)
    {
        // Explicit employee counts.
        var m = Regex.Match(text, @"(\d[\d,\.]*)\s*(?:\+)?\s*(?:employees|staff|people|headcount|fte)", RegexOptions.IgnoreCase);
        if (m.Success && decimal.TryParse(m.Groups[1].Value.Replace(",", "").Replace(".", ""), out var n))
            return SegmentFromHeadcount((int)n);

        // Qualitative cues.
        if (ContainsAny(text, "global", "multinational", "fortune 500", "enterprise-wide", "worldwide", "group of companies"))
            return ("Enterprise", "5k-20k");
        if (ContainsAny(text, "startup", "small business", "sme", "boutique", "early stage"))
            return ("SMB", "10-200");
        if (ContainsAny(text, "mid-market", "growing", "scale-up", "regional"))
            return ("Mid-Market", "200-1k");
        return ("Enterprise", "1k-5k"); // default leans enterprise for an enterprise-intake POC
    }

    private static (string, string) SegmentFromHeadcount(int n) => n switch
    {
        >= 10000 => ("Strategic", "10k+"),
        >= 1000 => ("Enterprise", "1k-10k"),
        >= 200 => ("Mid-Market", "200-1k"),
        _ => ("SMB", "<200")
    };

    // ---------------- Opportunity helpers ----------------

    private static readonly (string kw, string product)[] ProductMap =
    {
        ("data platform", "Enterprise Data Platform"),
        ("analytics", "Advanced Analytics Suite"),
        ("machine learning", "AI & ML Platform"),
        ("artificial intelligence", "AI & ML Platform"),
        (" ai ", "AI & ML Platform"),
        ("copilot", "AI Copilot Solution"),
        ("agent", "Agentic AI Platform"),
        ("integration", "Integration & API Platform"),
        ("security", "Security & Governance Suite"),
        ("migration", "Cloud Migration Program"),
        ("observability", "Observability Platform"),
        ("crm", "Customer Engagement Platform"),
    };

    private static string ProductInterest(string text)
    {
        var padded = $" {text.ToLowerInvariant()} ";
        foreach (var (kw, product) in ProductMap)
            if (padded.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return product;
        return "Enterprise Platform (general enquiry)";
    }

    private static string FirstSentenceAbout(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Inbound enquiry received.";
        var clean = Regex.Replace(body.Replace("\r", " ").Replace("\n", " "), @"\s+", " ").Trim();
        var idx = clean.IndexOfAny(new[] { '.', '!', '?' });
        var sentence = idx > 20 ? clean[..(idx + 1)] : clean;
        return Truncate(sentence, 220);
    }

    private static string ExtractTimeline(string text)
    {
        if (ContainsAny(text, "asap", "urgent", "immediately", "this week")) return "immediate";
        if (ContainsAny(text, "this quarter", "this month", "next 30 days", "by end of quarter")) return "this quarter";
        if (ContainsAny(text, "next quarter", "h1", "first half")) return "next quarter";
        if (ContainsAny(text, "this year", "2026", "h2", "second half")) return "this year";
        if (ContainsAny(text, "next year", "2027", "long term")) return "next year";
        if (ContainsAny(text, "evaluating", "exploring", "researching", "no timeline")) return "evaluating";
        return "unspecified";
    }

    private static readonly string[] KnownCompetitors =
        { "AWS", "Amazon Web Services", "Google Cloud", "GCP", "Snowflake", "Databricks", "Salesforce", "Oracle", "SAP", "ServiceNow", "IBM", "Palantir" };

    private static List<string> DetectCompetitors(string text)
    {
        var found = new List<string>();
        foreach (var c in KnownCompetitors)
            if (Regex.IsMatch(text, $@"\b{Regex.Escape(c)}\b", RegexOptions.IgnoreCase))
            {
                var canonical = c switch
                {
                    "Amazon Web Services" => "AWS",
                    "GCP" => "Google Cloud",
                    _ => c
                };
                if (!found.Contains(canonical)) found.Add(canonical);
            }
        return found;
    }

    private static decimal EstimateArr(string segment, string text)
    {
        // Explicit money mention wins (e.g. "$250k", "$1.2M", "budget of 500,000").
        var m = Regex.Match(text, @"\$?\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?", RegexOptions.IgnoreCase);
        // Prefer a money-context match.
        var money = Regex.Match(text, @"(?:budget|deal|contract|value|spend|invest)[^\d$]{0,20}\$?\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?", RegexOptions.IgnoreCase);
        var use = money.Success ? money : (text.Contains('$') ? Regex.Match(text, @"\$\s*([\d][\d,\.]*)\s*(k|m|million|thousand)?", RegexOptions.IgnoreCase) : Match.Empty);
        if (use.Success && decimal.TryParse(use.Groups[1].Value.Replace(",", ""), out var amount) && amount > 0)
        {
            var unit = use.Groups[2].Value.ToLowerInvariant();
            amount = unit switch
            {
                "k" or "thousand" => amount * 1_000m,
                "m" or "million" => amount * 1_000_000m,
                _ => amount
            };
            if (amount >= 1_000m) return Math.Round(amount, 0);
        }

        // Otherwise estimate from segment (typical first-year ARR bands for an enterprise platform).
        return segment switch
        {
            "Strategic" => 750_000m,
            "Enterprise" => 320_000m,
            "Mid-Market" => 120_000m,
            "SMB" => 35_000m,
            _ => 150_000m
        };
    }

    // ---------------- Research helpers ----------------

    private static List<string> InitiativesFor(string industry) => industry switch
    {
        "Financial Services" => new() { "Real-time fraud detection", "Regulatory reporting automation", "Customer 360 & personalisation" },
        "Healthcare & Life Sciences" => new() { "Clinical data interoperability", "AI-assisted diagnostics", "Patient engagement modernisation" },
        "Retail & CPG" => new() { "Demand forecasting", "Personalised commerce", "Supply-chain visibility" },
        "Manufacturing" => new() { "Predictive maintenance", "Digital twin / IIoT", "Quality analytics" },
        "Energy & Utilities" => new() { "Grid optimisation", "Asset performance management", "Sustainability reporting" },
        "Public Sector" => new() { "Citizen service automation", "Data governance & transparency", "Fraud & risk analytics" },
        "Telecommunications" => new() { "Network analytics", "Churn prediction", "Customer experience automation" },
        _ => new() { "Cloud & data modernisation", "Generative AI adoption", "Operational analytics" }
    };
}
