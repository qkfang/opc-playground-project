using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Produces a complete CPS 230 compliance-mapping framework. Two implementations exist:
///   * <see cref="OfflineComplianceEngine"/> — deterministic, no external calls (always available).
///   * <see cref="Foundry.FoundryComplianceEngine"/> — orchestrates six Foundry stage-agents and
///     falls back to the offline engine on any failure.
/// </summary>
public interface IComplianceEngine
{
    /// <summary>"offline" or "foundry".</summary>
    string Name { get; }

    /// <summary>Run the six-stage compliance-mapping pipeline and return the full framework graph.</summary>
    Task<ComplianceFramework> BuildAsync(CancellationToken ct = default);
}
