using System.Globalization;
using System.Text.RegularExpressions;
using Proj45.RelayDesk.Web.Models;
using Proj45.RelayDesk.Web.Services.Mcp;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>Heuristic stage implementations (extract / triage / intent) for <see cref="OfflineEmailPipeline"/>.</summary>
public sealed partial class OfflineEmailPipeline
{
    // ---------------------------------------------------------- 1. Extract ---

    private static readonly Regex RefRx = new(@"\b(?:INV|ORD|ACC|CASE|REF|OPP|CM)-[A-Z0-9]{3,}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MoneyRx = new(@"\$\s?\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);

    private EmailExtraction Extract(IncomingEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}";
        var refs = RefRx.Matches(text).Select(m => m.Value.ToUpperInvariant()).Distinct().ToList();

        var entities = new List<string>();
        foreach (var m in MoneyRx.Matches(text).Select(mm => mm.Value.Trim()).Distinct()) entities.Add(m);
        foreach (Match m in Regex.Matches(email.Body, @"\b([A-Z][a-z]+(?:\s+[A-Z][a-z]+){1,3})\b"))
        {
            var v = m.Groups[1].Value.Trim();
            if (v.Length > 4 && !entities.Contains(v)) entities.Add(v);
            if (entities.Count >= 12) break;
        }

        var domain = ExtractDomain(email.From);
        var hints = new List<string>();
        if (!string.IsNullOrWhiteSpace(domain)) hints.Add(domain!);
        if (!string.IsNullOrWhiteSpace(email.FromName)) hints.Add(email.FromName);
        var company = GuessCompany(email.Body);
        if (company is not null) hints.Add(company);

        var conf = 0.55
            + (refs.Count > 0 ? 0.15 : 0)
            + (hints.Count >= 2 ? 0.15 : 0.05)
            + (email.Body.Length > 120 ? 0.1 : 0);
        conf = Math.Clamp(conf, 0.3, 0.97);

        return new EmailExtraction
        {
            From = email.From,
            FromName = email.FromName,
            Subject = email.Subject,
            Channel = email.Channel,
            Language = DetectLanguage(email.Body),
            Entities = entities.Take(10).ToList(),
            OrderRefs = refs,
            AccountHints = hints.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NormalizedBody = Normalize(email.Body),
            ExtractionConfidence = Math.Round(conf, 2)
        };
    }

    private static string Normalize(string body)
    {
        var t = Regex.Replace(body, @"\s+", " ").Trim();
        return t.Length <= 280 ? t : t[..280] + "…";
    }

    private static string DetectLanguage(string body)
    {
        var lower = body.ToLowerInvariant();
        if (Regex.IsMatch(lower, @"\b(bonjour|merci)\b")) return "fr";
        if (Regex.IsMatch(lower, @"\b(hola|gracias)\b")) return "es";
        return "en";
    }

    private static string? GuessCompany(string body)
    {
        var lines = body.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var line in lines.Reverse().Take(4))
        {
            var m = Regex.Match(line, @"\b([A-Z][A-Za-z&]+(?:\s+[A-Z][A-Za-z&]+){1,3}(?:\s+(?:Inc|Co\.?|Ltd|LLC|Logistics|Retail|Foods|Parts|Cloud))?)\b");
            if (m.Success && m.Groups[1].Value.Length > 5) return m.Groups[1].Value.Trim();
        }
        return null;
    }

    internal static string? ExtractDomain(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var at = email.IndexOf('@');
        return at >= 0 && at < email.Length - 1 ? email[(at + 1)..].Trim().ToLowerInvariant() : null;
    }

    // ------------------------------------------------------------ 2. Triage ---

    private TriageResult Triage(EmailExtraction x, IncomingEmail email)
    {
        var text = $"{x.Subject} {x.NormalizedBody}".ToLowerInvariant();
        var spam = SpamScore(text, email.From);
        if (spam >= 0.6)
        {
            return new TriageResult
            {
                Category = "Spam", SubCategory = "promotional/junk", Urgency = "P4", Sentiment = "Neutral",
                SpamRisk = Math.Round(spam, 2), RiskFlags = new() { "Spam/marketing markers", "Unrecognized sender" },
                SlaHours = 0, TriageConfidence = Math.Round(Math.Min(0.97, 0.7 + (spam - 0.6)), 2),
                Rationale = "Matches spam markers (excessive punctuation, reward/claim language, suspicious link)."
            };
        }

        var (category, sub) = Categorize(text);
        var sentiment = Sentiment(text);
        var urgency = Urgency(text, sentiment, category);
        var flags = new List<string>();
        if (sentiment is "Angry" or "Negative") flags.Add("Negative sentiment");
        if (Regex.IsMatch(text, @"\b(urgent|asap|immediately|today|critical|impacting|down|outage)\b")) flags.Add("Time-sensitive language");
        if (Regex.IsMatch(text, @"\b(third time|again|second time|still waiting|no response|nobody)\b")) flags.Add("Repeat contact / unresolved");
        if (Regex.IsMatch(text, @"\b(public review|escalate|management|cancel|leaving)\b")) flags.Add("Escalation / churn risk");

        var conf = 0.6 + (category != "General" ? 0.2 : 0) + (flags.Count > 0 ? 0.1 : 0);
        conf = Math.Clamp(conf, 0.4, 0.95);

        return new TriageResult
        {
            Category = category, SubCategory = sub, Urgency = urgency, Sentiment = sentiment,
            SpamRisk = Math.Round(spam, 2), RiskFlags = flags, SlaHours = SlaFor(urgency),
            TriageConfidence = Math.Round(conf, 2),
            Rationale = $"Classified as {category} from keyword signals; sentiment {sentiment}; urgency {urgency}."
        };
    }

    private static double SpamScore(string text, string from)
    {
        double s = 0;
        if (Regex.IsMatch(text, @"!{2,}")) s += 0.25;
        if (Regex.IsMatch(text, @"\b(free|won|winner|prize|gift card|claim|reward|click here|act fast|limited time)\b")) s += 0.35;
        if (Regex.IsMatch(text, @"https?://")) s += 0.15;
        if (Regex.IsMatch(text, @"\$\$\$")) s += 0.1;
        var domain = ExtractDomain(from) ?? "";
        if (domain.EndsWith(".biz") || domain.Contains("marketing") || domain.Contains("promo")) s += 0.25;
        return Math.Clamp(s, 0, 0.99);
    }

    private static (string category, string sub) Categorize(string text)
    {
        if (Regex.IsMatch(text, @"\b(invoice|overcharg|billing|refund|credit|charge|payment|wrong amount)"))
            return ("Billing", "invoice/charge dispute");
        if (Regex.IsMatch(text, @"(cancel|terminat|not renew|not to renew|non-renew|won'?t renew|close (my|our) account)"))
            return ("Cancellation", "cancel / non-renewal");
        if (Regex.IsMatch(text, @"\b(error|503|500|api|bug|not working|broken|outage|down|crash|failed|integration)"))
            return ("Technical Support", "production/integration issue");
        if (Regex.IsMatch(text, @"\b(pricing|seats?|expand|enterprise agreement|quote|upgrade|interested in|sales)"))
            return ("Sales", "expansion / new business");
        if (Regex.IsMatch(text, @"\b(disappoint|unacceptable|complaint|frustrat|angry|terrible|worst|review)"))
            return ("Complaint", "service complaint");
        return ("General", "general enquiry");
    }

    private static string Sentiment(string text)
    {
        if (Regex.IsMatch(text, @"\b(unacceptable|disappointed|furious|angry|terrible|worst|frustrat)\b")) return "Angry";
        if (Regex.IsMatch(text, @"\b(not happy|issue|problem|wrong|fail|concern|disappoint)\b")) return "Negative";
        if (Regex.IsMatch(text, @"\b(thanks|great|happy|appreciate|interested|excited|going really well)\b")) return "Positive";
        return "Neutral";
    }

    private static string Urgency(string text, string sentiment, string category)
    {
        if (Regex.IsMatch(text, @"\b(critical|outage|down|impacting|production|503|today|immediately)\b")) return "P1";
        if (sentiment == "Angry" || category is "Cancellation" or "Complaint") return "P2";
        if (Regex.IsMatch(text, @"\b(urgent|asap|soon|this week)\b")) return "P2";
        return "P3";
    }

    private static int SlaFor(string urgency) => urgency switch { "P1" => 2, "P2" => 8, "P3" => 24, _ => 72 };

    // ------------------------------------------------------------ 3. Intent ---

    private IntentDecision DecideIntent(EmailExtraction x, TriageResult triage)
    {
        if (triage.Category == "Spam")
            return new IntentDecision
            {
                Intent = "Spam / No action", IntentConfidence = Math.Round(triage.TriageConfidence, 2), IntentBand = "High",
                RequiresHuman = false, SuggestedQueue = "Junk", Rationale = "Triaged as spam; no customer intent to action."
            };

        var text = $"{x.Subject} {x.NormalizedBody}".ToLowerInvariant();
        var scores = new Dictionary<string, double>
        {
            ["Billing Dispute"] = Score(text, @"(overcharg|invoice|credit|refund|wrong amount|billing|charged)") + (triage.Category == "Billing" ? 0.3 : 0),
            ["Cancellation Request"] = Score(text, @"(cancel|terminat|not renew|not to renew|non-renew|won'?t renew|close (my|our) account)") + (triage.Category == "Cancellation" ? 0.3 : 0),
            ["Technical Issue"] = Score(text, @"(error|503|api|not working|broken|outage|down|failed|integration|bug)") + (triage.Category == "Technical Support" ? 0.3 : 0),
            ["Sales Enquiry"] = Score(text, @"(pricing|seats?|expand|enterprise agreement|quote|upgrade|interested)") + (triage.Category == "Sales" ? 0.3 : 0),
            ["Complaint Escalation"] = Score(text, @"(complaint|unacceptable|disappoint|escalate|review|management|frustrat)") + (triage.Category == "Complaint" ? 0.25 : 0),
            ["Information Request"] = Score(text, @"(question|where it stands|next step|let me know|following up|update)")
        };

        var ranked = scores.OrderByDescending(kv => kv.Value).ToList();
        var top = ranked[0];
        var second = ranked.Count > 1 ? ranked[1] : new KeyValuePair<string, double>("", 0);

        var total = Math.Max(0.0001, ranked.Take(3).Sum(r => Math.Max(0, r.Value)));
        var dominance = Math.Max(0, top.Value) / total;
        var confidence = Math.Clamp(0.35 + 0.5 * dominance + 0.15 * Math.Min(1, top.Value), 0.2, 0.97);
        if (top.Value <= 0.15) confidence = Math.Min(confidence, 0.45);

        var band = confidence >= 0.75 ? "High" : confidence >= Threshold ? "Medium" : "Low";
        var requiresHuman = confidence < Threshold
            || (second.Value > 0 && Math.Abs(top.Value - second.Value) < 0.12 && top.Value < 0.6);
        var humanReason = requiresHuman
            ? (confidence < Threshold
                ? $"Intent confidence {confidence:0.00} below threshold {Threshold:0.00}."
                : $"Ambiguous between '{top.Key}' and '{second.Key}' (close scores).")
            : "";

        return new IntentDecision
        {
            Intent = top.Value <= 0.15 ? "Unknown" : top.Key,
            IntentConfidence = Math.Round(confidence, 2),
            IntentBand = band,
            AlternativeIntents = ranked.Skip(1).Where(r => r.Value > 0).Take(2)
                .Select(r => new AlternativeIntent { Intent = r.Key, Confidence = Math.Round(Math.Clamp(r.Value, 0, 1), 2) }).ToList(),
            RequiresHuman = requiresHuman,
            HumanReason = humanReason,
            SuggestedQueue = requiresHuman ? "Human Review" : QueueFor(top.Key),
            Rationale = $"Top intent '{top.Key}' (raw {top.Value:0.00}); dominance {dominance:0.00}."
        };
    }

    private static double Score(string text, string pattern) =>
        Math.Min(0.6, Regex.Matches(text, pattern).Count * 0.2);

    private static string QueueFor(string intent) => intent switch
    {
        "Billing Dispute" => "Billing Operations",
        "Cancellation Request" => "Retention Desk",
        "Technical Issue" => "Tier-2 Support",
        "Sales Enquiry" => "Sales / Account Management",
        "Complaint Escalation" => "Customer Success",
        "Information Request" => "General Support",
        _ => "General Support"
    };

    // ---- shared small helpers used by the task/outcome partial ----
    internal static string Trunc(string s, int n) => string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s[..n] + "…");

    internal static string Money(decimal? v) => v is null ? "n/a" : "$" + v.Value.ToString("#,0", CultureInfo.InvariantCulture);

    internal static string GuessDisputedAmount(string body)
    {
        // Pick the largest $ figure mentioned as the disputed amount (demo heuristic).
        var amounts = Regex.Matches(body, @"\$\s?(\d[\d,]*(?:\.\d+)?)")
            .Select(m => decimal.TryParse(m.Groups[1].Value.Replace(",", ""), out var d) ? d : 0m)
            .Where(d => d > 0).ToList();
        if (amounts.Count == 0) return "0";
        if (amounts.Count >= 2)
        {
            var diff = Math.Abs(amounts.Max() - amounts.Min());
            if (diff > 0) return diff.ToString("0", CultureInfo.InvariantCulture);
        }
        return amounts.Max().ToString("0", CultureInfo.InvariantCulture);
    }
}
