using Proj41.Underwriting.Web.Models;

namespace Proj41.Underwriting.Web.Services;

/// <summary>
/// The multi-agent underwriting pipeline: extraction -> appetite/triage -> exposure research -> risk study.
/// Implemented by the deterministic <see cref="OfflineUnderwritingPipeline"/> and the
/// Foundry-backed pipeline (which falls back to offline per-stage on any failure).
/// </summary>
public interface IUnderwritingPipeline
{
    /// <summary>Engine label surfaced on the health endpoint and case records.</summary>
    string Name { get; }

    /// <summary>Runs a broker submission email through the full origination pipeline.</summary>
    Task<SubmissionCase> RunAsync(SubmissionEmail email, CancellationToken ct = default);
}
