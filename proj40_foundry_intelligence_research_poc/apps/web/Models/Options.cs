namespace Proj40.IntelligenceResearch.Web.Models;

/// <summary>
/// Microsoft Foundry connection options. When <see cref="Enabled"/> is false or the endpoint is
/// missing, the app runs the deterministic offline engine so the POC is always demonstrable.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. Default false → offline/mock engine (safe fallback).</summary>
    public bool Enabled { get; set; }

    /// <summary>AI Foundry project endpoint, e.g. https://&lt;resource&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name (e.g. gpt-4o).</summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o";

    /// <summary>Display name for the ephemeral prompt agent.</summary>
    public string AgentName { get; set; } = "proj40-research-agent";

    /// <summary>True only when the live Foundry path should be attempted.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>Where generated cases are persisted. POC default is local JSON under App_Data.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>"local" (App_Data JSON) | "blob" (Azure Storage, future).</summary>
    public string Mode { get; set; } = "local";

    /// <summary>
    /// Writable folder for local JSON cases. On Linux App Service with WEBSITE_RUN_FROM_PACKAGE=1 the
    /// content root (/home/site/wwwroot) is read-only, so persistence must target a writable path such
    /// as /home/site/data. When unset the service resolves HOME/site/data, then falls back to App_Data.
    /// </summary>
    public string? LocalDataFolder { get; set; }

    /// <summary>Blob connection string / account URL when Mode = blob (managed identity preferred).</summary>
    public string? BlobEndpoint { get; set; }

    /// <summary>Container name when Mode = blob.</summary>
    public string ContainerName { get; set; } = "cases";
}
