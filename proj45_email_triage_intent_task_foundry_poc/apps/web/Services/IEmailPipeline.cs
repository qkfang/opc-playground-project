using Proj45.RelayDesk.Web.Models;

namespace Proj45.RelayDesk.Web.Services;

/// <summary>
/// The inbound-email orchestration pipeline: extraction -> triage -> intent -> task(D365 MCP) -> outcome.
/// Implemented by the deterministic offline engine and by the live Foundry engine (which falls back
/// to offline per-stage).
/// </summary>
public interface IEmailPipeline
{
    /// <summary>foundry | offline</summary>
    string Name { get; }

    /// <summary>Runs the full pipeline for one inbound email and returns the processed case.</summary>
    Task<EmailCase> RunAsync(IncomingEmail email, CancellationToken ct = default);

    /// <summary>Actively probes the engine (real round-trip for Foundry) for the health surface.</summary>
    Task<EngineDiagnostics> ProbeAsync(CancellationToken ct = default);
}
