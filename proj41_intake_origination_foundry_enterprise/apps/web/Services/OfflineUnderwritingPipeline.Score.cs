using System.Text;
using System.Text.RegularExpressions;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// Hazard / appetite classification, routing, narrative builders and small string utilities
/// for the offline underwriting pipeline.
/// </summary>
public sealed partial class OfflineUnderwritingPipeline
{
    // =======================================================  hazard + appetite  ===

    private static string HazardClass(string industry, string sic)
    {
        var i = (industry + " " + sic).ToLowerInvariant();
        if (Regex.IsMatch(i, @"chemical|oil|gas|mining|construction|trucking|haulage|metal|plastics|food process|energy|cannabis|aviation|marine|pharmaceutic")) return "High";
        if (Regex.IsMatch(i, @"manufactur|warehous|logistics|transport|healthcare|restaurant|hospitality|agriculture|automotive|textile|real estate|education")) return "Medium";
        if (Regex.IsMatch(i, @"technology|saas|software|financial|legal|professional|consult|information|services")) return "Low";
        return "Medium";
    }

    private static bool CatExposed(Insured ins)
    {
        var h = (ins.Headquarters + " " + ins.Country).ToLowerInvariant();
        return Regex.IsMatch(h, @"florida|texas|louisiana|gulf|coast|miami|houston|new orleans|california|wildfire|tampa|puerto rico|caribbean|queensland|philippines|japan");
    }

    private static bool NeedsRegulatory(Insured ins)
    {
        var i = ins.Industry.ToLowerInvariant();
        return i.Contains("health") || i.Contains("financial") || i.Contains("bank") || i.Contains("pharma") || i.Contains("legal") || i.Contains("education");
    }

    private static bool OutOfAppetite(ExtractedRecords r, out string reason)
    {
        reason = "";
        var i = (r.Insured.Industry + " " + r.Submission.CoverageType + " " + r.RawText).ToLowerInvariant();
        if (Regex.IsMatch(i, @"fireworks|asbestos abatement|coal mining|payday lend|adult entertainment|munitions|explosives manufactur"))
        { reason = "Prohibited class of business (carrier appetite guide)"; return true; }
        return false;
    }

    private static bool LooksLikeSpam(ExtractedRecords r)
    {
        var blob = $"{r.Producer.Email} {r.Insured.CompanyName} {r.RawText}".ToLowerInvariant();
        return Regex.IsMatch(blob, @"\b(seo|buy followers|crypto|giveaway|loan offer|marketing services|viagra|casino)\b");
    }

    private static (string queue, string desk) RouteDesk(string lob, string appetite, Insured ins)
    {
        if (appetite is "Decline" or "Out of Appetite") return ("Declinature", "Submissions Triage");
        var big = (ins.TotalInsurableValue ?? 0) >= 100_000_000m || (ins.EmployeeCount ?? 0) >= 1000;
        var queue = lob switch
        {
            "Cyber" => "Cyber Underwriting",
            "Professional Liability" or "Management Liability" => "Financial Lines",
            "Workers Comp" => "Workers' Comp",
            "Marine" => "Marine & Cargo",
            "Commercial Auto" => "Auto & Fleet",
            "Property" or "Multi-line" => big ? "Major Property" : "Commercial Property",
            "General Liability" => "Casualty",
            _ => "Commercial Property"
        };
        var desk = big ? $"{queue} - Major Accounts" : $"{queue} - SME/Mid";
        return (queue, desk);
    }

    // =======================================================  narrative builders  ===

    private static string BuildOverview(Insured ins, RiskSubmission sub, string hazard)
    {
        var name = string.IsNullOrWhiteSpace(ins.CompanyName) ? "The applicant" : ins.CompanyName;
        var bits = new List<string> { $"{name} is a {(string.IsNullOrWhiteSpace(ins.Industry) ? "commercial" : ins.Industry)} risk" };
        if (ins.EmployeeCount is int ec) bits.Add($"with ~{ec:N0} employees");
        if (ins.LocationCount > 1) bits.Add($"across {ins.LocationCount} locations");
        if (!string.IsNullOrWhiteSpace(ins.Headquarters)) bits.Add($"based in {ins.Headquarters}");
        var s = string.Join(" ", bits) + ".";
        s += $" The submission is for {sub.CoverageType} ({sub.SubmissionType}).";
        if (sub.RequestedLimit is decimal lim) s += $" Requested limit {Money(lim)}.";
        if (ins.TotalInsurableValue is decimal tiv) s += $" Total insurable value ~{Money(tiv)}.";
        s += $" Hazard class is assessed as {hazard}.";
        return s;
    }

    private static string BuildTriageRationale(string appetite, string rec, int fit, int risk, string hazard, Insured ins, RiskSubmission sub, List<string> referrals)
    {
        var s = $"Appetite: {appetite} -> recommended action {rec}. Fit score {fit}/100, risk score {risk}/100 ({hazard} hazard).";
        if (referrals.Count > 0) s += $" Referral triggers: {string.Join("; ", referrals)}.";
        if (sub.IncumbentCarriers.Count > 0) s += $" Incumbent/competing markets: {string.Join(", ", sub.IncumbentCarriers)}.";
        s += rec switch
        {
            "Quote" => " Risk sits within desk authority; proceed to quote.",
            "Refer" => " Refer to a senior underwriter for authority/risk review before quoting.",
            "Decline" => " Outside appetite; issue a courteous declinature.",
            _ => ""
        };
        return s;
    }

    private static string BuildExecutiveSummary(Insured ins, RiskSubmission sub, AppetiteDecision triage, LeadResearch research, string overall, decimal? indicated)
    {
        var name = string.IsNullOrWhiteSpace(ins.CompanyName) ? "the applicant" : ins.CompanyName;
        var sb = new StringBuilder();
        sb.Append($"Recommendation: {overall}. ");
        if (triage.Declined)
        {
            sb.Append($"This submission for {name} falls outside current appetite ({string.Join("; ", triage.RiskFlags)}). ");
            sb.Append("A prompt, courteous declinature is recommended to preserve the broker relationship.");
            return sb.ToString();
        }
        sb.Append($"{name} presents a {HazardClass(ins.Industry, ins.SicDivision).ToLowerInvariant()}-hazard {sub.LineOfBusiness} risk ");
        sb.Append($"with a fit score of {triage.FitScore}/100 and a risk score of {triage.RiskScore}/100. ");
        if (indicated is decimal p) sb.Append($"Indicated annual premium is approximately {Money(p)}. ");
        var adverse = research.Signals.Count(s => s.Sentiment == "Adverse");
        sb.Append(adverse > 0
            ? $"{adverse} adverse exposure signal(s) require attention before binding. "
            : "No material adverse exposure signals were identified. ");
        sb.Append(overall switch
        {
            "Bind" => "The risk is within appetite and authority; proceed to bind on standard terms.",
            "Quote with conditions" => "Proceed to quote subject to the recommended conditions and information requirements.",
            "Refer" => "Refer to a senior underwriter for authority sign-off prior to quoting.",
            _ => "Decline."
        });
        return sb.ToString();
    }

    private static string BuildPricingRationale(RiskSubmission sub, Insured ins, decimal? indicated, AppetiteDecision triage)
    {
        if (triage.Declined) return "No pricing developed - submission declined / out of appetite.";
        var basis = ins.TotalInsurableValue ?? sub.RequestedLimit;
        var sb = new StringBuilder();
        if (indicated is decimal p)
        {
            sb.Append($"Indicated annual premium ~{Money(p)}, derived from a {sub.LineOfBusiness} rate-on-line applied to ");
            sb.Append(basis is decimal b ? $"the {(ins.TotalInsurableValue is not null ? "total insurable value" : "requested limit")} of {Money(b)}. " : "the available exposure basis. ");
        }
        else sb.Append("Insufficient exposure data to indicate premium; obtain TIV / limits to rate. ");
        sb.Append(triage.RiskScore >= 60
            ? "Loading applied for elevated hazard/risk score; "
            : "Base rating appropriate for the hazard class; ");
        sb.Append("subject to final terms, conditions and information requirements below.");
        return sb.ToString();
    }

    private static List<string> BuildQuestions(ExtractedRecords r, string hazard, bool priorLoss)
    {
        var q = new List<string>();
        if (priorLoss) q.Add("Provide 5-year currently-valued loss runs.");
        foreach (var m in r.MissingForUnderwriting) q.Add($"Confirm {m}.");
        if (IsPropertyLike(r.Submission.LineOfBusiness)) q.Add("Provide a statement of values (COPE) for all locations.");
        if (r.Submission.LineOfBusiness == "Cyber") q.Add("Complete the cyber controls questionnaire (MFA, EDR, backups).");
        if (hazard == "High") q.Add("Describe risk-management / safety programmes in place.");
        if (q.Count == 0) q.Add("Confirm coverage, limits and effective date to proceed to quote.");
        return q.Distinct().Take(6).ToList();
    }

    private static List<string> BuildNextActions(string overall, AppetiteDecision triage, ExtractedRecords r, LeadResearch research)
    {
        var a = new List<string>();
        switch (overall)
        {
            case "Bind":
                a.Add("Issue quote and bind subject to confirmation of effective date.");
                a.Add("Set up policy in PAS and schedule inspection if required.");
                break;
            case "Quote with conditions":
                a.Add($"Request outstanding information: {string.Join(", ", research.RecommendedQuestions.Take(3))}.");
                a.Add("Prepare quote with the recommended conditions/exclusions.");
                break;
            case "Refer":
                a.Add($"Refer to {triage.AssignedDesk} for authority sign-off.");
                a.Add("Provide exposure summary and pricing rationale to the referring underwriter.");
                break;
            default:
                a.Add("Send a courteous declinature to the broker citing appetite.");
                a.Add("Log the declinature reason for portfolio analytics.");
                break;
        }
        a.Add($"Acknowledge the broker ({(string.IsNullOrWhiteSpace(r.Producer.ContactName) ? "producer" : r.Producer.ContactName)}) within SLA ({triage.SlaHours}h).");
        return a;
    }

    // =======================================================  small utilities  ===

    private static string CleanCompany(string s)
    {
        s = Regex.Replace(s ?? "", @"\s+", " ").Trim().Trim('"', '\'', ',', '.', ';', ':', '-');
        // strip a leading relationship phrase the label patterns may include
        s = Regex.Replace(s, @"(?i)^(our client|the client|client|insured|applicant|named insured)[,:]?\s+", "").Trim();
        // drop trailing role/relationship phrases captured by the label patterns
        s = Regex.Replace(s, @"(?i)\b(is|are|seeking|requesting|would|wants|needs|looking|a |an ).*$", "").Trim();
        if (s.Length > 60) s = s[..60].Trim();
        return s;
    }

    private static bool IsPlausibleCompany(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length < 2) return false;
        if (Regex.IsMatch(s, @"^(hi|hello|dear|team|there|all|sir|madam|our client|the client|client|insured|applicant|named insured)$", RegexOptions.IgnoreCase)) return false;
        return Regex.IsMatch(s, "[A-Za-z]");
    }

    private static string GuessPersonName(string text)
    {
        var m = Regex.Match(text, @"(?im)(?:regards|thanks|sincerely|cheers|best)[,\s]+([A-Z][a-z]+(?:\s+[A-Z][a-z]+){0,2})");
        if (m.Success) return m.Groups[1].Value.Trim();
        return "";
    }

    private static string ExtractCity(string text)
    {
        var m = Regex.Match(text, @"(?i)\b(?:in|based in|located in|headquartered in|hq in)\s+([A-Z][a-zA-Z]+(?:\s[A-Z][a-zA-Z]+){0,2})");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static string ExtractCountry(string text, string lower)
    {
        (string kw, string label)[] map =
        {
            ("united states", "USA"), ("u.s.", "USA"), ("usa", "USA"), ("america", "USA"),
            ("united kingdom", "UK"), ("u.k.", "UK"), (" uk", "UK"), ("england", "UK"), ("london", "UK"),
            ("australia", "Australia"), ("sydney", "Australia"), ("melbourne", "Australia"),
            ("canada", "Canada"), ("toronto", "Canada"), ("singapore", "Singapore"),
            ("germany", "Germany"), ("france", "France"), ("japan", "Japan"), ("new zealand", "New Zealand"),
            ("ireland", "Ireland"), ("india", "India"),
        };
        foreach (var (kw, label) in map) if (lower.Contains(kw)) return label;
        return "";
    }

    private static string ExtractPhone(string text)
    {
        var m = Regex.Match(text, @"(\+?\d[\d\-\s().]{7,}\d)");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }

    private static string DomainOf(string email)
    {
        var e = email ?? "";
        var at = e.IndexOf('@');
        return at < 0 ? "" : e[(at + 1)..].Trim().ToLowerInvariant();
    }

    private static bool IsFreeMail(string domain) =>
        new[] { "gmail.com", "outlook.com", "hotmail.com", "yahoo.com", "icloud.com", "live.com", "aol.com", "proton.me" }
        .Contains(domain);

    private static bool IsBrokerDomain(string domain, string lower) =>
        Regex.IsMatch(domain, @"broker|insurance|risk|agency|marsh|aon|gallagher|lockton|hub|wtw")
        || lower.Contains("brokerage");

    private static string PrettifyDomain(string domain)
    {
        var host = domain.Split('.')[0];
        host = host.Replace('-', ' ').Replace('_', ' ');
        return Title(host);
    }

    private static string Title(string s) =>
        string.IsNullOrWhiteSpace(s) ? "" :
        string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Length <= 3 && w.ToUpperInvariant() is "E&O" or "D&O" or "GL" or "WC"
                ? w.ToUpperInvariant()
                : char.ToUpperInvariant(w[0]) + (w.Length > 1 ? w[1..] : "")));

    private static string Money(decimal v) =>
        v >= 1_000_000_000m ? $"${v / 1_000_000_000m:0.##}B" :
        v >= 1_000_000m ? $"${v / 1_000_000m:0.##}M" :
        v >= 1_000m ? $"${v / 1_000m:0.#}K" : $"${v:0}";

    private static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (var ch in s ?? "") h = h * 31 + ch;
            return Math.Abs(h);
        }
    }
}
