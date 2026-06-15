using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services.Foundry;

/// <summary>
/// Intake &amp; origination engine backed by a Microsoft Foundry prompt agent (Microsoft Agent
/// Framework, hosted in-process pattern via <c>AIProjectClient.AsAIAgent(...)</c>).
///
/// Pipeline (grounded prompt-agent calls, each returning JSON):
///   1. EXTRACTION — read the inbound email, emit Account/Lead/Opportunity records.
///   2. TRIAGE     — classify + score (Hot/Warm/Cold/Spam) with a transparent factor breakdown.
///   3. RESEARCH   — Lead Management Agent: company overview + inbound demand signals + actions.
///   4. REPORT     — Report Agent: assemble an origination study (sections + markdown).
///
/// On ANY failure (missing config, auth, transient service error) it transparently falls back to the
/// deterministic offline engine and records the reason, so the POC is always demonstrable.
/// </summary>
public sealed class FoundryOriginationEngine : IOriginationEngine
{
    private readonly FoundryOptions _options;
    private readonly OfflineOriginationEngine _offline;
    private readonly ILogger<FoundryOriginationEngine> _logger;

    public FoundryOriginationEngine(FoundryOptions options, OfflineOriginationEngine offline, ILogger<FoundryOriginationEngine> logger)
    {
        _options = options;
        _offline = offline;
        _logger = logger;
    }

    public string Name => "foundry";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public async Task<OriginationCase> ProcessAsync(InboundEmail email, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            var c0 = await _offline.ProcessAsync(email, ct);
            c0.AgentSteps.Insert(0, new AgentStepLog { Agent = "Engine", Step = "engine", Summary = "Foundry disabled/unconfigured — used deterministic offline engine." });
            return c0;
        }

        var c = new OriginationCase { Email = email, Engine = Name, Status = "running" };
        try
        {
            var agent = CreateAgent();
            var corpus = BuildCorpus(email);

            // 1) EXTRACTION
            var extraction = await RunJsonAsync<ExtractionResult>(agent, ExtractionPrompt(corpus), ct)
                             ?? throw new InvalidOperationException("Extraction step returned no JSON.");
            NormalizeExtraction(extraction, email);
            c.Extraction = extraction;
            c.AgentSteps.Add(Step("Extraction", "extract", $"Foundry agent extracted Account '{extraction.Account.Name}', Lead '{extraction.Lead.FullName}', Opportunity '{extraction.Opportunity.Name}'."));

            // 2) TRIAGE
            var triage = await RunJsonAsync<TriageResult>(agent, TriagePrompt(corpus, extraction), ct)
                         ?? throw new InvalidOperationException("Triage step returned no JSON.");
            NormalizeTriage(triage);
            c.Triage = triage;
            c.AgentSteps.Add(Step("Triage", "classify", $"Foundry agent classified {triage.Classification} (score {triage.Score}/100) -> {triage.RoutedTo}."));

            // 3) RESEARCH (Lead Management Agent)
            var research = await RunJsonAsync<ResearchResult>(agent, ResearchPrompt(corpus, extraction, triage), ct)
                           ?? new ResearchResult();
            c.Research = research;
            c.AgentSteps.Add(Step("LeadResearch", "research", $"Foundry agent captured {research.DemandSignals.Count} demand signal(s) and {research.RecommendedActions.Count} action(s)."));

            // 4) REPORT (Report Agent) — model writes narrative; we assemble the markdown deterministically.
            var report = await RunJsonAsync<ReportDraft>(agent, ReportPrompt(corpus, extraction, triage, research), ct);
            c.Report = BuildReportFromDraft(report, c);
            c.AgentSteps.Add(Step("Report", "report", $"Foundry agent generated origination study '{c.Report.Title}' — disposition {c.Report.Disposition}."));

            c.Engine = Name;
            c.Status = "completed";
            return c;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry pipeline failed; falling back to offline engine.");
            var fallback = await _offline.ProcessAsync(email, ct);
            fallback.AgentSteps.Insert(0, new AgentStepLog { Agent = "Engine", Step = "engine", Summary = $"Foundry call failed ({ex.GetType().Name}); fell back to offline engine. Detail: {Trunc(ex.Message, 200)}" });
            return fallback;
        }
    }

    private static AgentStepLog Step(string agent, string step, string summary) =>
        new() { Agent = agent, Step = step, Engine = "foundry", Summary = summary };

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeploymentName,
            instructions:
                "You are a B2B intake & origination analyst for an enterprise sales org. You read inbound " +
                "emails, extract structured CRM records (Account, Lead, Opportunity), triage and score them, " +
                "research the prospect, capture inbound demand signals, and write an origination study. " +
                "Always respond with ONLY a single valid JSON object matching the requested schema — no " +
                "markdown, no prose, no code fences. Be realistic and conservative; never invent specific " +
                "facts not supported by the email (mark unknowns as null).",
            name: _options.AgentName);
    }

    private static string BuildCorpus(InboundEmail email) => Trunc(
        $"FROM: {email.FromName} <{email.From}>\nTO: {email.To}\nRECEIVED: {email.ReceivedUtc:u}\n" +
        $"SUBJECT: {email.Subject}\nATTACHMENTS: {(email.Attachments.Count > 0 ? string.Join(", ", email.Attachments) : "none")}\n\n{email.Body}", 24_000);

    private async Task<T?> RunJsonAsync<T>(AIAgent agent, string prompt, CancellationToken ct)
    {
        var response = await agent.RunAsync(prompt, cancellationToken: ct);
        var json = ExtractJsonObject(response.Text ?? string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    private static string? ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text.Substring(start, end - start + 1);
    }

    // ---------------- Prompts ----------------

    private static string ExtractionPrompt(string corpus) =>
        $$"""
        Extract structured CRM records from this inbound email. Return JSON exactly:
        {
          "account": { "name": string, "industry": string|null, "website": string|null, "domain": string|null,
                       "employeeBand": string|null, "annualRevenueBand": string|null, "country": string|null, "region": string|null, "notes": string[] },
          "lead": { "fullName": string, "title": string|null, "email": string|null, "phone": string|null, "accountName": string|null,
                    "seniority": "C-Level"|"VP"|"Director"|"Manager"|"Individual Contributor"|null, "isDecisionMaker": boolean,
                    "preferredContactMethod": string|null, "notes": string[] },
          "opportunity": { "name": string, "productInterest": string|null, "summary": string|null, "estimatedValue": number|null,
                           "currency": string, "timeline": string|null, "budgetStatus": string|null, "stage": "New",
                           "drivers": string[], "notes": string[] },
          "confidence": number, "missingFields": string[]
        }
        Use null for anything not stated. estimatedValue is a number in the stated currency (default AUD).

        EMAIL:
        {{corpus}}
        """;

    private static string TriagePrompt(string corpus, ExtractionResult x) =>
        $$"""
        Triage and classify this lead based on the EXTRACTION and email. Score 0-100 across these factors and
        return a transparent breakdown. If it is spam/scam, classify "Spam" with score 0.

        EXTRACTION: {{JsonSerializer.Serialize(x, JsonOpts)}}

        Return JSON exactly:
        {
          "classification": "Hot"|"Warm"|"Cold"|"Spam",
          "score": number,
          "routedTo": string,            // e.g. "Enterprise Sales", "Inside Sales", "Nurture / Marketing", "Quarantine"
          "slaTarget": string,           // e.g. "4 business hours", "1 business day", "5 business days", "None"
          "recommendation": string,
          "factors": [ { "name": string, "points": number, "detail": string } ],
          "tags": string[]
        }
        Weight roughly: budget/deal size (0-30), authority (0-20), need/pain (0-20), timeline (0-15), company fit/size (0-15).

        EMAIL:
        {{corpus}}
        """;

    private static string ResearchPrompt(string corpus, ExtractionResult x, TriageResult t) =>
        $$"""
        Act as a Lead Management Agent. Produce a concise company overview, capture INBOUND DEMAND SIGNALS
        (from the email's stated pains/context; you may add plausible sector signals clearly marked as
        industry intelligence), and recommend next actions. Do NOT fabricate specific named facts.

        EXTRACTION: {{JsonSerializer.Serialize(x, JsonOpts)}}
        TRIAGE: {{JsonSerializer.Serialize(t, JsonOpts)}}

        Return JSON exactly:
        {
          "companyOverview": string,
          "demandSignals": [ { "signal": string, "source": string, "strength": "Strong"|"Medium"|"Weak", "implication": string } ],
          "recommendedActions": string[],
          "talkingPoints": string[],
          "competitors": string[],
          "fitAssessment": string
        }

        EMAIL:
        {{corpus}}
        """;

    private static string ReportPrompt(string corpus, ExtractionResult x, TriageResult t, ResearchResult r) =>
        $$"""
        Act as a Report Agent. Write an origination study for this lead. Return JSON exactly:
        {
          "title": string,
          "executiveSummary": string,
          "disposition": "Pursue"|"Nurture"|"Disqualify",
          "recommendedNextStep": string,
          "sections": [ { "heading": string, "body": string } ]
        }
        Include sections covering Account, Lead, Opportunity, Triage rationale, Demand signals, and Next steps.

        CONTEXT:
        EXTRACTION: {{JsonSerializer.Serialize(x, JsonOpts)}}
        TRIAGE: {{JsonSerializer.Serialize(t, JsonOpts)}}
        RESEARCH: {{JsonSerializer.Serialize(r, JsonOpts)}}

        EMAIL:
        {{corpus}}
        """;

    // ---------------- Normalisation + report assembly ----------------

    private static void NormalizeExtraction(ExtractionResult x, InboundEmail email)
    {
        if (string.IsNullOrWhiteSpace(x.Account.Name)) x.Account.Name = "Unknown";
        if (string.IsNullOrWhiteSpace(x.Lead.FullName)) x.Lead.FullName = string.IsNullOrWhiteSpace(email.FromName) ? "Unknown Contact" : email.FromName;
        if (string.IsNullOrWhiteSpace(x.Lead.Email)) x.Lead.Email = email.From;
        if (string.IsNullOrWhiteSpace(x.Opportunity.Currency)) x.Opportunity.Currency = "AUD";
        if (string.IsNullOrWhiteSpace(x.Opportunity.Name)) x.Opportunity.Name = $"{x.Account.Name} — Inbound enquiry";
        if (string.IsNullOrWhiteSpace(x.Opportunity.Stage)) x.Opportunity.Stage = "New";
        if (x.Confidence is < 0 or > 1) x.Confidence = 0.7m;
    }

    private static void NormalizeTriage(TriageResult t)
    {
        t.Score = Math.Clamp(t.Score, 0, 100);
        if (string.IsNullOrWhiteSpace(t.Classification)) t.Classification = t.Score >= 70 ? "Hot" : t.Score >= 45 ? "Warm" : "Cold";
        if (string.IsNullOrWhiteSpace(t.RoutedTo)) t.RoutedTo = t.Classification == "Hot" ? "Enterprise Sales" : t.Classification == "Warm" ? "Inside Sales" : "Nurture / Marketing";
        if (string.IsNullOrWhiteSpace(t.SlaTarget)) t.SlaTarget = t.Classification switch { "Hot" => "4 business hours", "Warm" => "1 business day", "Spam" => "None", _ => "5 business days" };
    }

    private static OriginationReport BuildReportFromDraft(ReportDraft? draft, OriginationCase c)
    {
        if (draft is null || draft.Sections.Count == 0)
        {
            // Model gave nothing usable; deterministically build the study from the structured records.
            return OfflineOriginationEngine.BuildReport(c);
        }
        var report = new OriginationReport
        {
            Title = string.IsNullOrWhiteSpace(draft.Title) ? $"Origination Study — {c.Extraction.Account.Name}" : draft.Title,
            ExecutiveSummary = draft.ExecutiveSummary ?? "",
            Disposition = string.IsNullOrWhiteSpace(draft.Disposition)
                ? (c.Triage.Classification is "Hot" or "Warm" ? "Pursue" : c.Triage.Classification == "Spam" ? "Disqualify" : "Nurture")
                : draft.Disposition,
            RecommendedNextStep = draft.RecommendedNextStep ?? "",
            Sections = draft.Sections.Select(s => new ReportSection { Heading = s.Heading ?? "", Body = s.Body ?? "" }).ToList(),
        };
        report.GeneratedMarkdown = RenderMarkdown(report, c);
        return report;
    }

    private static string RenderMarkdown(OriginationReport report, OriginationCase c)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {report.Title}");
        sb.AppendLine();
        sb.AppendLine($"*Generated {c.CreatedUtc:yyyy-MM-dd HH:mm} UTC · Engine: {c.Engine} · Case {c.CaseId}*");
        sb.AppendLine();
        sb.AppendLine($"**Disposition:** {report.Disposition}  ");
        sb.AppendLine($"**Classification:** {c.Triage.Classification} (score {c.Triage.Score}/100)");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(report.ExecutiveSummary))
        {
            sb.AppendLine("## Executive Summary");
            sb.AppendLine();
            sb.AppendLine(report.ExecutiveSummary);
            sb.AppendLine();
        }
        foreach (var s in report.Sections)
        {
            sb.AppendLine($"## {s.Heading}");
            sb.AppendLine();
            sb.AppendLine(s.Body);
            sb.AppendLine();
        }
        sb.AppendLine("---");
        sb.AppendLine("*Microsoft Foundry Intake & Origination POC — proj39.*");
        return sb.ToString();
    }

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ---------------- DTO for the report-agent draft ----------------

    private sealed class ReportDraft
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("executiveSummary")] public string? ExecutiveSummary { get; set; }
        [JsonPropertyName("disposition")] public string? Disposition { get; set; }
        [JsonPropertyName("recommendedNextStep")] public string? RecommendedNextStep { get; set; }
        [JsonPropertyName("sections")] public List<DraftSection> Sections { get; set; } = new();
    }

    private sealed class DraftSection
    {
        [JsonPropertyName("heading")] public string? Heading { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
    }
}
