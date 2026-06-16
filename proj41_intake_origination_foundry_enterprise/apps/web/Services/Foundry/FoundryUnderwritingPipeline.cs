using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services.Foundry;

/// <summary>
/// Underwriting pipeline backed by a Microsoft Foundry prompt agent (Microsoft Agent Framework,
/// in-process pattern via <c>AIProjectClient.AsAIAgent(...)</c> + <c>agent.RunAsync(...)</c>).
///
/// Four grounded prompt-agent calls, each returning a single JSON object:
///   1. INTAKE    — read the broker email, emit Producer / Insured / RiskSubmission + confidence.
///   2. TRIAGE    — appetite class, risk/fit score, routing, priority/SLA, referral triggers.
///   3. RESEARCH  — exposure + inbound demand signals + recommended questions.
///   4. STUDY     — executive underwriting risk study (recommendation, pricing, conditions, actions).
///
/// On ANY failure (missing config, auth, transient error, malformed JSON) each stage transparently
/// falls back to the deterministic offline pipeline and records the reason, so the POC always runs.
/// </summary>
public sealed class FoundryUnderwritingPipeline : IUnderwritingPipeline
{
    private readonly FoundryOptions _options;
    private readonly OfflineUnderwritingPipeline _offline;
    private readonly ILogger<FoundryUnderwritingPipeline> _logger;

    public string Name => "foundry";

    public FoundryUnderwritingPipeline(FoundryOptions options, OfflineUnderwritingPipeline offline, ILogger<FoundryUnderwritingPipeline> logger)
    {
        _options = options;
        _offline = offline;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SubmissionCase> RunAsync(SubmissionEmail email, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Foundry not configured; using offline pipeline.");
            return await _offline.RunAsync(email, ct);
        }

        AIAgent agent;
        try
        {
            agent = CreateAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Foundry agent; falling back to offline pipeline.");
            var off = await _offline.RunAsync(email, ct);
            off.Trace.Insert(0, new AgentTrace { Stage = "Engine", Agent = "offline", Engine = "offline", Summary = $"Foundry init failed ({ex.GetType().Name}); offline pipeline used." });
            return off;
        }

        var trace = new List<AgentTrace>();
        var emailText = $"FROM: {email.FromName} <{email.From}>\nCHANNEL: {email.Channel}\nSUBJECT: {email.Subject}\nATTACHMENTS: {string.Join(", ", email.Attachments)}\n\n{email.Body}";

        // 1) INTAKE
        var records = await StageAsync(trace, "Submission Intake", agent, IntakePrompt(emailText),
            () => _offline.ExtractStage(email),
            r => $"Extracted insured '{r.Insured.CompanyName}', {r.Submission.LineOfBusiness}; producer '{r.Producer.ContactName}'.",
            ct);

        // 2) TRIAGE
        var triage = await StageAsync(trace, "Appetite & Triage", agent, TriagePrompt(emailText, records),
            () => _offline.TriageStage(records),
            t => $"{t.AppetiteClass} -> {t.Recommendation}; risk {t.RiskScore}, fit {t.FitScore}, {t.Priority}.",
            ct);

        // 3) RESEARCH
        var research = await StageAsync(trace, "Risk Research", agent, ResearchPrompt(records, triage),
            () => _offline.ResearchStage(email, records, triage),
            r => $"{r.Signals.Count} exposure signals; intent {r.IntentScore} ({r.IntentBand}).",
            ct);

        // 4) STUDY
        var study = await StageAsync(trace, "Underwriting Study", agent, StudyPrompt(records, triage, research),
            () => _offline.StudyStage(records, triage, research),
            s => $"Study '{s.Title}' -> {s.OverallRecommendation}.",
            ct);

        var anyFoundry = trace.Any(t => t.Engine == "foundry");
        var reference = $"SUB-{DateTimeOffset.UtcNow:yyyy}-{Random.Shared.Next(10000, 99999)}";

        return new SubmissionCase
        {
            Reference = reference,
            Status = triage.Declined ? "declined" : "completed",
            Engine = anyFoundry ? "foundry" : "offline",
            Source = email,
            Records = records,
            Triage = triage,
            Research = research,
            Study = study,
            Trace = trace
        };
    }

    // ---------------- Stage runner with per-stage offline fallback ----------------

    private async Task<T> StageAsync<T>(List<AgentTrace> trace, string stage, AIAgent agent, string prompt,
        Func<T> offlineFallback, Func<T, string> summarise, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var parsed = await RunJsonAsync<T>(agent, prompt, ct);
            if (parsed is null) throw new InvalidOperationException("Agent returned no parseable JSON.");
            sw.Stop();
            trace.Add(new AgentTrace { Stage = stage, Agent = "foundry", Engine = "foundry", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Summary = summarise(parsed) });
            return parsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Foundry stage '{Stage}' failed; using offline result for this stage.", stage);
            var fb = offlineFallback();
            trace.Add(new AgentTrace { Stage = stage, Agent = "offline-fallback", Engine = "offline", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Summary = $"Foundry '{stage}' failed ({ex.GetType().Name}); offline result used." });
            return fb;
        }
    }

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeployment,
            instructions:
                "You are a commercial property & casualty insurance underwriting specialist. You read inbound " +
                "broker submission emails and produce structured records (producer/insured/risk submission), an " +
                "appetite & triage decision, exposure/demand-signal research, and an executive underwriting risk " +
                "study. Always respond with ONLY a single valid JSON object matching the requested schema — no " +
                "markdown, no prose, no code fences. Be realistic and conservative; never invent verifiable facts " +
                "(treat exposure signals as analyst hypotheses) and use null when a value is unknown.",
            name: "sentinel-underwriting-agent");
    }

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

    private static string IntakePrompt(string emailText) =>
        $$"""
        Extract structured underwriting records from this broker submission email. Return JSON exactly:
        {
          "producer": { "contactName": string, "title": string, "brokerage": string, "email": string, "phone": string, "brokerTier": "National|Regional|Independent|Wholesale|Unknown", "appointed": bool, "confidence": number },
          "insured":  { "companyName": string, "industry": string, "sicDivision": string, "headquarters": string, "country": string, "employeeCount": number, "annualRevenue": number, "locationCount": number, "totalInsurableValue": number, "yearsInBusiness": number, "enrichment": string[], "confidence": number },
          "submission": { "lineOfBusiness": "Property|General Liability|Cyber|Professional Liability|Workers Comp|Marine|Commercial Auto|Management Liability|Multi-line", "coverageType": string, "requestedLimit": number, "deductible": number, "estimatedAnnualPremium": number, "effectiveDate": string, "submissionType": "New Business|Renewal|Rewrite", "incumbentCarriers": string[], "confidence": number },
          "missingForUnderwriting": string[]
        }
        Confidence values are 0..1. Use null where unknown.

        EMAIL:
        {{emailText}}
        """;

    private static string TriagePrompt(string emailText, ExtractedRecords rec) =>
        $$"""
        Decide appetite and triage for this submission. Return JSON:
        {
          "appetiteClass": "In Appetite|Refer to Underwriter|Out of Appetite|Decline",
          "recommendation": "Quote|Refer|Decline",
          "riskScore": number, "fitScore": number,
          "priority": "P1|P2|P3", "slaHours": number,
          "routingQueue": string, "assignedDesk": string,
          "declined": bool, "referralTriggers": string[], "riskFlags": string[], "rationale": string
        }
        Higher riskScore = more scrutiny needed.

        RECORDS: {{JsonSerializer.Serialize(rec, JsonOpts)}}
        """;

    private static string ResearchPrompt(ExtractedRecords rec, AppetiteDecision triage) =>
        $$"""
        Capture exposure and inbound demand signals for this insured (treat signals as analyst hypotheses,
        do not fabricate specific verifiable facts). Return JSON:
        {
          "accountOverview": string,
          "intentScore": number, "intentBand": string,
          "exposureHighlights": string[],
          "signals": [ { "category": "CatastropheExposure|LossHistory|IndustryHazard|FinancialStress|Regulatory|Growth", "headline": string, "detail": string, "sentiment": "Positive|Neutral|Adverse", "impact": "Low|Medium|High" } ],
          "recommendedQuestions": string[]
        }

        RECORDS: {{JsonSerializer.Serialize(rec, JsonOpts)}}
        TRIAGE: {{JsonSerializer.Serialize(triage, JsonOpts)}}
        """;

    private static string StudyPrompt(ExtractedRecords rec, AppetiteDecision triage, LeadResearch research) =>
        $$"""
        Produce an executive underwriting risk study with clear rationale, risk flags and next actions. Return JSON:
        {
          "title": string, "executiveSummary": string,
          "overallRecommendation": "Bind|Quote with conditions|Refer|Decline",
          "indicatedPremium": number, "pricingRationale": string,
          "keyRiskFlags": string[], "recommendedConditions": string[], "exclusions": string[],
          "sections": [ { "heading": string, "body": string } ],
          "nextActions": string[]
        }

        RECORDS: {{JsonSerializer.Serialize(rec, JsonOpts)}}
        TRIAGE: {{JsonSerializer.Serialize(triage, JsonOpts)}}
        RESEARCH: {{JsonSerializer.Serialize(research, JsonOpts)}}
        """;
}
