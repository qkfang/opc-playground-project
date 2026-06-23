using Proj44.Compliance.Web.Models;

namespace Proj44.Compliance.Web.Services;

public static partial class FrameworkBuilder
{
    // =====================================================================================
    // MAPPINGS  -- wire the Requirement -> Policy -> Standard -> Control spine.
    //
    // Strategy: align by CPS 230 theme/domain so mappings are meaningful, then guarantee minimum
    // coverage so every non-gap item has at least one downstream link, and every control is
    // referenced by at least one standard. The KnownGaps sets are honoured so a small, deliberate
    // set of orphans remains for the Gap Analysis tab.
    // =====================================================================================
    private static void WireMappings(ComplianceFramework fw)
    {
        var reqGaps = new HashSet<string>(KnownGaps.RequirementsWithoutPolicy);
        var polGaps = new HashSet<string>(KnownGaps.PoliciesWithoutStandard);
        var stdGaps = new HashSet<string>(KnownGaps.StandardsWithoutControl);

        // Index by domain for theme-aligned mapping.
        var policiesByDomain = fw.Policies.GroupBy(p => p.Domain).ToDictionary(g => g.Key, g => g.ToList());
        var standardsByDomain = fw.Standards.GroupBy(s => s.Domain).ToDictionary(g => g.Key, g => g.ToList());
        var controlsByDomain = fw.Controls.GroupBy(c => c.Domain).ToDictionary(g => g.Key, g => g.ToList());

        string DomainOf(string themeKey) => Cps230Seed.Themes.First(t => t.Key == themeKey).PolicyDomain;

        // ---------------------------------------------------------------- Requirement -> Policy
        foreach (var req in fw.Requirements)
        {
            if (reqGaps.Contains(req.Id)) continue; // deliberate orphan
            var domain = DomainOf(req.Theme);
            if (!policiesByDomain.TryGetValue(domain, out var pols) || pols.Count == 0) continue;

            // Map each requirement to up to 3 policies in its domain (stable, index-based selection).
            var idx = RequirementIndex(req.Id);
            var picks = PickStable(pols, idx, 3);
            foreach (var p in picks)
                if (!req.PolicyIds.Contains(p.Id)) req.PolicyIds.Add(p.Id);
        }

        // ---------------------------------------------------------------- Policy -> Standard
        foreach (var pol in fw.Policies)
        {
            if (polGaps.Contains(pol.Id)) continue; // deliberate orphan
            var stds = standardsByDomain.TryGetValue(pol.Domain, out var s) ? s : fw.Standards;
            // Prefer same-domain standards; if a domain has none, fall back to all standards so the
            // policy is never accidentally orphaned (only KnownGaps policies are orphans).
            if (stds.Count == 0) stds = fw.Standards;
            var idx = PolicyIndex(pol.Id);
            var picks = PickStable(stds, idx, 2);
            foreach (var st in picks)
                if (!pol.StandardIds.Contains(st.Id)) pol.StandardIds.Add(st.Id);
        }

        // ---------------------------------------------------------------- Standard -> Control
        foreach (var std in fw.Standards)
        {
            if (stdGaps.Contains(std.Id)) continue; // deliberate orphan
            var ctls = controlsByDomain.TryGetValue(std.Domain, out var c) ? c : fw.Controls;
            if (ctls.Count == 0) ctls = fw.Controls;
            var idx = StandardIndex(std.Id);
            var picks = PickStable(ctls, idx, 2);
            foreach (var ct in picks)
                if (!std.ControlIds.Contains(ct.Id)) std.ControlIds.Add(ct.Id);
        }

        // ---------------------------------------------------------------- Guarantee control coverage
        // Every control must be referenced by >=1 standard (no accidental orphan controls; the only
        // control-library "gap" we surface is none by default -- standards STD-039/040 are the gaps).
        var referencedControls = new HashSet<string>(fw.Standards.SelectMany(s => s.ControlIds));
        var mappableStandards = fw.Standards.Where(s => !stdGaps.Contains(s.Id)).ToList();
        foreach (var ctl in fw.Controls)
        {
            if (referencedControls.Contains(ctl.Id)) continue;
            // Attach to a same-domain non-gap standard, else any non-gap standard.
            var host = mappableStandards.FirstOrDefault(s => s.Domain == ctl.Domain)
                       ?? mappableStandards.FirstOrDefault();
            if (host is null) continue;
            host.ControlIds.Add(ctl.Id);
            referencedControls.Add(ctl.Id);
        }

        // ---------------------------------------------------------------- Guarantee standard coverage
        // Every non-gap standard must enforce >=1 policy via the reverse mapping being non-empty is not
        // required, but every non-gap standard must have >=1 control. Backfill any that ended up empty.
        foreach (var std in mappableStandards.Where(s => s.ControlIds.Count == 0))
        {
            var ctl = controlsByDomain.TryGetValue(std.Domain, out var c) && c.Count > 0
                ? c[0] : fw.Controls[0];
            std.ControlIds.Add(ctl.Id);
        }

        // ---------------------------------------------------------------- Guarantee policy coverage
        foreach (var pol in fw.Policies.Where(p => !polGaps.Contains(p.Id) && p.StandardIds.Count == 0))
        {
            // Prefer a same-domain standard; fall back to any non-gap standard so a policy is never
            // accidentally orphaned (only KnownGaps policies are orphans).
            ImplementationStandard? st = null;
            if (standardsByDomain.TryGetValue(pol.Domain, out var sds) && sds.Count > 0)
                st = sds.FirstOrDefault(x => !stdGaps.Contains(x.Id)) ?? sds[0];
            st ??= mappableStandards.FirstOrDefault() ?? fw.Standards.FirstOrDefault();
            if (st is not null) pol.StandardIds.Add(st.Id);
        }

        // ---------------------------------------------------------------- Guarantee requirement coverage
        foreach (var req in fw.Requirements.Where(r => !reqGaps.Contains(r.Id) && r.PolicyIds.Count == 0))
        {
            var domain = DomainOf(req.Theme);
            var pol = policiesByDomain.TryGetValue(domain, out var p) && p.Count > 0 ? p[0] : fw.Policies[0];
            req.PolicyIds.Add(pol.Id);
        }
    }

    // Deterministic, evenly-distributed selection of `take` items starting at an offset derived from id.
    private static List<T> PickStable<T>(IReadOnlyList<T> items, int offset, int take)
    {
        var result = new List<T>();
        if (items.Count == 0) return result;
        take = Math.Min(take, items.Count);
        for (int i = 0; i < take; i++)
            result.Add(items[(offset + i) % items.Count]);
        return result;
    }

    private static int RequirementIndex(string id) => ParseTail(id);
    private static int PolicyIndex(string id) => ParseTail(id);
    private static int StandardIndex(string id) => ParseTail(id);

    private static int ParseTail(string id)
    {
        var dash = id.LastIndexOf('-');
        return dash >= 0 && int.TryParse(id[(dash + 1)..], out var v) ? v : 0;
    }
}
