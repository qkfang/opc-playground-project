using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Proj45.RelayDesk.Web.Models;
using Proj45.RelayDesk.Web.Services.Mcp;

namespace Proj45.RelayDesk.Web.Services.Foundry;

/// <summary>
/// Inbound-email orchestration pipeline backed by Microsoft Foundry prompt agents (Microsoft Agent
/// Framework, in-process via <c>AIProjectClient.AsAIAgent(...)</c> + <c>agent.RunAsync(...)</c>).
///
/// Five grounded prompt-agent calls, each returning a single JSON object:
///   1. EXTRACTION — read the email, emit a normalized structured record + entities + confidence.
///   2. TRIAGE     — category, urgency, sentiment, spam, SLA + triage confidence.
///   3. INTENT     — purpose, confidence, alternatives, and human-review routing.
///   4. TASK       — plan the downstream D365 operation (the MCP lookups + the operation execution
///                   are performed deterministically by the pipeline, not the model).
///   5. OUTCOME    — final status, drafted reply, audit trail, next actions.
///
/// On ANY failure (missing config, auth, transient error, malformed JSON) each stage transparently
/// falls back to the deterministic offline pipeline and records the reason, so the POC always runs.
/// </summary>
public sealed class FoundryEmailPipeline : IEmailPipeline
{
    private readonly FoundryOptions _options;
    private readonly OfflineEmailPipeline _offline;
    private readonly ID365McpServer _mcp;
    private readonly HumanReviewQueue _queue;
    private readonly ILogger<FoundryEmailPipeline> _logger;

    public string Name => "foundry";

    private static int _seq = 45_500;

    public FoundryEmailPipeline(FoundryOptions options, OfflineEmailPipeline offline, ID365McpServer mcp,
        HumanReviewQueue queue, ILogger<FoundryEmailPipeline> logger)
    {
        _options = options;
        _offline = offline;
        _mcp = mcp;
        _queue = queue;
        _logger = logger;
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<EmailCase> RunAsync(IncomingEmail email, CancellationToken ct = default)
    {
        if (!_options.IsConfigured)
            return await _offline.RunAsync(email, ct);

        AIAgent agent;
        try { agent = CreateAgent(); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Foundry agent; falling back to offline pipeline.");
            var off = await _offline.RunAsync(email, ct);
            off.Trace.Insert(0, new AgentStep { Stage = "Engine", Agent = "offline", Engine = "offline", Summary = $"Foundry init failed ({ex.GetType().Name}); offline pipeline used." });
            return off;
        }

        var trace = new List<AgentStep>();
        var emailText = $"MAILBOX: {email.Mailbox}\nFROM: {email.FromName} <{email.From}>\nCHANNEL: {email.Channel}\nSUBJECT: {email.Subject}\nATTACHMENTS: {string.Join(", ", email.Attachments)}\n\n{email.Body}";

        // 1) EXTRACTION
        var extraction = await StageAsync(trace, "Extraction", AgentInstructions.Extraction.Name, agent,
            ExtractionPrompt(emailText),
            () => _offline.ExtractStage(email),
            e => $"Extracted {e.Entities.Count} entities, {e.OrderRefs.Count} ref(s); language {e.Language}.",
            e => "extraction", e => e.ExtractionConfidence, ct);

        // 2) TRIAGE
        var triage = await StageAsync(trace, "Triage", AgentInstructions.Triage.Name, agent,
            TriagePrompt(emailText, extraction),
            () => _offline.TriageStage(extraction, email),
            t => $"{t.Category} / {t.Urgency}; sentiment {t.Sentiment}; spam {t.SpamRisk:0.00}.",
            t => $"{t.Category} ({t.Urgency})", t => t.TriageConfidence, ct);

        // 3) INTENT
        var intent = await StageAsync(trace, "Intent", AgentInstructions.Intent.Name, agent,
            IntentPrompt(emailText, extraction, triage),
            () => _offline.IntentStage(extraction, triage),
            i => i.RequiresHuman ? $"Uncertain → human ({i.Intent} {i.IntentConfidence:0.00})." : $"{i.Intent} ({i.IntentConfidence:0.00}).",
            i => i.RequiresHuman ? "Route to human" : i.Intent, i => i.IntentConfidence, ct);

        // Enforce the business threshold regardless of what the model returned.
        ApplyHumanThreshold(intent);

        // 4) TASK — deterministic MCP lookups + LLM operation plan + deterministic execution.
        var task = await RunTaskStageAsync(trace, agent, email, extraction, triage, intent, ct);

        // 5) OUTCOME
        var outcome = await StageAsync(trace, "Outcome", AgentInstructions.Outcome.Name, agent,
            OutcomePrompt(email, extraction, triage, intent, task),
            () => _offline.OutcomeStage(email, extraction, triage, intent, task),
            o => $"{o.FinalStatus}; {o.AuditTrail.Count} audit entries.",
            o => o.FinalStatus, o => null, ct);

        var anyFoundry = trace.Any(t => t.Engine == "foundry");
        var caseObj = new EmailCase
        {
            Reference = $"RLY-{DateTimeOffset.UtcNow:yyyy}-{Interlocked.Increment(ref _seq):D5}",
            Status = _offline.StatusFor(triage, intent),
            Engine = anyFoundry ? "foundry" : "offline",
            Source = email,
            Extraction = extraction,
            Triage = triage,
            Intent = intent,
            Task = task,
            Outcome = outcome,
            Trace = trace
        };
        _offline.EnqueueHumanIfNeeded(caseObj);
        return caseObj;
    }

    private void ApplyHumanThreshold(IntentDecision intent)
    {
        if (intent.Intent.Equals("Spam / No action", StringComparison.OrdinalIgnoreCase)) return;
        if (intent.IntentConfidence < _offline.Threshold)
        {
            intent.RequiresHuman = true;
            if (string.IsNullOrWhiteSpace(intent.HumanReason))
                intent.HumanReason = $"Intent confidence {intent.IntentConfidence:0.00} below threshold {_offline.Threshold:0.00}.";
            intent.SuggestedQueue = "Human Review";
            intent.IntentBand = "Low";
        }
    }

    // ---- TASK stage: lookups (MCP) are deterministic; only the operation PLAN is agent-driven. ----
    private async Task<TaskExecution> RunTaskStageAsync(List<AgentStep> trace, AIAgent agent, IncomingEmail email,
        EmailExtraction x, TriageResult triage, IntentDecision intent, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        if (triage.Category == "Spam" || intent.RequiresHuman)
        {
            // Defer to the offline behaviour (skip/hold) — no live call needed.
            var fb = _offline.TaskStage(email, x, triage, intent);
            sw.Stop();
            trace.Add(new AgentStep { Stage = "Task", Agent = AgentInstructions.Task.Name, Engine = "offline", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Decision = fb.Plan.Operation, Summary = $"{fb.Customer.MatchNote}; {fb.ToolCalls.Count} MCP call(s); {fb.ExecutionStatus}." });
            return fb;
        }

        var exec = new TaskExecution();
        // Deterministic MCP context lookups (these are tool calls, not the model).
        exec.ToolCalls.Add(_mcp.Invoke("customer.search", new Dictionary<string, string> { ["query"] = x.AccountHints.FirstOrDefault() ?? email.FromName }));
        var account = _mcp.ResolveAccount(x.AccountHints, email.From);

        if (account is null)
        {
            var fb = _offline.TaskStage(email, x, triage, intent);
            sw.Stop();
            trace.Add(new AgentStep { Stage = "Task", Agent = AgentInstructions.Task.Name, Engine = "offline", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Decision = fb.Plan.Operation, Summary = $"{fb.Customer.MatchNote}; offline plan used (no account match)." });
            return fb;
        }

        exec.ToolCalls.Add(_mcp.Invoke("account.get", new Dictionary<string, string> { ["accountId"] = account.AccountId }));
        exec.ToolCalls.Add(_mcp.Invoke("contact.get", new Dictionary<string, string> { ["accountId"] = account.AccountId }));
        var preLookup = intent.Intent switch
        {
            "Billing Dispute" or "Technical Issue" or "Complaint Escalation" => "service.cases.list",
            "Cancellation Request" or "Sales Enquiry" => "opportunity.list",
            _ => null
        };
        if (preLookup is not null)
            exec.ToolCalls.Add(_mcp.Invoke(preLookup, new Dictionary<string, string> { ["accountId"] = account.AccountId }));

        exec.Customer = new CustomerContext
        {
            AccountId = account.AccountId, AccountName = account.Name, Tier = account.Tier, Industry = account.Industry,
            AnnualValue = account.AnnualValue, Owner = account.Owner, Status = account.Status,
            PrimaryContact = account.Contacts.FirstOrDefault(c => c.Primary)?.Name ?? account.Contacts.FirstOrDefault()?.Name ?? "",
            OpenOpportunities = account.Opportunities.Count,
            OpenServiceCases = account.ServiceCases.Count(c => c.Status == "Open"),
            Matched = true,
            MatchNote = $"Matched {account.Name} ({account.AccountId})."
        };

        // Ask the agent to PLAN the operation, grounded on the retrieved context.
        TaskPlan plan;
        var engine = "foundry";
        try
        {
            var ctxJson = JsonSerializer.Serialize(new
            {
                intent = intent.Intent,
                account = new { account.AccountId, account.Name, account.Tier, account.Industry, account.Status, account.Owner, contractMonthly = account.ContractMonthly, annualValue = account.AnnualValue },
                openServiceCases = account.ServiceCases.Count(c => c.Status == "Open"),
                openOpportunities = account.Opportunities.Count,
                urgency = triage.Urgency
            }, JsonOpts);
            var planned = await RunJsonAsync<TaskPlan>(agent, TaskPlanPrompt(ctxJson), ct);
            if (planned is null || string.IsNullOrWhiteSpace(planned.PlannedTool)) throw new InvalidOperationException("No usable plan JSON.");
            // Always bind the operation to the resolved account id (never trust a model-provided id).
            planned.OperationArgs ??= new();
            planned.OperationArgs["accountId"] = account.AccountId;
            plan = planned;
        }
        catch (Exception ex)
        {
            engine = "offline";
            _logger.LogWarning(ex, "Foundry task-plan failed; using offline plan.");
            var fb = _offline.TaskStage(email, x, triage, intent);
            plan = fb.Plan;
            plan.OperationArgs["accountId"] = account.AccountId;
        }

        exec.Plan = plan;

        // Deterministic execution of the chosen mock MCP operation.
        if (!string.IsNullOrWhiteSpace(plan.PlannedTool))
        {
            var opCall = _mcp.Invoke(plan.PlannedTool, plan.OperationArgs);
            exec.ToolCalls.Add(opCall);
            exec.ExecutionStatus = plan.RequiresApproval ? "simulated" : (opCall.Ok ? "executed" : "simulated");
            exec.OperationReference = ExtractRef(opCall.ResultJson);
            exec.OperationResult = plan.RequiresApproval ? $"{plan.Operation} prepared (requires approval): {opCall.ResultSummary}" : opCall.ResultSummary;
        }
        else exec.ExecutionStatus = "simulated";

        sw.Stop();
        trace.Add(new AgentStep
        {
            Stage = "Task", Agent = AgentInstructions.Task.Name, Engine = engine,
            DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds),
            Decision = plan.Operation,
            Summary = $"{exec.Customer.MatchNote}; {exec.ToolCalls.Count} MCP call(s); {exec.ExecutionStatus} {plan.Operation}."
        });
        return exec;
    }

    private static string ExtractRef(string resultJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(resultJson);
            foreach (var prop in new[] { "caseId", "creditMemoId", "callbackId", "churnSignalId" })
                if (doc.RootElement.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString() ?? "";
        }
        catch { }
        return "";
    }

    // ---------------- Generic stage runner with per-stage offline fallback ----------------

    private async Task<T> StageAsync<T>(List<AgentStep> trace, string stage, string agentName, AIAgent agent, string prompt,
        Func<T> offlineFallback, Func<T, string> summarise, Func<T, string> decide, Func<T, double?> conf, CancellationToken ct) where T : class
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var parsed = await RunJsonAsync<T>(agent, prompt, ct);
            if (parsed is null) throw new InvalidOperationException("Agent returned no parseable JSON.");
            sw.Stop();
            trace.Add(new AgentStep { Stage = stage, Agent = agentName, Engine = "foundry", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Decision = decide(parsed), Confidence = conf(parsed), Summary = summarise(parsed) });
            return parsed;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Foundry stage '{Stage}' failed; using offline result for this stage.", stage);
            var fb = offlineFallback();
            var reason = ex.Message.Length > 160 ? ex.Message[..160] : ex.Message;
            trace.Add(new AgentStep { Stage = stage, Agent = "offline-fallback", Engine = "offline", DurationMs = (int)Math.Max(1, sw.ElapsedMilliseconds), Decision = decide(fb), Confidence = conf(fb), Summary = $"Foundry '{stage}' failed ({ex.GetType().Name}: {reason}); offline result used." });
            return fb;
        }
    }

    // ---------------- Live diagnostics probe ----------------

    public async Task<EngineDiagnostics> ProbeAsync(CancellationToken ct = default)
    {
        var diag = new EngineDiagnostics
        {
            FoundryEnabled = _options.Enabled,
            FoundryConfigured = _options.IsConfigured,
            EndpointHost = EndpointHost(_options.ProjectEndpoint),
            ModelDeployment = _options.ModelDeployment
        };

        if (!_options.IsConfigured)
        {
            diag.FoundryMode = "offline";
            diag.Detail = _options.Enabled ? "Foundry enabled but ProjectEndpoint is not set." : "Foundry disabled (Foundry__Enabled=false).";
            return diag;
        }

        AIAgent agent;
        try { agent = CreateAgent(); }
        catch (Exception ex)
        {
            diag.FoundryMode = "error";
            diag.Detail = $"Agent init failed: {ex.GetType().Name}.";
            _logger.LogError(ex, "Foundry probe: agent initialisation failed.");
            return diag;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            probeCts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 60)));
            var response = await agent.RunAsync("Reply with exactly the word: READY", cancellationToken: probeCts.Token);
            sw.Stop();
            var text = (response.Text ?? string.Empty).Trim();
            diag.ProbeMs = (int)Math.Max(1, sw.ElapsedMilliseconds);
            if (text.Length == 0) { diag.FoundryMode = "fallback"; diag.Detail = "Live agent returned an empty response."; }
            else { diag.FoundryMode = "live"; diag.FoundryLive = true; diag.Detail = "Live Foundry agent round-trip succeeded."; }
        }
        catch (Exception ex)
        {
            sw.Stop();
            diag.ProbeMs = (int)Math.Max(1, sw.ElapsedMilliseconds);
            diag.FoundryMode = "fallback";
            diag.Detail = $"Live agent call failed: {ex.GetType().Name}.";
            _logger.LogWarning(ex, "Foundry probe: live agent round-trip failed.");
        }
        return diag;
    }

    private static string? EndpointHost(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return null;
        return Uri.TryCreate(endpoint, UriKind.Absolute, out var u) ? u.Host : null;
    }

    private AIAgent CreateAgent()
    {
        var client = new AIProjectClient(new Uri(_options.ProjectEndpoint!), new DefaultAzureCredential());
        return client.AsAIAgent(
            model: _options.ModelDeployment,
            instructions: AgentInstructions.SystemPreamble,
            name: "relay-desk-agent");
    }

    private async Task<T?> RunJsonAsync<T>(AIAgent agent, string prompt, CancellationToken ct)
    {
        var response = await agent.RunAsync(prompt, cancellationToken: ct);
        var json = ExtractJsonObject(response.Text ?? string.Empty);
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, LenientJson.Options);
    }

    private static string? ExtractJsonObject(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        if (start < 0 || end <= start) return null;
        return text.Substring(start, end - start + 1);
    }

    // ---------------- Prompts ----------------

    private static string ExtractionPrompt(string emailText) =>
        $"{AgentInstructions.Extraction.Instructions}\n\nEMAIL:\n{emailText}";

    private static string TriagePrompt(string emailText, EmailExtraction x) =>
        $"{AgentInstructions.Triage.Instructions}\n\nEMAIL:\n{emailText}\n\nEXTRACTION: {JsonSerializer.Serialize(x, JsonOpts)}";

    private static string IntentPrompt(string emailText, EmailExtraction x, TriageResult t) =>
        $"{AgentInstructions.Intent.Instructions}\n\nEMAIL:\n{emailText}\n\nEXTRACTION: {JsonSerializer.Serialize(x, JsonOpts)}\nTRIAGE: {JsonSerializer.Serialize(t, JsonOpts)}";

    private static string TaskPlanPrompt(string contextJson) =>
        $"{AgentInstructions.Task.Instructions}\n\nRETRIEVED CONTEXT (from D365 MCP lookups): {contextJson}";

    private static string OutcomePrompt(IncomingEmail email, EmailExtraction x, TriageResult t, IntentDecision i, TaskExecution k) =>
        $"{AgentInstructions.Outcome.Instructions}\n\n" +
        $"EMAIL_FROM: {email.FromName} <{email.From}>\n" +
        $"TRIAGE: {JsonSerializer.Serialize(t, JsonOpts)}\n" +
        $"INTENT: {JsonSerializer.Serialize(i, JsonOpts)}\n" +
        $"TASK: {JsonSerializer.Serialize(new { k.Customer, k.Plan, k.ExecutionStatus, k.OperationReference, k.OperationResult, toolCalls = k.ToolCalls.Select(c => new { c.Tool, c.ResultSummary }) }, JsonOpts)}";
}
