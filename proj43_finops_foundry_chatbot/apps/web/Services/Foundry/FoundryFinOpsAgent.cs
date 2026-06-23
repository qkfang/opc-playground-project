using System.Runtime.CompilerServices;
using System.Text;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using Proj43.FinOps.Web.Models;
using ChatResponse = Proj43.FinOps.Web.Models.ChatResponse;

namespace Proj43.FinOps.Web.Services.Foundry;

/// <summary>
/// Live FinOps assistant backed by a Microsoft Foundry agent (Microsoft Agent Framework, hosted
/// in-process via <c>AIProjectClient.AsAIAgent(...)</c>).
///
/// Capabilities wired here:
///   • Multi-turn conversation via <see cref="AgentSession"/> (one session per conversation id).
///   • Streaming responses via <c>RunStreamingAsync(message, session)</c> → surfaced as SSE tokens.
///   • Microsoft Fabric data access as a tool, two ways (selectable by config):
///       1. Fabric data agent tool — attached through the Foundry project connection
///          (<see cref="FabricOptions.ConnectionId"/>); identity passthrough (OBO) per Microsoft docs.
///       2. Fabric MCP tools — connected via <see cref="McpClient"/> (stdio), <c>ListToolsAsync()</c>
///          → cast to <see cref="AITool"/> and passed to the agent.
///
/// Resilience: if Foundry/Fabric is unconfigured or any runtime call fails (auth, quota, transient),
/// this transparently delegates to <see cref="OfflineFinOpsAgent"/> so the chat never dead-ends. The
/// offline engine produces the same FinOps answers over the seeded dataset, so the POC is always usable.
/// </summary>
public sealed class FoundryFinOpsAgent : IFinOpsAgent
{
    private readonly FoundryOptions _foundry;
    private readonly FabricOptions _fabric;
    private readonly McpOptions _mcp;
    private readonly OfflineFinOpsAgent _offline;
    private readonly ConversationStore _store;
    private readonly MarkdownRenderer _md;
    private readonly FoundryAgentDiagnostics _diag;
    private readonly ILogger<FoundryFinOpsAgent> _logger;

    // Lazily-created singletons for the live path.
    private AIAgent? _agent;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    // One Foundry AgentSession per conversation id (multi-turn memory on the service side).
    private readonly Dictionary<string, AgentSession> _sessions = new();

    public FoundryFinOpsAgent(
        FoundryOptions foundry, FabricOptions fabric, McpOptions mcp,
        OfflineFinOpsAgent offline, ConversationStore store, MarkdownRenderer md,
        FoundryAgentDiagnostics diag, ILogger<FoundryFinOpsAgent> logger)
    {
        _foundry = foundry;
        _fabric = fabric;
        _mcp = mcp;
        _offline = offline;
        _store = store;
        _md = md;
        _diag = diag;
        _logger = logger;
    }

    /// <summary>Live diagnostics: created agent id, Fabric-tool wiring, last engine used.</summary>
    public FoundryAgentDiagnostics Diagnostics => _diag;

    public string Name => "foundry";

    public async Task<ChatResponse> ReplyAsync(string conversationId, string message, CancellationToken ct = default)
    {
        var id = _store.EnsureConversation(conversationId);
        var sb = new StringBuilder();
        string tool = "fabric";
        await foreach (var ev in StreamAsync(id, message, ct))
        {
            if (ev.Type == "token" && ev.Data is { } d) sb.Append(d);
            if (ev.Tool is { } t) tool = t;
        }
        var text = sb.ToString();
        return new ChatResponse
        {
            ConversationId = id,
            Reply = text,
            ReplyHtml = _md.ToHtml(text),
            Engine = Name,
            Tool = tool,
        };
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
        string conversationId, string message, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var id = _store.EnsureConversation(conversationId);

        // If not configured for live, defer entirely to the offline engine.
        if (!_foundry.IsConfigured)
        {
            await foreach (var ev in _offline.StreamAsync(id, message, ct)) yield return ev;
            yield break;
        }

        AIAgent? agent = null;
        AgentSession? session = null;
        string? initError = null;
        try
        {
            agent = await EnsureAgentAsync(ct);
            session = await EnsureSessionAsync(agent, id, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry agent init failed; falling back to offline engine.");
            initError = ex.GetType().Name;
        }

        if (agent is null || session is null)
        {
            _diag.RecordTurn("offline", initError ?? "init");
            yield return new ChatStreamEvent { Type = "status", Data = $"Live agent unavailable ({initError ?? "init"}); using offline FinOps engine.", ConversationId = id };
            await foreach (var ev in _offline.StreamAsync(id, message, ct)) yield return ev;
            yield break;
        }

        _store.Add(id, "user", message);
        yield return new ChatStreamEvent { Type = "meta", ConversationId = id, Engine = Name, Tool = "fabric", Data = _diag.AgentId };
        yield return new ChatStreamEvent { Type = "status", Data = $"Asking the Foundry agent ({_diag.AgentId ?? _foundry.AgentName}) — querying Microsoft Fabric…", ConversationId = id };

        // Stream tokens; on any mid-stream failure, fall back to offline for a complete answer.
        var sb = new StringBuilder();
        IAsyncEnumerator<AgentResponseUpdate>? e = null;
        bool failed = false;
        try
        {
            e = agent.RunStreamingAsync(message, session, cancellationToken: ct).GetAsyncEnumerator(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Foundry streaming start failed; falling back to offline.");
            failed = true;
        }

        if (e is not null)
        {
            while (true)
            {
                AgentResponseUpdate? update = null;
                try
                {
                    if (!await e.MoveNextAsync()) break;
                    update = e.Current;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Foundry streaming failed mid-response; falling back to offline.");
                    failed = true;
                    break;
                }
                var text = update?.Text;
                if (!string.IsNullOrEmpty(text))
                {
                    sb.Append(text);
                    yield return new ChatStreamEvent { Type = "token", Data = text, ConversationId = id };
                }
            }
            await e.DisposeAsync();
        }

        if (failed || sb.Length == 0)
        {
            // Reset partial output and let the offline engine answer fully.
            _diag.RecordTurn("offline", failed ? "stream_failed" : "empty_response");
            yield return new ChatStreamEvent { Type = "status", Data = "Falling back to offline FinOps engine…", ConversationId = id };
            await foreach (var ev in _offline.StreamAsync(id, message, ct)) yield return ev;
            yield break;
        }

        var final = sb.ToString();
        _store.Add(id, "assistant", final);
        _diag.RecordTurn("foundry");
        yield return new ChatStreamEvent { Type = "done", ConversationId = id, Engine = Name, Tool = "fabric", Data = _md.ToHtml(final) };
    }

    // ---------------- Live agent / session construction ----------------

    private async Task<AIAgent> EnsureAgentAsync(CancellationToken ct)
    {
        if (_agent is not null) return _agent;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_agent is not null) return _agent;

            var tools = new List<AITool>();
            int mcpCount = 0;

            // (1) Fabric MCP tools — generic "MCP access to Fabric" path (optional).
            if (_mcp.IsConfigured)
            {
                try
                {
                    var mcpTools = await ConnectMcpAsync(ct);
                    tools.AddRange(mcpTools.Cast<AITool>());
                    mcpCount = mcpTools.Count;
                    _logger.LogInformation("Attached {Count} Fabric MCP tool(s): {Names}",
                        mcpTools.Count, string.Join(", ", mcpTools.Select(t => t.Name)));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Fabric MCP connection failed; continuing without MCP tools.");
                }
            }

            // (2) Microsoft Fabric data-agent tool — the REAL hosted Foundry tool, bound to the
            //     published Fabric data agent via the Foundry project connection id. The Foundry
            //     service routes tool calls to Fabric (identity passthrough). This is not a stub.
            var fabricTool = FoundryAgentFactory.TryCreateFabricTool(_fabric);
            bool fabricWired = fabricTool is not null;
            if (fabricTool is not null)
            {
                tools.Add(fabricTool);
                _logger.LogInformation("Microsoft Fabric data-agent tool wired via connection {ConnectionId}", _fabric.ConnectionId);
            }
            else
            {
                _logger.LogWarning("No Fabric connection configured; the Foundry agent is created without the Fabric tool.");
            }

            var instructions = FoundryAgentFactory.BuildInstructions();

            // Create the Foundry agent in the project. AsAIAgent(...) provisions a server-side agent
            // (Responses-API backed) and returns its handle, including the agent Id we surface as proof.
            var client = new AIProjectClient(new Uri(_foundry.ProjectEndpoint!), new DefaultAzureCredential());
            _agent = client.AsAIAgent(
                model: _foundry.ModelDeploymentName,
                instructions: instructions,
                name: _foundry.AgentName,
                tools: tools);

            _diag.RecordProvisioned(
                agentId: _agent.Id,
                agentName: _agent.Name ?? _foundry.AgentName,
                model: _foundry.ModelDeploymentName,
                fabricToolWired: fabricWired,
                fabricConnectionId: _fabric.ConnectionId,
                mcpToolCount: mcpCount);
            _logger.LogInformation("Foundry agent provisioned: id={AgentId} name={AgentName} model={Model} fabricTool={Fabric} mcpTools={Mcp}",
                _agent.Id, _agent.Name, _foundry.ModelDeploymentName, fabricWired, mcpCount);
            return _agent;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<AgentSession> EnsureSessionAsync(AIAgent agent, string conversationId, CancellationToken ct)
    {
        lock (_sessions)
        {
            if (_sessions.TryGetValue(conversationId, out var existing)) return existing;
        }
        var session = await agent.CreateSessionAsync(ct);
        lock (_sessions)
        {
            _sessions[conversationId] = session;
        }
        return session;
    }

    private async Task<IList<McpClientTool>> ConnectMcpAsync(CancellationToken ct)
    {
        // Local stdio MCP server exposing Fabric query tools.
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = _mcp.ServerName,
            Command = _mcp.Command!,
            Arguments = _mcp.Args,
        });
        var mcpClient = await McpClient.CreateAsync(transport, cancellationToken: ct);
        return await mcpClient.ListToolsAsync(cancellationToken: ct);
    }
}
