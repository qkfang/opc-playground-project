using Proj40.IntelligenceResearch.Web.Models;

namespace Proj40.IntelligenceResearch.Web.Services;

/// <summary>
/// The intelligence &amp; research pipeline. Implementations: <c>OfflineResearchEngine</c> (deterministic
/// mock) and <c>FoundryResearchEngine</c> (Microsoft Foundry prompt agent, falls back to offline).
/// Each stage mutates the supplied <see cref="ResearchCase"/> and appends an <see cref="AgentStepLog"/>.
/// </summary>
public interface IResearchEngine
{
    string Name { get; }

    /// <summary>Run the full pipeline: entities → insights → source pulls → research brief → report email.</summary>
    Task<ResearchCase> RunAsync(ResearchCase researchCase, CancellationToken ct = default);
}
