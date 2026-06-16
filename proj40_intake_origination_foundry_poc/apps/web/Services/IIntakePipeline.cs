using Proj40.IntakeOrigination.Web.Models;

namespace Proj40.IntakeOrigination.Web.Services;

/// <summary>
/// The origination pipeline. An implementation runs the four agent stages against an inbound email
/// and returns a fully-populated <see cref="IntakeCase"/>. The Foundry implementation calls a
/// Microsoft Foundry prompt agent per stage; the offline implementation is fully deterministic.
/// </summary>
public interface IIntakePipeline
{
    string Name { get; }
    Task<IntakeCase> RunAsync(InboundEmail email, CancellationToken ct = default);
}
