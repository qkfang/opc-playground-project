namespace Proj44.Compliance.Web.Models;

/// <summary>
/// Bound from configuration section "Foundry". Controls how the compliance-mapping pipeline talks to
/// Microsoft Foundry. When <see cref="Enabled"/> is false (or endpoint missing), the app uses the
/// deterministic offline engine so the POC is fully runnable without live Azure access.
///
/// Mirrors the proj37 blueprint: a single AI Foundry project endpoint + model deployment, with one
/// logical agent created per pipeline stage (each stage supplies its own instructions/persona/name).
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. If false, always use the offline engine.</summary>
    public bool Enabled { get; set; }

    /// <summary>AI Foundry project endpoint (the "AI Foundry API" endpoint of the project).</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name used by every stage agent (e.g. "gpt-4o").</summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o";

    /// <summary>
    /// Base agent name. Each pipeline stage derives a per-stage agent name from this
    /// (e.g. "proj44-compliance-ingestion") so Foundry shows which agent ran which stage.
    /// </summary>
    public string AgentName { get; set; } = "proj44-compliance";

    /// <summary>The Foundry path is only attempted when enabled AND an endpoint is configured.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>
/// Bound from configuration section "Storage". Optional durable persistence for generated frameworks.
/// The POC keeps everything in-memory + on local disk by default; blob is used only when an account
/// URL is supplied (keyless, via managed identity), matching the proj37 storage convention.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Blob account URL, e.g. https://acct.blob.core.windows.net. Empty => local disk only.</summary>
    public string? AccountUrl { get; set; }

    /// <summary>Blob container for persisted frameworks.</summary>
    public string ContainerName { get; set; } = "compliance";

    /// <summary>Local folder for persistence (App Service: /home/site/data).</summary>
    public string LocalDataFolder { get; set; } = "App_Data";

    public bool UseBlob => !string.IsNullOrWhiteSpace(AccountUrl);
}
