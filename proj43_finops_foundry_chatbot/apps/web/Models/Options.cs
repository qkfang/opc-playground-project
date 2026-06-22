namespace Proj43.FinOps.Web.Models;

/// <summary>
/// Configuration for the Microsoft Foundry agent that powers the FinOps chatbot.
/// When <see cref="Enabled"/> is false (or endpoint missing) the app uses the deterministic offline
/// FinOps agent, so the POC is fully runnable without live Azure/Fabric access.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. If false, always use the offline agent.</summary>
    public bool Enabled { get; set; }

    /// <summary>Foundry project endpoint, e.g. https://&lt;name&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name used for agent orchestration, e.g. gpt-5.4.</summary>
    public string ModelDeploymentName { get; set; } = "gpt-5.4";

    /// <summary>Name for the agent created at runtime.</summary>
    public string AgentName { get; set; } = "proj43-finops-assistant";

    /// <summary>True when configuration is sufficient to attempt a live run.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>
/// Microsoft Fabric data agent connection used as a tool by the Foundry agent (identity passthrough).
/// See: Consume Fabric data agent from Microsoft Foundry (preview).
/// </summary>
public sealed class FabricOptions
{
    public const string SectionName = "Fabric";

    /// <summary>
    /// Foundry connection ID for the published Fabric data agent, of the form
    /// /subscriptions/.../providers/Microsoft.CognitiveServices/accounts/&lt;foundry&gt;/projects/&lt;project&gt;/connections/&lt;name&gt;.
    /// When set, the Foundry agent attaches the Fabric data agent tool.
    /// </summary>
    public string? ConnectionId { get; set; }

    /// <summary>Optional Fabric workspace GUID (for diagnostics / display only).</summary>
    public string? WorkspaceId { get; set; }

    /// <summary>Optional Fabric data agent artifact GUID (for diagnostics / display only).</summary>
    public string? ArtifactId { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionId);
}

/// <summary>
/// Model Context Protocol server exposing Fabric query tools, attached to the Foundry agent.
/// Supports a local stdio server (Command/Args) — the generic "MCP access to Fabric" path.
/// </summary>
public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>Enable attaching MCP tools to the agent.</summary>
    public bool Enabled { get; set; }

    /// <summary>Friendly name for the MCP server connection.</summary>
    public string ServerName { get; set; } = "fabric-mcp";

    /// <summary>Executable to launch the stdio MCP server, e.g. "npx" or a dotnet tool.</summary>
    public string? Command { get; set; }

    /// <summary>Arguments for the MCP server command.</summary>
    public string[] Args { get; set; } = Array.Empty<string>();

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(Command);
}

/// <summary>Chat behaviour knobs.</summary>
public sealed class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>Max prior turns (user+assistant pairs) kept per conversation for context.</summary>
    public int MaxHistoryTurns { get; set; } = 12;

    /// <summary>Persist transcripts to the local data folder.</summary>
    public bool PersistTranscripts { get; set; } = true;
}

/// <summary>Optional blob persistence of chat transcripts.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? AccountUrl { get; set; }
    public string ContainerName { get; set; } = "transcripts";

    /// <summary>Local folder for transcript persistence (App Service: /home/site/data).</summary>
    public string LocalDataFolder { get; set; } = "App_Data";

    public bool UseBlob => !string.IsNullOrWhiteSpace(AccountUrl);
}
