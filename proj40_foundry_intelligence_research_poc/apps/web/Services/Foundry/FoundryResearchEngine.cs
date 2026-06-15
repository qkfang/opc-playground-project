using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services.Foundry;

/// <summary>
/// Intelligence &amp; research pipeline backed by a Microsoft Foundry prompt agent (Microsoft Agent
/// Framework, hosted in-process via <c>AIProjectClient.AsAIAgent(...)</c>).
///
/// Pipeline (grounded prompt-agent calls, each returning JSON):
///   1. ENTITIES  — read the email + attached document, extract key entities.
///   2. INSIGHTS  — generate insights from the email + document.
///   (3. SOURCES) — deterministic pull from the mocked corpus, keyed by the agent's entities.
///   4. RESEARCH  — synthesise a research brief from insights + the pulled sources.
///   5. REPORT    — compose a send-ready report email summarising the insights.
///
/// Stage 3 stays deterministic (the corpus is mocked and owned by us) so outputs are traceable. On ANY
/// failure (missing config, auth, transient error) the whole pipeline falls back to the offline engine,
/// so the POC is always demonstrable.
/// </summary>
public sealed class FoundryResearchEngine : IResearchEngine
{
    private readonly FoundryOptions _options;
    private readonly OfflineResearchEngine _offline;
    private readonly SourceCorpus _corpus;
    private readonly ILogger<FoundryResearchEngine> _logger;

    public FoundryResearchEngine(FoundryOptions options, OfflineResearchEngine offline, SourceCorpus corpus, ILogger<FoundryResearchEngine> logger)
    {
        _options = options;
        _offline = offline;
        _corpus = corpus;
        _logger = logger;
    }

    public string Name => "foundry";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };

    public async Task<ResearchCase> RunAsync(ResearchCase c, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
        {
            _logger.LogInformation("Foundry not configured; using offline engine.");
            await _offline.RunAsync(c, ct);
            c.AgentSteps.Insert(0, new AgentStepLog { Step = "engine", Summary = "Foundry disabled/unconfigured — used deterministic offline engine." });
            return c;
        }

        try
        {
            var agent = CreateAgent();
            var corpusText = BuildCorpus(c.Email);

            // 1) ENTITIES
            var entities = await RunJsonAsync<ExtractedEntities>(agent, EntitiesPrompt(corpusText), ct)
                           ?? throw new InvalidOperationException("Entities step returned no JSON.");
            if (entities.PrimaryOrganisation is not null && !entities.Organisations.Contains(entities.PrimaryOrganisation, StringComparer.OrdinalIgnoreCase))
                entities.Organisations.Insert(0, entities.PrimaryOrganisation);
            c.Entities = entities;
            c.AgentSteps.Add(new AgentStepLog { Step = "entities", Summary = $"Foundry agent extracted {c.Entities.AllKeyEntities.Count()} key entities (org: {c.Entities.PrimaryOrganisation ?? "n/a"})." });

            // 2) INSIGHTS
            var insightsWrap = await RunJsonAsync<InsightsWrapper>(agent, InsightsPrompt(corpusText), ct);
            c.Insights = insightsWrap?.Insights ?? new();
            c.AgentSteps.Add(new AgentStepLog { Step = "insights", Summary = $"Foundry agent generated {c.Insights.Count} insight(s)." });

            // 3) SOURCES — deterministic pull keyed by the agent's entities.
            c.SourceHits = _corpus.Pull(BuildEntityQuery(c.Entities));
            c.AgentSteps.Add(new AgentStepLog { Step = "sources", Summary = $"Pulled {c.SourceHits.Count} source record(s) ({c.SourceHits.Count(h => h.SourceType == "Internal")} internal, {c.SourceHits.Count(h => h.SourceType == "External")} external) keyed by extracted entities." });

            // 4) RESEARCH BRIEF
            var brief = await RunJsonAsync<ResearchBrief>(agent, ResearchPrompt(c), ct);
            c.Brief = brief ?? OfflineResearchEngine.BuildBrief(c);
            AttachCitations(c);
            c.AgentSteps.Add(new AgentStepLog { Step = "research", Summary = $"Foundry agent drafted a research brief ({c.Brief.KeyFindings.Count} findings, {c.Brief.Risks.Count} risks, {c.Brief.Opportunities.Count} opportunities)." });

            // 5) REPORT EMAIL
            var report = await RunJsonAsync<ReportEmail>(agent, ReportPrompt(c), ct);
            c.ReportEmail = report ?? OfflineResearchEngine.BuildReportEmail(c);
            FinalizeReport(c);
            c.AgentSteps.Add(new AgentStepLog { Step = "report-email", Summary = $"Foundry agent composed the report email for {c.ReportEmail.To}." });

            c.Engine = Name;
            return c;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry research failed; falling back to offline engine.");
            // Reset partial state and fall back so the user still gets a complete result.
            c.Entities = new(); c.Insights = new(); c.SourceHits = new(); c.Brief = new(); c.ReportEmail = new(); c.AgentSteps.Clear();
            await _offline.RunAsync(c, ct);
            c.AgentSteps.Insert(0, new AgentStepLog { Step = "engine", Summary = $"Foundry call failed ({ex.GetType().Name}); fell back to offline engine. Detail: {Trunc(ex.Message, 200)}" });
            return c;
        }
    }

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeploymentName,
            instructions:
                "You are an enterprise intelligence and research analyst. You read inbound customer emails and " +
                "their attached documents, extract key entities, generate grounded insights, and synthesise concise " +
                "research briefs and stakeholder-ready summary emails. Always respond with ONLY a single valid JSON " +
                "object matching the requested schema — no markdown, no prose, no code fences. Be precise, cite the " +
                "document, and never invent facts that are not supported by the supplied text or sources.",
            name: _options.AgentName);
    }

    private static string BuildCorpus(InboundEmail email)
    {
        var doc = email.Document is null
            ? "(no document attached)"
            : $"=== ATTACHED DOCUMENT: {email.Document.FileName} ({email.Document.DocType}, {email.Document.WordCount} words) ===\n{email.Document.Content}";
        var text =
            $"FROM: {email.FromName} <{email.From}>\nSUBJECT: {email.Subject}\n\nBODY:\n{email.Body}\n\n{doc}";
        return Trunc(text, 48_000);
    }

    private static List<string> BuildEntityQuery(ExtractedEntities x)
    {
        var q = new List<string>();
        if (x.PrimaryOrganisation is not null) q.Add(x.PrimaryOrganisation);
        q.AddRange(x.Organisations); q.AddRange(x.Topics); q.AddRange(x.Technologies);
        if (x.Industry is not null) q.Add(x.Industry.Split(' ', '&', '/')[0]);
        return q;
    }

    private static void AttachCitations(ResearchCase c)
    {
        if (c.Brief.Citations.Count > 0) return;
        int n = 1;
        foreach (var h in c.SourceHits)
            c.Brief.Citations.Add(new Citation { Marker = $"[S{n++}]", SourceName = h.SourceName, Title = h.Title, Url = h.Url });
        if (string.IsNullOrWhiteSpace(c.Brief.Title)) c.Brief.Title = $"Research Brief — {c.Entities.PrimaryOrganisation ?? "Inbound enquiry"}";
    }

    private static void FinalizeReport(ResearchCase c)
    {
        var r = c.ReportEmail;
        if (string.IsNullOrWhiteSpace(r.Signature)) r.Signature = "— Intelligence & Research Agent (proj40)\nMicrosoft Foundry Intelligence & Research POC";
        if (string.IsNullOrWhiteSpace(r.Greeting)) r.Greeting = "Hi team,";
        if (string.IsNullOrWhiteSpace(r.To)) r.To = "sales-desk@contoso.com";
        if (string.IsNullOrWhiteSpace(r.Subject)) r.Subject = $"Inbound intelligence: {c.Entities.PrimaryOrganisation ?? "enquiry"}";
        // Always (re)render with our deterministic envelope + sources for traceability.
        r.RenderedMarkdown = RenderEmailEnvelope(r, c);
    }

    private static string RenderEmailEnvelope(ReportEmail e, ResearchCase c)
    {
        var sb = new System.Text.StringBuilder();
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

    private async Task<T?> RunJsonAsync<T>(AIAgent agent, string prompt, CancellationToken ct)
    {
        var response = await agent.RunAsync(prompt, cancellationToken: ct);
        var json = ExtractJsonObject(response.Text ?? string.Empty);
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOpts);
    }

    private static string? ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text.Substring(start, end - start + 1);
    }

    // ---------------- Prompts ----------------

    private static string EntitiesPrompt(string corpus) =>
        $$"""
        Read the inbound customer email and its attached document. Extract the KEY ENTITIES.

        Return JSON with exactly these fields:
        {
          "primaryOrganisation": string,
          "organisations": string[],
          "people": string[],
          "topics": string[],
          "technologies": string[],
          "locations": string[],
          "monetaryAmounts": string[],
          "dates": string[],
          "industry": string,
          "intent": string
        }

        EMAIL + DOCUMENT:
        {{corpus}}
        """;

    private static string InsightsPrompt(string corpus) =>
        $$"""
        From the same email and document, generate 4-7 INSIGHTS a sales/research team would care about.

        Return JSON: { "insights": [ {
          "headline": string,
          "detail": string,
          "category": "Need|Risk|Opportunity|Context|Signal",
          "confidence": "High|Medium|Low",
          "evidence": string
        } ] }

        Ground every insight in the text; the "evidence" field must say what supports it.

        EMAIL + DOCUMENT:
        {{corpus}}
        """;

    private static string ResearchPrompt(ResearchCase c) =>
        $$"""
        You are the Research Agent. Synthesise a RESEARCH BRIEF from the insights and the pulled sources.

        INSIGHTS: {{JsonSerializer.Serialize(c.Insights, JsonOpts)}}

        PULLED SOURCES (cite by their order as [S1], [S2], ...): {{JsonSerializer.Serialize(c.SourceHits.Select((h, i) => new { marker = $"[S{i + 1}]", h.SourceType, h.SourceName, h.Title, h.Snippet }), JsonOpts)}}

        Return JSON: {
          "title": string,
          "executiveSummary": string,
          "keyFindings": string[],
          "risks": string[],
          "opportunities": string[],
          "recommendedActions": string[],
          "openQuestions": string[],
          "confidence": "High|Medium|Low"
        }
        Reference source markers inline in findings where relevant. Be concise and decision-useful.
        """;

    private static string ReportPrompt(ResearchCase c) =>
        $$"""
        Compose a send-ready internal REPORT EMAIL that summarises the insights for the relevant sales vertical.

        ORG: {{c.Entities.PrimaryOrganisation}}
        INDUSTRY: {{c.Entities.Industry}}
        BRIEF: {{JsonSerializer.Serialize(c.Brief, JsonOpts)}}

        Return JSON: {
          "to": string,        // an internal distribution address appropriate to the industry
          "cc": string,
          "subject": string,
          "greeting": string,
          "body": string,      // plain text, short sections (What they want / Key findings / Why we can win / Watch-outs / Next steps)
          "callToAction": string,
          "signature": string
        }
        Keep it skimmable and professional. Do not invent facts beyond the brief.
        """;

    private static string Trunc(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    // ---------------- DTO ----------------
    private sealed class InsightsWrapper
    {
        [JsonPropertyName("insights")] public List<Insight> Insights { get; set; } = new();
    }
}
