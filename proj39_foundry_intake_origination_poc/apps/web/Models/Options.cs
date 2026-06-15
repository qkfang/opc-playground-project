namespace Proj39.IntakeOrigination.Web.Models;

/// <summary>
/// Bound from configuration section "Foundry". Controls how the intake/origination agents talk to
/// Microsoft Foundry. When <see cref="Enabled"/> is false (or endpoint missing), the app uses the
/// deterministic offline agent engine so the POC is fully runnable without live Azure access.
/// </summary>
public sealed class FoundryOptions
{
    public const string SectionName = "Foundry";

    /// <summary>Master switch. If false, always use the offline engine.</summary>
    public bool Enabled { get; set; }

    /// <summary>Foundry project endpoint, e.g. https://&lt;name&gt;.services.ai.azure.com/api/projects/&lt;project&gt;.</summary>
    public string? ProjectEndpoint { get; set; }

    /// <summary>Model deployment name, e.g. gpt-4o.</summary>
    public string ModelDeploymentName { get; set; } = "gpt-4o";

    /// <summary>Name for the ephemeral prompt agent created at runtime.</summary>
    public string AgentName { get; set; } = "proj39-intake-origination";

    /// <summary>True when configuration is sufficient to attempt a live run.</summary>
    public bool IsConfigured => Enabled && !string.IsNullOrWhiteSpace(ProjectEndpoint);
}

/// <summary>Bound from "Storage". Optional blob persistence of cases / reports.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string? AccountUrl { get; set; }
    public string ContainerName { get; set; } = "origination";

    /// <summary>Local folder for case persistence (App Service: /home/site/data).</summary>
    public string LocalDataFolder { get; set; } = "App_Data";

    public bool UseBlob => !string.IsNullOrWhiteSpace(AccountUrl);
}
