using Proj39.IntakeOrigination.Web.Models;

namespace Proj39.IntakeOrigination.Web.Services;

/// <summary>
/// A pluggable intake/origination engine that runs the full multi-agent pipeline against an inbound
/// email. The app ships two implementations:
///  - <c>OfflineOriginationEngine</c>: deterministic, rule + heuristic based, always available.
///  - <c>FoundryOriginationEngine</c>: uses a Microsoft Foundry prompt agent for the reasoning steps
///    (extraction, triage rationale, research, report), falling back to the offline engine on any failure.
/// </summary>
public interface IOriginationEngine
{
    string Name { get; }

    Task<OriginationCase> ProcessAsync(InboundEmail email, CancellationToken ct = default);
}
