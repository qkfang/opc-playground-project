using System.Text.RegularExpressions;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// Deterministic, offline implementation of the intelligence &amp; research pipeline. No external calls —
/// makes the POC fully demonstrable without Azure/Foundry. Part 1: entity extraction + insight generation.
/// </summary>
public sealed partial class OfflineResearchEngine : IResearchEngine
{
    private readonly SourceCorpus _corpus;

    public OfflineResearchEngine(SourceCorpus corpus) => _corpus = corpus;

    public string Name => "offline";

    public Task<ResearchCase> RunAsync(ResearchCase c, CancellationToken ct = default)
    {
        // Stage 1 — extract key entities from the email + attached document.
        c.Entities = ExtractEntities(c.Email);
        c.AgentSteps.Add(new AgentStepLog { Step = "entities", Summary = $"Extracted {c.Entities.AllKeyEntities.Count()} key entities (org: {c.Entities.PrimaryOrganisation ?? "n/a"}, {c.Entities.Topics.Count} topics, {c.Entities.Technologies.Count} technologies)." });

        // Stage 2 — generate insights from the email + document.
        c.Insights = GenerateInsights(c.Email, c.Entities);
        c.AgentSteps.Add(new AgentStepLog { Step = "insights", Summary = $"Generated {c.Insights.Count} insight(s) from the customer email and document." });

        // Stage 3 — pull data from mocked internal/external sources keyed by entities.
        c.SourceHits = PullSources(c.Entities);
        c.AgentSteps.Add(new AgentStepLog { Step = "sources", Summary = $"Pulled {c.SourceHits.Count} source record(s) ({c.SourceHits.Count(h => h.SourceType == "Internal")} internal, {c.SourceHits.Count(h => h.SourceType == "External")} external)." });

        // Stage 4 — Research Agent synthesises a brief.
        c.Brief = BuildBrief(c);
        c.AgentSteps.Add(new AgentStepLog { Step = "research", Summary = $"Research brief drafted: {c.Brief.KeyFindings.Count} findings, {c.Brief.Risks.Count} risks, {c.Brief.Opportunities.Count} opportunities, {c.Brief.Citations.Count} citations." });

        // Stage 5 — generate the report email that summarises the insights.
        c.ReportEmail = BuildReportEmail(c);
        c.AgentSteps.Add(new AgentStepLog { Step = "report-email", Summary = $"Report email composed for {c.ReportEmail.To}." });

        c.Engine = Name;
        return Task.FromResult(c);
    }

    // ================================================ 1. ENTITY EXTRACTION ================================================

    public ExtractedEntities ExtractEntities(InboundEmail email)
    {
        var e = new ExtractedEntities();
        var doc = email.Document?.Content ?? "";
        var text = $"{email.Subject}\n{email.Body}\n{doc}";

        // Primary organisation: from the sender display/signature or the document's "About" line.
        e.PrimaryOrganisation = GuessPrimaryOrg(email, text);
        if (e.PrimaryOrganisation is not null) e.Organisations.Add(e.PrimaryOrganisation);

        // Other organisations / named systems mentioned (heuristic proper-noun + known-tech capture).
        foreach (var org in FindNamedOrgs(text))
            if (!e.Organisations.Contains(org, StringComparer.OrdinalIgnoreCase)) e.Organisations.Add(org);

        // People: "<First> <Last>, <Title>" patterns + signature names.
        e.People = FindPeople(email, text);

        // Technologies / platforms (curated vocabulary — extensible).
        e.Technologies = FindFromVocabulary(text, TechVocabulary);

        // Locations (curated vocabulary).
        e.Locations = FindFromVocabulary(text, LocationVocabulary);

        // Topics / themes (curated vocabulary).
        e.Topics = FindFromVocabulary(text, TopicVocabulary);

        // Monetary amounts.
        e.MonetaryAmounts = Regex.Matches(text, @"(?:EUR|USD|AUD|GBP|\$|€|£)\s?\d[\d,\.]*\s?(?:million|billion|bn|m|k|MWh|GWh)?", RegexOptions.IgnoreCase)
            .Select(m => m.Value.Trim()).Where(v => v.Any(char.IsDigit)).Distinct().Take(8).ToList();

        // Dates / timeframes.
        e.Dates = Regex.Matches(text, @"\b(?:Q[1-4]\s?20\d{2}|20\d{2}|next financial year|this financial year|within \d+ (?:days|weeks|months)|\d+ months|\d+ weeks)\b", RegexOptions.IgnoreCase)
            .Select(m => m.Value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        // Industry + intent.
        e.Industry = GuessIndustry(text);
        e.Intent = GuessIntent(email);

        return e;
    }

    private static string? GuessPrimaryOrg(InboundEmail email, string text)
    {
        // 1) Signature: "..., <Org>" on the last non-empty body lines.
        var sigOrg = ExtractSignatureOrg(email.Body);
        if (sigOrg is not null) return CleanOrg(sigOrg);

        // 2) Document "About"/intro: "<Org> is a ..." or "<Org> AG/Ltd/Inc/Group/Corporation".
        var m = Regex.Match(text, @"([A-Z][A-Za-z0-9&'\-]+(?:\s+[A-Z][A-Za-z0-9&'\-]+){0,3}\s+(?:AG|Ltd|Limited|Inc|Group|Corporation|Corp|plc|PLC|GmbH|Energy|Retail|Pay))\b");
        if (m.Success) return CleanOrg(m.Groups[1].Value);

        // 3) Email domain → title-cased org.
        var domain = email.From.Contains('@') ? email.From.Split('@')[1] : "";
        var host = domain.Split('.').FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(host) && host.Length > 2)
            return string.Join(' ', host.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return null;
    }

    private static string? ExtractSignatureOrg(string body)
    {
        var lines = body.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        // Look at the last few lines for a "Title, Organisation" or standalone org line.
        foreach (var line in lines.AsEnumerable().Reverse().Take(4))
        {
            var commaIdx = line.LastIndexOf(',');
            if (commaIdx > 0 && commaIdx < line.Length - 2)
            {
                var tail = line[(commaIdx + 1)..].Trim();
                if (Regex.IsMatch(tail, @"(AG|Ltd|Limited|Inc|Group|Corporation|Corp|plc|PLC|GmbH|Energy|Retail|Pay|Logistics|Health|Bank|Mining)\b", RegexOptions.IgnoreCase)
                    || tail.Split(' ').Length is >= 2 and <= 5 && char.IsUpper(tail[0]))
                    return tail;
            }
        }
        return null;
    }

    private static string CleanOrg(string s) => Regex.Replace(s, @"\s+", " ").Trim().TrimEnd('.', ',');

    private static List<string> FindNamedOrgs(string text)
    {
        var known = new[] { "SAP", "SAP S/4HANA", "S/4HANA", "OSIsoft", "PI historian", "Salesforce", "Adobe Analytics",
            "Synapse", "Power BI", "Entra ID", "Tesla", "Fluence", "BYD", "EPEX", "Nord Pool", "Bundesnetzagentur" };
        return known.Where(k => text.Contains(k, StringComparison.OrdinalIgnoreCase)).Distinct().ToList();
    }

    private static List<string> FindPeople(InboundEmail email, string text)
    {
        var people = new List<string>();
        if (!string.IsNullOrWhiteSpace(email.FromName) && email.FromName.Split(' ').Length >= 2)
            people.Add(email.FromName.Trim());

        foreach (Match m in Regex.Matches(text, @"\b((?:Dr\.\s)?[A-Z][a-z]+\s[A-Z][a-z]+)\s*,\s*(?:Head|Director|Lead|VP|Principal|Chief|Manager|CMO|CTO|CEO|CIO|SRE)", RegexOptions.None))
        {
            var name = m.Groups[1].Value.Trim();
            if (!people.Contains(name, StringComparer.OrdinalIgnoreCase)) people.Add(name);
        }
        return people.Take(6).ToList();
    }

    private static List<string> FindFromVocabulary(string text, string[] vocab) =>
        vocab.Where(v => Regex.IsMatch(text, $@"(?<![A-Za-z]){Regex.Escape(v)}(?![A-Za-z])", RegexOptions.IgnoreCase))
             .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static readonly string[] TechVocabulary =
    {
        "Azure", "SAP", "S/4HANA", "OSIsoft", "PI historian", "Salesforce", "Adobe Analytics", "Synapse",
        "Power BI", "Entra ID", "SSO", "Event Hubs", "Stream Analytics", "Azure ML", "FHIR", "HL7",
        "API", "SQL", "multi-region", "circuit breakers", "SLO", "disaster recovery", "failover", "PCI-DSS", "PCI"
    };

    private static readonly string[] LocationVocabulary =
    {
        "Germany", "Denmark", "Norway", "Nordics", "DACH", "Hamburg", "UK", "Ireland", "Australia",
        "New Zealand", "ANZ", "Singapore", "EU", "Victoria", "Perth"
    };

    private static readonly string[] TopicVocabulary =
    {
        "battery", "energy storage", "grid", "arbitrage", "trading", "forecasting", "optimisation",
        "regulatory reporting", "ESG", "state-of-health", "customer data", "CDP", "loyalty",
        "personalisation", "identity resolution", "consent", "GDPR", "privacy", "payments", "resilience",
        "availability", "outage", "multi-region", "PCI-DSS", "disaster recovery", "observability"
    };

    private static string? GuessIndustry(string text)
    {
        var t = text.ToLowerInvariant();
        if (t.Contains("battery") || t.Contains("grid") || t.Contains("renewable") || t.Contains("energy")) return "Energy & Utilities";
        if (t.Contains("retail") || t.Contains("loyalty") || t.Contains("e-commerce") || t.Contains("stores")) return "Retail";
        if (t.Contains("payment") || t.Contains("pci") || t.Contains("merchant") || t.Contains("fintech")) return "Financial Services / Payments";
        if (t.Contains("hospital") || t.Contains("patient") || t.Contains("clinical") || t.Contains("fhir")) return "Healthcare";
        if (t.Contains("logistics") || t.Contains("fleet") || t.Contains("freight")) return "Logistics & Transport";
        return null;
    }

    private static string? GuessIntent(InboundEmail email)
    {
        var doc = email.Document;
        if (doc is not null)
        {
            return doc.DocType switch
            {
                "RFP" => "Formal RFP — soliciting a proposal for a defined scope.",
                "Briefing note" => "Early discovery — seeking perspective and rough order of magnitude.",
                "Incident report" => "Post-incident — seeking an independent resilience/architecture review.",
                _ => $"Shared a {doc.DocType.ToLowerInvariant()} for review."
            };
        }
        return "General inbound enquiry.";
    }
}
