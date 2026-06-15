using System.Text;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// OfflineResearchEngine part 2: insight generation, source-pull orchestration, the Research Agent's
/// brief synthesis, and the report-email generator.
/// </summary>
public sealed partial class OfflineResearchEngine
{
    // ================================================ 2. INSIGHTS ================================================

    public static List<Insight> GenerateInsights(InboundEmail email, ExtractedEntities x)
    {
        var insights = new List<Insight>();
        var doc = email.Document;
        var text = $"{email.Subject}\n{email.Body}\n{doc?.Content}".ToLowerInvariant();

        // Spam / non-genuine guard — produce a single explanatory insight and stop.
        if (IsLikelyJunk(email))
        {
            insights.Add(new Insight
            {
                Headline = "Message appears to be spam / non-genuine",
                Detail = "Prize/fee solicitation language and a low-reputation sender domain. No genuine customer document attached.",
                Category = "Signal",
                Confidence = "High",
                Evidence = "Subject/body match scam heuristics; no document."
            });
            return insights;
        }

        // Intent / need.
        if (x.Intent is not null)
            insights.Add(new Insight
            {
                Headline = doc is not null ? $"{doc.DocType} received: {ShortIntent(x.Intent)}" : "Inbound enquiry",
                Detail = x.Intent,
                Category = "Need",
                Confidence = doc is not null ? "High" : "Medium",
                Evidence = doc is not null ? $"Attached document: {doc.FileName}" : "Email body."
            });

        // Scale / context from numbers.
        if (x.MonetaryAmounts.Count > 0)
            insights.Add(new Insight
            {
                Headline = "Quantified scale & budget signals present",
                Detail = $"Document references {string.Join(", ", x.MonetaryAmounts.Take(4))}. Indicates a funded or board-level initiative rather than idle curiosity.",
                Category = "Opportunity",
                Confidence = "Medium",
                Evidence = "Monetary/scale figures in the document."
            });

        // Pain / risk drivers — pull explicit pain sentences.
        foreach (var pain in ExtractPainPoints(doc?.Content ?? email.Body))
            insights.Add(new Insight
            {
                Headline = "Stated pain point",
                Detail = pain,
                Category = "Risk",
                Confidence = "High",
                Evidence = "Directly stated in the customer document."
            });

        // Integration / constraint signals.
        if (x.Technologies.Any(t => t is "SAP" or "S/4HANA" or "OSIsoft" or "PI historian" or "Salesforce" or "Synapse"))
            insights.Add(new Insight
            {
                Headline = "Existing enterprise systems to integrate",
                Detail = $"Mentions {string.Join(", ", x.Technologies.Where(t => t is "SAP" or "S/4HANA" or "OSIsoft" or "PI historian" or "Salesforce" or "Synapse" or "Power BI" or "Adobe Analytics"))}. Integration and data-residency will shape the solution.",
                Category = "Context",
                Confidence = "Medium",
                Evidence = "Named systems in the document."
            });

        // Compliance / governance signal.
        if (text.Contains("gdpr") || text.Contains("pci") || text.Contains("residency") || text.Contains("governance") || text.Contains("privacy"))
            insights.Add(new Insight
            {
                Headline = "Compliance & data-governance is in scope",
                Detail = "The customer explicitly raises regulatory/data-governance requirements — a buying criterion and a differentiation opportunity.",
                Category = "Opportunity",
                Confidence = "Medium",
                Evidence = "Compliance terms in subject/body/document."
            });

        // Timeline urgency.
        if (x.Dates.Count > 0)
            insights.Add(new Insight
            {
                Headline = "Defined timeframe stated",
                Detail = $"Timeframe markers: {string.Join(", ", x.Dates.Take(4))}. Use these to anchor a delivery plan and response SLA.",
                Category = "Signal",
                Confidence = "Medium",
                Evidence = "Date/timeline references."
            });

        return insights;
    }

    private static string ShortIntent(string intent) => intent.Split('—', '-')[0].Trim().TrimEnd('.');

    /// <summary>Prefer a monetary amount that appears in a budget/investment context in the document
    /// (e.g. "Indicative budget: EUR 2.5M") over the largest figure (often company revenue).</summary>
    private static string? PreferredBudget(ResearchCase c)
    {
        var content = c.Email.Document?.Content;
        if (string.IsNullOrWhiteSpace(content)) return null;
        // Find a money amount that appears shortly AFTER a budget/investment cue word, regardless of
        // line breaks (revenue figures often precede the budget in the same paragraph).
        var m = System.Text.RegularExpressions.Regex.Match(
            content,
            @"(?:budget|indicative|allocated|invest[a-z]*)\b[^.\n]{0,40}?((?:EUR|USD|AUD|GBP|\$|€|£)\s?\d[\d,\.]*\s?(?:million|billion|bn|m|k)?(?:\s?-\s?(?:EUR|USD|AUD|GBP|\$|€|£)?\s?\d[\d,\.]*\s?(?:million|billion|bn|m|k)?)?)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (m.Success && m.Groups[1].Value.Any(char.IsDigit)) return m.Groups[1].Value.Trim();
        return null;
    }

    private static IEnumerable<string> ExtractPainPoints(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) yield break;
        var painCues = new[] { "manual", "taking our team", "takes our team", "cannot", "can't", "no unified", "no automated",
            "stalled", "exhaustion", "fragmented", "cannot resolve", "leaving", "stale", "flagged", "gaps", "no slo", "lead times" };
        var sentences = content.Replace("\n", " ").Split(new[] { ". ", ".\n", "; " }, StringSplitOptions.RemoveEmptyEntries);
        int found = 0;
        foreach (var s in sentences)
        {
            var t = s.Trim().TrimStart('-', '*', ' ');
            if (t.Length < 20 || t.Length > 240) continue;
            if (painCues.Any(c => t.Contains(c, StringComparison.OrdinalIgnoreCase)))
            {
                yield return t.EndsWith('.') ? t : t + ".";
                if (++found >= 4) yield break;
            }
        }
    }

    private static bool IsLikelyJunk(InboundEmail email)
    {
        var text = $"{email.Subject}\n{email.Body}".ToLowerInvariant();
        var redFlags = new[] { "you have won", "lucky winner", "prize", "verification fee", "bank details",
            "claim your", "million dollar", "act now", "pre-selected", "expires in 24 hours", "free $", "explode" };
        int hits = redFlags.Count(f => text.Contains(f));
        var domain = email.From.Contains('@') ? email.From.Split('@')[1].ToLowerInvariant() : "";
        bool suspiciousTld = domain.EndsWith(".biz") || domain.EndsWith(".top") || domain.EndsWith(".win");
        return hits >= 2 || (hits >= 1 && suspiciousTld && email.Document is null);
    }

    // ================================================ 3. SOURCE PULLS ================================================

    public List<SourceHit> PullSources(ExtractedEntities x)
    {
        // Build the entity set to query: org + topics + technologies + industry token.
        var query = new List<string>();
        if (x.PrimaryOrganisation is not null) query.Add(x.PrimaryOrganisation);
        query.AddRange(x.Organisations);
        query.AddRange(x.Topics);
        query.AddRange(x.Technologies);
        if (x.Industry is not null) query.Add(x.Industry.Split(' ', '&', '/')[0]);
        return _corpus.Pull(query);
    }

    // ================================================ 4. RESEARCH BRIEF ================================================

    public static ResearchBrief BuildBrief(ResearchCase c)
    {
        var x = c.Entities;
        var brief = new ResearchBrief { Title = $"Research Brief — {x.PrimaryOrganisation ?? "Inbound enquiry"}" };

        if (c.Insights.Count == 1 && c.Insights[0].Category == "Signal" && c.Insights[0].Headline.Contains("spam", StringComparison.OrdinalIgnoreCase))
        {
            brief.ExecutiveSummary = "Message classified as spam/non-genuine; no research performed.";
            brief.Confidence = "High";
            brief.RecommendedActions.Add("Quarantine and discard. No follow-up.");
            return brief;
        }

        // Citations from source hits.
        int n = 1;
        foreach (var h in c.SourceHits)
            brief.Citations.Add(new Citation { Marker = $"[S{n++}]", SourceName = h.SourceName, Title = h.Title, Url = h.Url });

        string Cite(string sourceNameContains)
        {
            var idx = c.SourceHits.FindIndex(h => h.SourceName.Contains(sourceNameContains, StringComparison.OrdinalIgnoreCase));
            return idx >= 0 ? $" [S{idx + 1}]" : "";
        }

        // Executive summary.
        var sb = new StringBuilder();
        sb.Append($"{x.PrimaryOrganisation ?? "The sender"} ");
        sb.Append(x.Industry is not null ? $"({x.Industry}) " : "");
        sb.Append(x.Intent is not null ? x.Intent.ToLowerInvariant().TrimEnd('.') : "made an inbound enquiry");
        sb.Append(". ");
        var internalCount = c.SourceHits.Count(h => h.SourceType == "Internal");
        var externalCount = c.SourceHits.Count(h => h.SourceType == "External");
        sb.Append($"Cross-referenced against {internalCount} internal and {externalCount} external source(s), ");
        sb.Append(internalCount > 0 ? "we have prior context on this account" : "this is a net-new account");
        sb.Append(". ");
        if (x.MonetaryAmounts.Count > 0) sb.Append($"Indicative scale/budget: {PreferredBudget(c) ?? x.MonetaryAmounts.First()}. ");
        brief.ExecutiveSummary = sb.ToString();

        // Key findings — from insights + corroborating sources.
        foreach (var ins in c.Insights.Where(i => i.Category is "Need" or "Context" or "Signal").Take(4))
            brief.KeyFindings.Add($"{ins.Headline}: {ins.Detail}");
        var newsHit = c.SourceHits.FirstOrDefault(h => h.SourceType == "External");
        if (newsHit is not null) brief.KeyFindings.Add($"External corroboration — {newsHit.Title}: {newsHit.Snippet}{Cite(newsHit.SourceName)}");
        var crmHit = c.SourceHits.FirstOrDefault(h => h.SourceName.Contains("CRM", StringComparison.OrdinalIgnoreCase));
        if (crmHit is not null) brief.KeyFindings.Add($"Internal account context — {crmHit.Snippet}{Cite("CRM")}");

        // Risks — from risk insights + governance.
        foreach (var ins in c.Insights.Where(i => i.Category == "Risk").Take(4))
            brief.Risks.Add(ins.Detail);
        if (brief.Risks.Count == 0) brief.Risks.Add("No explicit risks stated; validate constraints during discovery.");

        // Opportunities — from opportunity insights + reusable internal assets.
        foreach (var ins in c.Insights.Where(i => i.Category == "Opportunity").Take(3))
            brief.Opportunities.Add(ins.Detail);
        var kbHit = c.SourceHits.FirstOrDefault(h => h.SourceName.Contains("knowledge base", StringComparison.OrdinalIgnoreCase));
        if (kbHit is not null) brief.Opportunities.Add($"Reusable internal asset available — {kbHit.Title}: {kbHit.Snippet}{Cite("knowledge base")}");

        // Recommended actions.
        var docType = c.Email.Document?.DocType;
        brief.RecommendedActions.Add(docType switch
        {
            "RFP" => "Confirm intent to respond before the stated deadline and assign a bid lead.",
            "Incident report" => "Offer an independent resilience review scoped to the stated root causes.",
            "Briefing note" => "Propose a short discovery workshop and a rough order-of-magnitude estimate.",
            _ => "Schedule a qualification call to clarify scope and budget."
        });
        if (kbHit is not null) brief.RecommendedActions.Add($"Lead with the matching reference architecture ({kbHit.Title}).");
        brief.RecommendedActions.Add("Prepare a tailored point of view referencing the customer's stated drivers and our prior work.");

        // Open questions.
        if (c.Email.Document?.Content is { } dc)
        {
            if (!dc.Contains("budget", StringComparison.OrdinalIgnoreCase)) brief.OpenQuestions.Add("Is budget approved, and what is the range?");
            if (!dc.Contains("timeline", StringComparison.OrdinalIgnoreCase) && x.Dates.Count == 0) brief.OpenQuestions.Add("What is the decision timeline?");
        }
        brief.OpenQuestions.Add("Who are the technical and commercial decision makers, and what are their evaluation criteria?");

        brief.Confidence = c.SourceHits.Count >= 3 && c.Email.Document is not null ? "High" : c.SourceHits.Count >= 1 ? "Medium" : "Low";
        return brief;
    }

    // ================================================ 5. REPORT EMAIL ================================================

    public static ReportEmail BuildReportEmail(ResearchCase c)
    {
        var x = c.Entities;
        var brief = c.Brief;
        var org = x.PrimaryOrganisation ?? "the prospect";

        // Junk short-circuit.
        if (c.Insights.Count == 1 && c.Insights[0].Headline.Contains("spam", StringComparison.OrdinalIgnoreCase))
        {
            var junk = new ReportEmail
            {
                To = "intake-triage@contoso.com",
                Subject = $"[Quarantined] Non-genuine inbound from {c.Email.From}",
                Greeting = "Team,",
                Body = "This inbound was automatically classified as spam/non-genuine and quarantined. No research was performed and no action is required.",
                CallToAction = "No action required.",
                Signature = "— Intelligence & Research Agent (proj40)"
            };
            junk.RenderedMarkdown = RenderEmail(junk, c);
            return junk;
        }

        var routed = x.Industry switch
        {
            "Energy & Utilities" => "energy-vertical@contoso.com",
            "Retail" => "retail-vertical@contoso.com",
            "Financial Services / Payments" => "fsi-vertical@contoso.com",
            "Healthcare" => "health-vertical@contoso.com",
            _ => "sales-desk@contoso.com"
        };

        var email = new ReportEmail
        {
            To = routed,
            Cc = "research-desk@contoso.com",
            Subject = $"Inbound intelligence: {org} — {c.Email.Document?.DocType ?? "enquiry"} ({brief.Confidence} confidence)",
            Greeting = "Hi team,",
            Signature = "— Intelligence & Research Agent (proj40)\nMicrosoft Foundry Intelligence & Research POC"
        };

        var body = new StringBuilder();
        body.AppendLine($"We received an inbound {c.Email.Document?.DocType?.ToLowerInvariant() ?? "enquiry"} from {c.Email.FromName} at {org}. Summary of the automated research below.");
        body.AppendLine();
        body.AppendLine("WHAT THEY WANT");
        body.AppendLine($"  {brief.ExecutiveSummary.Trim()}");
        body.AppendLine();
        if (brief.KeyFindings.Count > 0)
        {
            body.AppendLine("KEY FINDINGS");
            foreach (var f in brief.KeyFindings.Take(5)) body.AppendLine($"  • {f}");
            body.AppendLine();
        }
        if (brief.Opportunities.Count > 0)
        {
            body.AppendLine("WHY WE CAN WIN");
            foreach (var o in brief.Opportunities.Take(3)) body.AppendLine($"  • {o}");
            body.AppendLine();
        }
        if (brief.Risks.Count > 0)
        {
            body.AppendLine("WATCH-OUTS");
            foreach (var r in brief.Risks.Take(3)) body.AppendLine($"  • {r}");
            body.AppendLine();
        }
        body.AppendLine("RECOMMENDED NEXT STEPS");
        foreach (var a in brief.RecommendedActions.Take(4)) body.AppendLine($"  • {a}");
        email.Body = body.ToString().TrimEnd();

        email.CallToAction = brief.RecommendedActions.FirstOrDefault() ?? "Review and action.";
        email.RenderedMarkdown = RenderEmail(email, c);
        return email;
    }

    private static string RenderEmail(ReportEmail e, ResearchCase c)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"To: {e.To}");
        if (!string.IsNullOrWhiteSpace(e.Cc)) sb.AppendLine($"Cc: {e.Cc}");
        sb.AppendLine($"Subject: {e.Subject}");
        sb.AppendLine($"X-Generated: {c.CreatedUtc:yyyy-MM-dd HH:mm} UTC · engine={c.Engine} · case={c.CaseId}");
        sb.AppendLine();
        sb.AppendLine(e.Greeting);
        sb.AppendLine();
        sb.AppendLine(e.Body);
        sb.AppendLine();
        if (c.Brief.Citations.Count > 0)
        {
            sb.AppendLine("SOURCES");
            foreach (var cit in c.Brief.Citations)
                sb.AppendLine($"  {cit.Marker} {cit.SourceName} — {cit.Title}{(cit.Url is not null ? $" ({cit.Url})" : "")}");
            sb.AppendLine();
        }
        sb.AppendLine(e.Signature);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine("Microsoft Foundry Intelligence & Research POC — proj40. Mock/demo data; not for production decisions.");
        return sb.ToString();
    }
}
