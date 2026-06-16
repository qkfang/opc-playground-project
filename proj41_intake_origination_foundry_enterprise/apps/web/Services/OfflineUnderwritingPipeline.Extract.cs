using System.Globalization;
using System.Text.RegularExpressions;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// Low-level extraction + scoring helpers for the offline underwriting pipeline.
/// All methods are pure/deterministic so cases are reproducible for the same input.
/// </summary>
public sealed partial class OfflineUnderwritingPipeline
{
    // =======================================================  text extraction  ===

    private static readonly string[] CompanySuffixes =
    {
        "inc", "inc.", "llc", "l.l.c", "ltd", "ltd.", "limited", "corp", "corp.", "corporation",
        "co", "co.", "company", "plc", "pty", "group", "holdings", "partners", "industries",
        "manufacturing", "logistics", "systems", "technologies", "solutions", "services", "gmbh", "ag", "sa"
    };

    private static string ExtractInsuredName(string text, string lower, SubmissionEmail email)
    {
        foreach (var label in new[] { "named insured", "insured", "applicant", "company", "client", "account", "risk" })
        {
            var m = Regex.Match(text, $@"(?im)^\s*{Regex.Escape(label)}\s*[:\-]\s*(.+)$");
            if (m.Success)
            {
                var v = CleanCompany(m.Groups[1].Value);
                if (IsPlausibleCompany(v)) return v;
            }
        }

        foreach (var pat in new[]
        {
            @"(?i)\b(?:our client|the client|insured|applicant|named insured)[,:]?\s+(?:is\s+)?([A-Z][\w&.\-' ]{2,60})",
            @"(?i)\b(?:submission|quote|coverage|insurance|policy)\s+for\s+(?:our client[,:]?\s+)?([A-Z][\w&.\-' ]{2,60})",
            @"(?i)\bon behalf of\s+([A-Z][\w&.\-' ]{2,60})"
        })
        {
            var m = Regex.Match(text, pat);
            if (m.Success)
            {
                var v = CleanCompany(m.Groups[1].Value);
                if (IsPlausibleCompany(v)) return v;
            }
        }

        foreach (var line in text.Split('\n'))
        {
            var toks = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < toks.Length; i++)
            {
                var t = toks[i].Trim().TrimEnd(',', '.', ';').ToLowerInvariant();
                if (CompanySuffixes.Contains(t) && i >= 1)
                {
                    var start = Math.Max(0, i - 3);
                    var cand = CleanCompany(string.Join(' ', toks[start..(i + 1)]));
                    if (IsPlausibleCompany(cand)) return cand;
                }
            }
        }

        var domain = DomainOf(email.From);
        if (!string.IsNullOrEmpty(domain) && !IsFreeMail(domain) && !IsBrokerDomain(domain, lower))
            return PrettifyDomain(domain);

        return "";
    }

    private static string ExtractBrokerage(SubmissionEmail email, string text, string lower)
    {
        foreach (var label in new[] { "brokerage", "broker", "agency", "producer" })
        {
            var m = Regex.Match(text, $@"(?im)^\s*{Regex.Escape(label)}\s*[:\-]\s*(.+)$");
            if (m.Success)
            {
                var v = CleanCompany(m.Groups[1].Value);
                if (IsPlausibleCompany(v)) return v;
            }
        }
        foreach (var b in new[] { "Marsh", "Aon", "Gallagher", "Willis Towers Watson", "Lockton", "HUB International", "WTW" })
            if (lower.Contains(b.ToLowerInvariant())) return b;

        var domain = DomainOf(email.From);
        if (!string.IsNullOrEmpty(domain) && !IsFreeMail(domain) && IsBrokerDomain(domain, lower))
            return PrettifyDomain(domain);
        return "";
    }

    private static string ExtractTitle(string lower)
    {
        foreach (var t in new[]
        {
            "account executive", "risk advisor", "account manager", "vice president",
            "managing director", "principal", "underwriting assistant", "broker", "producer", "agent"
        })
            if (lower.Contains(t)) return Title(t);
        return "";
    }

    private static string ExtractIndustry(string lower)
    {
        (string kw, string label)[] map =
        {
            ("manufactur", "Manufacturing"), ("warehouse", "Warehousing & Distribution"), ("logistics", "Logistics & Transport"),
            ("trucking", "Trucking & Haulage"), ("construction", "Construction"), ("contractor", "Construction"),
            ("restaurant", "Restaurants & Hospitality"), ("hospitality", "Hospitality"), ("hotel", "Hospitality"),
            ("ecommerce", "E-commerce Retail"), ("e-commerce", "E-commerce Retail"), ("retail", "Retail"),
            ("hospital", "Healthcare"), ("clinic", "Healthcare"), ("medical", "Healthcare"), ("health", "Healthcare"),
            ("software", "Technology / SaaS"), ("saas", "Technology / SaaS"), ("fintech", "Financial Technology"),
            ("bank", "Financial Services"), ("financial", "Financial Services"),
            ("law firm", "Legal Services"), ("legal", "Legal Services"), ("accounting", "Professional Services"),
            ("consult", "Professional Services"), ("engineering", "Engineering Services"), ("architect", "Architecture & Engineering"),
            ("agricultur", "Agriculture"), ("farm", "Agriculture"), ("food process", "Food Processing"),
            ("chemical", "Chemicals"), ("oil", "Energy / Oil & Gas"), ("gas", "Energy / Oil & Gas"), ("mining", "Mining"),
            ("real estate", "Real Estate"), ("property management", "Real Estate"), ("school", "Education"),
            ("university", "Education"), ("education", "Education"),
            ("marine", "Marine / Shipping"), ("aviation", "Aviation"), ("pharma", "Pharmaceuticals"),
            ("biotech", "Biotechnology"), ("automotive", "Automotive"), ("metal", "Metal Fabrication"),
            ("plastic", "Plastics Manufacturing"), ("textile", "Textiles"), ("winery", "Food & Beverage"),
            ("brewery", "Food & Beverage"), ("cannabis", "Cannabis"), ("staffing", "Staffing & PEO"),
            ("transport", "Transportation"),
        };
        foreach (var (kw, label) in map) if (lower.Contains(kw)) return label;
        return "";
    }

    private static string SicDivisionFor(string industry, string lower)
    {
        var i = industry.ToLowerInvariant();
        if (i.Contains("manufactur") || i.Contains("fabrication") || i.Contains("plastics") || i.Contains("textile") || i.Contains("food process")) return "Manufacturing";
        if (i.Contains("construction")) return "Construction";
        if (i.Contains("retail") || i.Contains("e-commerce")) return "Retail Trade";
        if (i.Contains("logistics") || i.Contains("transport") || i.Contains("trucking") || i.Contains("marine") || i.Contains("aviation")) return "Transportation";
        if (i.Contains("healthcare")) return "Health Services";
        if (i.Contains("technology") || i.Contains("saas")) return "Information";
        if (i.Contains("financial")) return "Finance & Insurance";
        if (i.Contains("services") || i.Contains("legal") || i.Contains("professional")) return "Services";
        if (i.Contains("agriculture") || i.Contains("mining") || i.Contains("energy")) return "Resources";
        if (i.Contains("real estate")) return "Real Estate";
        if (i.Contains("education")) return "Education";
        return "General Commercial";
    }

    private static string ExtractLineOfBusiness(string lower)
    {
        if (Regex.IsMatch(lower, @"cyber|data breach|ransomware|privacy liability")) return "Cyber";
        if (Regex.IsMatch(lower, @"professional liability|errors? and omissions?|e&o|malpractice")) return "Professional Liability";
        if (Regex.IsMatch(lower, @"workers'? comp|workers compensation")) return "Workers Comp";
        if (Regex.IsMatch(lower, @"general liability|public liability|premises liability")) return "General Liability";
        if (Regex.IsMatch(lower, @"directors? and officers?|d&o|management liability")) return "Management Liability";
        if (Regex.IsMatch(lower, @"marine|cargo|hull|inland marine")) return "Marine";
        if (Regex.IsMatch(lower, @"fleet|commercial auto|motor")) return "Commercial Auto";
        if (Regex.IsMatch(lower, @"package|bop|business owners|multi-?line|multiline")) return "Multi-line";
        if (Regex.IsMatch(lower, @"commercial property|property insurance|building|fire|business interruption|tiv")) return "Property";
        return "Property";
    }

    private static string CoverageFor(string lob, string lower) => lob switch
    {
        "Property" => "Commercial Property (building, contents, business interruption)",
        "General Liability" => "Commercial General Liability (per-occurrence + aggregate)",
        "Cyber" => "Cyber & Privacy Liability (1st & 3rd party)",
        "Professional Liability" => "Professional Liability / E&O (claims-made)",
        "Workers Comp" => "Workers' Compensation & Employers' Liability",
        "Marine" => "Marine / Cargo",
        "Commercial Auto" => "Commercial Auto / Fleet",
        "Management Liability" => "Directors & Officers Liability",
        "Multi-line" => "Package policy (property + liability)",
        _ => lob
    };

    private static bool IsPropertyLike(string lob) => lob is "Property" or "Multi-line" or "Marine";

    private static List<string> ExtractCarriers(string lower)
    {
        var found = new List<string>();
        foreach (var c in new[] { "chubb", "aig", "travelers", "hartford", "liberty mutual", "zurich", "cna", "axa", "allianz", "berkshire", "nationwide", "qbe" })
            if (lower.Contains(c)) found.Add(Title(c));
        return found.Distinct().ToList();
    }

    // =======================================================  numeric extraction  ===

    private static int? ExtractEmployees(string lower)
    {
        var m = Regex.Match(lower, @"([\d,]{1,7})\s*(?:\+|plus)?\s*(?:employees|staff|workers|fte|headcount|people)");
        if (m.Success && int.TryParse(m.Groups[1].Value.Replace(",", ""), out var n)) return n;
        m = Regex.Match(lower, @"(?:employees|staff|headcount|workforce)\D{0,6}([\d,]{1,7})");
        if (m.Success && int.TryParse(m.Groups[1].Value.Replace(",", ""), out n)) return n;
        return null;
    }

    private static int ExtractLocations(string lower)
    {
        var m = Regex.Match(lower, @"([\d,]{1,4})\s*(?:locations|sites|branches|stores|facilities|premises|outlets)");
        if (m.Success && int.TryParse(m.Groups[1].Value.Replace(",", ""), out var n)) return Math.Max(1, n);
        return 1;
    }

    private static int ExtractYears(string lower)
    {
        var m = Regex.Match(lower, @"(?:established|founded|since|operating since)\D{0,6}([12]\d{3})");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var y)) return Math.Max(0, DateTime.UtcNow.Year - y);
        m = Regex.Match(lower, @"([\d]{1,3})\s*(?:\+|plus)?\s*years? (?:in business|of operation|trading|operating)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out var v)) return v;
        return 0;
    }

    // Matches a money token: requires a $ sign OR a magnitude unit (k/m/bn/...).
    // Bare integers (years like 2026, counts, street numbers) are intentionally NOT money.
    private static readonly Regex MoneyToken = new(
        @"\$\s*(\d[\d,]*(?:\.\d+)?)\s*(k|m|bn|b|million|billion|thousand)?|\b(\d[\d,]*(?:\.\d+)?)\s*(k|m|bn|b|million|billion|thousand)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static decimal? ExtractMoney(string lower, params string[] nearKeywords)
    {
        decimal? best = null;
        int bestDist = int.MaxValue;
        foreach (var kw in nearKeywords)
        {
            foreach (Match km in Regex.Matches(lower, Regex.Escape(kw)))
            {
                int kwEnd = km.Index + kw.Length;
                foreach (Match mm in MoneyToken.Matches(lower))
                {
                    var (numStr, unit) = MoneyParts(mm);
                    var val = ParseAmount(numStr, unit);
                    if (val is not > 0) continue;
                    int dist = mm.Index >= kwEnd ? mm.Index - kwEnd : km.Index - (mm.Index + mm.Length);
                    if (dist < 0) dist = 0;
                    if (dist > 30) continue;            // must be adjacent to the keyword
                    // Prefer the closest; on a tie prefer the larger magnitude (headline figure).
                    if (dist < bestDist || (dist == bestDist && val > best))
                    { bestDist = dist; best = val; }
                }
            }
        }
        return best;
    }

    private static (string num, string unit) MoneyParts(Match m)
    {
        if (m.Groups[1].Success) return (m.Groups[1].Value, m.Groups[2].Value);
        return (m.Groups[3].Value, m.Groups[4].Value);
    }


    private static decimal? ParseAmount(string num, string unit)
    {
        if (!decimal.TryParse(num.Replace(",", ""), NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return null;
        switch (unit.Trim().ToLowerInvariant())
        {
            case "k": case "thousand": v *= 1_000m; break;
            case "m": case "million": v *= 1_000_000m; break;
            case "b": case "bn": case "billion": v *= 1_000_000_000m; break;
        }
        return v;
    }

    private static DateTimeOffset? ExtractEffectiveDate(string lower)
    {
        var m = Regex.Match(lower, @"(?:effective|inception|incept|bind by|start)\D{0,12}(\d{1,2}[/\-]\d{1,2}[/\-]\d{2,4})");
        if (m.Success && DateTimeOffset.TryParse(m.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d)) return d;
        if (Regex.IsMatch(lower, @"\b(asap|urgent|immediate|expiring|expires)\b")) return DateTimeOffset.UtcNow.AddDays(14);
        return null;
    }

    private static decimal? EstimatePremium(RiskSubmission sub, decimal? tiv)
    {
        decimal rate = sub.LineOfBusiness switch
        {
            "Property" => 0.0045m,
            "Cyber" => 0.012m,
            "Professional Liability" => 0.010m,
            "General Liability" => 0.006m,
            "Workers Comp" => 0.020m,
            "Management Liability" => 0.009m,
            "Marine" => 0.005m,
            "Commercial Auto" => 0.015m,
            "Multi-line" => 0.007m,
            _ => 0.006m
        };
        var basis = tiv ?? sub.RequestedLimit;
        if (basis is null or <= 0) return null;
        var premium = Math.Round(basis.Value * rate, 0);
        return Math.Max(2_500m, premium);
    }
}
