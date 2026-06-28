namespace Proj45.RelayDesk.Web.Models;

/// <summary>
/// Microsoft Foundry connection settings. When <see cref="IsConfigured"/> is false the app
/// runs the deterministic offline pipeline so the demo always works.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. When false the offline pipeline is always used.</summary>
    public bool Enabled { get; set; }

    /// <summary>AI Foundry project endpoint, e.g. https://&lt;account&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name used for the prompt agents (e.g. gpt-4o).</summary>
    public string ModelDeployment { get; set; } = "gpt-4o";

    /// <summary>Per-agent request timeout (seconds).</summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Intent confidence threshold (0..1). When the router's confidence is below this value the
    /// case is routed to the human review queue. Applies to both engines.
    /// </summary>
    public double IntentHumanReviewThreshold { get; set; } = 0.62;

    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>
/// Durable storage settings. The POC persists a JSON case journal so cases survive restarts.
/// On Azure App Service this points at the persisted /home/site/data share.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Local/served directory for the case journal.</summary>
    public string DataDirectory { get; set; } = "App_Data";

    /// <summary>Optional blob account (managed-identity / RBAC, no keys) for future durable export.</summary>
    public string? BlobAccountUrl { get; set; }
}
