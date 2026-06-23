using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

/// <summary>
/// Resolves the full Requirement -> Policy -> Standard -> Control chain for a single requirement
/// (GET /api/traceability/{id} and the Traceability tab). Reports completeness and where the chain
/// breaks. Deterministic and pure.
/// </summary>
public static class TraceabilityResolver
{
    public static TraceabilityChain? Resolve(ComplianceFramework fw, string requirementId)
    {
        var req = fw.Requirements.FirstOrDefault(r =>
            string.Equals(r.Id, requirementId, StringComparison.OrdinalIgnoreCase));
        if (req is null) return null;

        var policyById = fw.Policies.ToDictionary(p => p.Id);
        var standardById = fw.Standards.ToDictionary(s => s.Id);
        var controlById = fw.Controls.ToDictionary(c => c.Id);

        var chain = new TraceabilityChain { Requirement = req };

        if (req.PolicyIds.Count == 0)
            chain.BrokenLinks.Add($"{req.Id} has no mapped policy.");

        foreach (var pid in req.PolicyIds)
        {
            if (!policyById.TryGetValue(pid, out var pol)) continue;
            var pNode = new TraceabilityPolicyNode { Policy = pol };

            if (pol.StandardIds.Count == 0)
                chain.BrokenLinks.Add($"Policy {pol.Id} has no mapped standard.");

            foreach (var sid in pol.StandardIds)
            {
                if (!standardById.TryGetValue(sid, out var std)) continue;
                var sNode = new TraceabilityStandardNode { Standard = std };

                if (std.ControlIds.Count == 0)
                    chain.BrokenLinks.Add($"Standard {std.Id} has no enforcing control.");

                foreach (var cid in std.ControlIds)
                    if (controlById.TryGetValue(cid, out var ctl))
                        sNode.Controls.Add(ctl);

                pNode.Standards.Add(sNode);
            }
            chain.Policies.Add(pNode);
        }

        // Complete when at least one policy -> standard -> control path reaches a control.
        chain.IsComplete = chain.Policies
            .SelectMany(p => p.Standards)
            .Any(s => s.Controls.Count > 0);

        return chain;
    }
}
