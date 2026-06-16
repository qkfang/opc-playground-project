namespace Proj40.IntakeOrigination.Web.Models;

/// <summary>
/// Microsoft Foundry prompt-agent configuration. When <see cref="IsConfigured"/> is false the app
/// runs entirely on its deterministic offline agents so the POC is always demoable.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. Even when true, a missing endpoint forces offline mode.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>AI Foundry project endpoint (the "AI Foundry API" endpoint of the project).</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name used by the prompt agents (e.g. gpt-4o).</summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o";

    /// <summary>Logical agent name surfaced to Foundry for traceability.</summary>
    public string AgentName { get; set; } = "proj40-intake-origination";

    public bool IsConfigured =>
        Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>
/// Storage configuration. The POC persists its case journal to a local data folder so it survives
/// App Service restarts (a real deployment would point this at Blob + a database).
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Blob account URL (managed-identity auth). Optional for the POC.</summary>
    public string? AccountUrl { get; set; }

    /// <summary>Container name for durable artefacts.</summary>
    public string ContainerName { get; set; } = "intake";

    /// <summary>
    /// Local folder for durable journal persistence. On App Service this maps to /home/site/data,
    /// which is writeable and persisted across restarts (the app root is read-only under RunFromPackage).
    /// </summary>
    public string LocalDataFolder { get; set; } = "App_Data";
}
