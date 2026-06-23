namespace Proj43.FinOps.Web.Services.Foundry;

/// <summary>
/// Live record of the Microsoft Foundry agent the app provisions and uses. This is the evidence
/// surface for "a real Foundry agent was created and the chat uses it":
///   . <see cref="Provisioned"/> + <see cref="AgentId"/> prove the agent resource was created in the
///     Foundry project (the id returned by <c>AsAIAgent(...)</c>).
///   . <see cref="FabricToolWired"/> proves the real Microsoft Fabric data-agent tool is attached
///     (not a function stub).
///   . <see cref="LastEngineUsed"/> proves whether the most recent chat turn was actually answered by
///     the Foundry agent path or fell back to the deterministic offline engine.
/// Surfaced via <c>/api/health</c> and <c>/api/agent</c>.
/// </summary>
public sealed class FoundryAgentDiagnostics
{
    private readonly object _gate = new();

    /// <summary>True once the Foundry agent has been created in the project.</summary>
    public bool Provisioned { get; private set; }

    /// <summary>The created agent's id (proof of a real Foundry agent resource).</summary>
    public string? AgentId { get; private set; }

    /// <summary>The created agent's name.</summary>
    public string? AgentName { get; private set; }

    /// <summary>Model deployment the agent runs on.</summary>
    public string? Model { get; private set; }

    /// <summary>True when the real Microsoft Fabric data-agent tool is attached to the agent.</summary>
    public bool FabricToolWired { get; private set; }

    /// <summary>Fabric Foundry connection id the tool is bound to.</summary>
    public string? FabricConnectionId { get; private set; }

    /// <summary>Number of MCP tools attached (optional path).</summary>
    public int McpToolCount { get; private set; }

    /// <summary>UTC time the agent was provisioned.</summary>
    public DateTimeOffset? ProvisionedAtUtc { get; private set; }

    /// <summary>"foundry" or "offline" - which engine actually served the last chat turn.</summary>
    public string? LastEngineUsed { get; private set; }

    /// <summary>Last error (exception type) that forced an offline fallback, if any.</summary>
    public string? LastError { get; private set; }

    /// <summary>Record a successful agent provisioning.</summary>
    public void RecordProvisioned(string? agentId, string? agentName, string? model, bool fabricToolWired, string? fabricConnectionId, int mcpToolCount)
    {
        lock (_gate)
        {
            Provisioned = true;
            AgentId = agentId;
            AgentName = agentName;
            Model = model;
            FabricToolWired = fabricToolWired;
            FabricConnectionId = fabricConnectionId;
            McpToolCount = mcpToolCount;
            ProvisionedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    /// <summary>Record which engine served a chat turn (and any fallback error).</summary>
    public void RecordTurn(string engine, string? error = null)
    {
        lock (_gate)
        {
            LastEngineUsed = engine;
            if (error is not null) LastError = error;
        }
    }

    /// <summary>Immutable snapshot for serialization in health/diagnostic endpoints.</summary>
    public object Snapshot()
    {
        lock (_gate)
        {
            return new
            {
                provisioned = Provisioned,
                agentId = AgentId,
                agentName = AgentName,
                model = Model,
                fabricToolWired = FabricToolWired,
                fabricConnectionId = FabricConnectionId,
                mcpToolCount = McpToolCount,
                provisionedAtUtc = ProvisionedAtUtc,
                lastEngineUsed = LastEngineUsed,
                lastError = LastError,
            };
        }
    }
}
