using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services.Foundry;

/// <summary>
/// Origination pipeline backed by a Microsoft Foundry prompt agent (Microsoft Agent Framework, hosted
/// in-process pattern via <c>AIProjectClient.AsAIAgent(...)</c>).
///
/// Four grounded prompt-agent calls, each returning a single JSON object:
///   1. EXTRACTION — read the email, emit Lead / Account / Opportunity + confidence.
///   2. TRIAGE     — classify, score, route, set priority/SLA, list risks.
///   3. RESEARCH   — synthesise firmographic overview + demand signals + talking points.
///   4. REPORT     — assemble an executive origination brief.
///
/// On ANY failure (missing config, auth, transient service error, malformed JSON) it transparently
/// falls back to the deterministic offline pipeline for the remaining stages and records the reason,
/// so the POC is always demonstrable.
/// </summary>
public sealed class FoundryIntakePipeline : IIntakePipeline
{
    private readonly FoundryOptions _options;
    private readonly OfflineIntakePipeline _offline;
    private readonly ILogger<FoundryIntakePipeline> _logger;

    public FoundryIntakePipeline(FoundryOptions options, OfflineIntakePipeline offline, ILogger<FoundryIntakePipeline> logger)
    {
        _options = options;
        _offline = offline;
        _logger = logger;
    }

    public string Name => "foundry";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<IntakeCase> RunAsync(InboundEmail email, CancellationToken ct = default)
    {
        var c = new IntakeCase { Email = email, Engine = Name, Status = "processing" };

        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Foundry not configured; using offline pipeline.");
            return await FallbackAll(email, "Foundry disabled/unconfigured — used deterministic offline pipeline.", ct);
        }

        AIAgent agent;
        try
        {
            agent = CreateAgent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Foundry agent; falling back to offline.");
            return await FallbackAll(email, $"Foundry agent init failed ({ex.GetType().Name}); used offline pipeline.", ct);
        }

        var emailText = $"FROM: {email.FromName} <{email.From}>\nCHANNEL: {email.Channel}\nSUBJECT: {email.Subject}\n\n{email.Body}";

        // 1) EXTRACTION
        try
        {
            c.Records = await StageAsync(c, "extraction", agent, ExtractionPrompt(emailText),
                () => _offline.ExtractRecords(email),
                r => $"Foundry extracted Lead '{r.Lead.FullName}', Account '{r.Account.CompanyName}' ({r.Account.Segment}); confidence {r.Confidence}%.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry extraction failed; switching to offline for the whole case.");
            return await FallbackAll(email, $"Foundry extraction failed ({ex.GetType().Name}); used offline pipeline. Detail: {Trunc(ex.Message, 160)}", ct);
        }

        // 2) TRIAGE
        c.Triage = await StageAsync(c, "triage", agent, TriagePrompt(emailText, c.Records),
            () => _offline.Triage(c.Records, email),
            t => $"Foundry triage: {t.Classification}/{t.Priority}, score {t.LeadScore}, queue {t.RoutingQueue}.",
            ct);

        // 3) RESEARCH
        c.Research = await StageAsync(c, "research", agent, ResearchPrompt(c.Records),
            () => _offline.Research(c.Records),
            r => $"Foundry research: {r.Signals.Count} signals, intent {r.IntentScore} ({r.BuyingStage}).",
            ct);

        // 4) REPORT
        c.Report = await StageAsync(c, "report", agent, ReportPrompt(c),
            () => _offline.BuildReport(c),
            r => $"Foundry report '{r.Title}' with {r.Sections.Count} sections.",
            ct);

        c.Status = c.Triage.Disqualified ? "disqualified" : "completed";
        return c;
    }

    // ---------------- Stage runner with per-stage offline fallback ----------------

    private async Task<T> StageAsync<T>(IntakeCase c, string stage, AIAgent agent, string prompt,
        Func<T> offlineFallback, Func<T, string> summarise, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var parsed = await RunJsonAsync<T>(agent, prompt, ct);
            if (parsed is null) throw new InvalidOperationException("Agent returned no parseable JSON.");
            sw.Stop();
            c.Trace.Add(new AgentTrace { Stage = stage, Agent = "foundry", DurationMs = sw.ElapsedMilliseconds, Summary = summarise(parsed) });
            return parsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Foundry stage '{Stage}' failed; using offline result for this stage.", stage);
            var fb = offlineFallback();
            c.Trace.Add(new AgentTrace { Stage = stage, Agent = "offline-fallback", DurationMs = sw.ElapsedMilliseconds, Summary = $"Foundry '{stage}' failed ({ex.GetType().Name}); used offline result." });
            return fb;
        }
    }

    private async Task<IntakeCase> FallbackAll(InboundEmail email, string reason, CancellationToken ct)
    {
        var c = await _offline.RunAsync(email, ct);
        c.Engine = "offline";
        c.Trace.Insert(0, new AgentTrace { Stage = "engine", Agent = "offline", Summary = reason });
        return c;
    }

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeploymentName,
            instructions:
                "You are an enterprise revenue-operations intake & origination specialist. You read inbound " +
                "sales emails and produce structured CRM records, triage decisions, account research, and " +
                "executive briefs. Always respond with ONLY a single valid JSON object matching the requested " +
                "schema — no markdown, no prose, no code fences. Be realistic and conservative; never invent " +
                "verifiable facts (treat research signals as analyst hypotheses).",
            name: _options.AgentName);
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

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ---------------- Prompts ----------------

    private static string ExtractionPrompt(string emailText) =>
        $$"""
        Extract CRM records from this inbound email. Return JSON with exactly:
        {
          "lead":   { "fullName": string, "title": string, "email": string, "phone": string, "seniority": "C-Level|VP|Director|Manager|IC|Unknown", "isDecisionMaker": bool, "preferredChannel": string },
          "account":{ "companyName": string, "domain": string, "industry": string, "segment": "SMB|Mid-Market|Enterprise|Strategic", "employeeBand": string, "region": "APAC|AMER|EMEA|Global", "country": string, "isExistingCustomer": bool },
          "opportunity": { "name": string, "productInterest": string, "useCase": string, "timeline": string, "estimatedAnnualValue": number, "currency": "USD", "budgetStatus": "Budgeted|Exploring|Unknown", "competitors": string[], "notes": string },
          "confidence": number,          // 0-100
          "missingFields": string[]      // fields you could not populate
        }

        EMAIL:
        {{emailText}}
        """;

    private static string TriagePrompt(string emailText, ExtractedRecords rec) =>
        $$"""
        Given these extracted records and the original email, produce a triage decision. Return JSON:
        {
          "classification": "New Business|Expansion|Renewal|Support|Spam/Disqualified",
          "priority": "P1|P2|P3",
          "leadScore": number,          // 0-100 = firmographic fit + buying intent
          "routingQueue": string,        // e.g. "Enterprise AE — APAC"
          "recommendedAction": string,
          "slaHours": number,
          "rationale": string,
          "riskFlags": string[],
          "disqualified": bool
        }

        RECORDS: {{JsonSerializer.Serialize(rec, JsonOpts)}}

        EMAIL:
        {{emailText}}
        """;

    private static string ResearchPrompt(ExtractedRecords rec) =>
        $$"""
        Act as a lead-management research analyst. Using the account/opportunity below, synthesise an account
        research note with demand signals. Treat signals as plausible analyst hypotheses (do NOT fabricate
        specific verifiable facts like exact funding amounts). Return JSON:
        {
          "companyOverview": string,
          "signals": [ { "title": string, "category": "Hiring|Funding|TechAdoption|Expansion|Leadership|Regulatory", "detail": string, "source": string, "recency": string, "strength": number } ],
          "keyInitiatives": string[],
          "talkingPoints": string[],
          "buyingStage": "Awareness|Consideration|Decision",
          "intentScore": number          // 0-100
        }

        ACCOUNT+OPP: {{JsonSerializer.Serialize(rec, JsonOpts)}}
        """;

    private static string ReportPrompt(IntakeCase c) =>
        $$"""
        Produce an executive origination brief from the full case below. Return JSON:
        {
          "title": string,
          "executiveSummary": string,
          "highlights": string[],
          "recommendations": string[],
          "sections": [ { "heading": string, "body": string } ],
          "nextBestAction": string,
          "generatedBy": "foundry"
        }

        CASE: {{JsonSerializer.Serialize(new { c.Records, c.Triage, c.Research, c.Email }, JsonOpts)}}
        """;
}
