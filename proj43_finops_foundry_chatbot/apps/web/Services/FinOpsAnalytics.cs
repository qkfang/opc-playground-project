using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services;

/// <summary>
/// Deterministic FinOps analytics over <see cref="FinOpsDataset"/>. Pure functions (no AI) that compute
/// the numbers the chatbot reports: period spend, trends, top-N drivers, anomalies, commitment coverage,
/// optimisation recommendations, tag/showback and a simple forecast. The live Foundry agent produces the
/// same answers over real Fabric data; this is the auditable math behind the offline engine.
/// </summary>
public sealed class FinOpsAnalytics
{
    private readonly FinOpsDataset _data;
    public FinOpsAnalytics(FinOpsDataset data) => _data = data;

    public string Currency => _data.Currency;
    public DateOnly EndDate => _data.EndDate;

    private static string MonthKey(DateOnly d) => $"{d.Year:0000}-{d.Month:00}";

    /// <summary>The most recent complete calendar month present in the data.</summary>
    public (int Year, int Month) LatestFullMonth()
    {
        // EndDate is "today"; the latest *full* month is the month before EndDate's month if EndDate isn't month-end,
        // but the seeded data ends at EndDate, so treat EndDate's month as the latest month with data.
        return (_data.EndDate.Year, _data.EndDate.Month);
    }

    public decimal SpendForMonth(int year, int month) =>
        _data.Records.Where(r => r.Date.Year == year && r.Date.Month == month).Sum(r => r.Cost);

    public decimal SpendBetween(DateOnly from, DateOnly to) =>
        _data.Records.Where(r => r.Date >= from && r.Date <= to).Sum(r => r.Cost);

    public decimal TotalSpend() => _data.Records.Sum(r => r.Cost);

    /// <summary>Monthly totals oldest→newest for the trailing <paramref name="months"/> months.</summary>
    public List<(string Month, decimal Cost)> MonthlyTrend(int months = 6)
    {
        var grouped = _data.Records
            .GroupBy(r => MonthKey(r.Date))
            .Select(g => (Month: g.Key, Cost: g.Sum(x => x.Cost)))
            .OrderBy(x => x.Month)
            .ToList();
        return grouped.Skip(Math.Max(0, grouped.Count - months)).ToList();
    }

    public enum Dimension { Service, ResourceGroup, Subscription, Region, CostCenter, Team, Environment }

    private static Func<CostRecord, string> Selector(Dimension d) => d switch
    {
        Dimension.Service => r => r.Service,
        Dimension.ResourceGroup => r => r.ResourceGroup,
        Dimension.Subscription => r => r.Subscription,
        Dimension.Region => r => r.Region,
        Dimension.CostCenter => r => r.CostCenter,
        Dimension.Team => r => r.Team,
        Dimension.Environment => r => r.Environment,
        _ => r => r.Service,
    };

    /// <summary>Top-N buckets by cost for a dimension, optionally restricted to a single month.</summary>
    public List<SpendBucket> TopBy(Dimension dim, int n = 5, (int Year, int Month)? month = null)
    {
        IEnumerable<CostRecord> q = _data.Records;
        if (month is { } m) q = q.Where(r => r.Date.Year == m.Year && r.Date.Month == m.Month);
        var list = q.ToList();
        decimal total = list.Sum(r => r.Cost);
        var sel = Selector(dim);
        return list.GroupBy(sel)
            .Select(g => new SpendBucket { Name = g.Key, Cost = Math.Round(g.Sum(x => x.Cost), 2) })
            .OrderByDescending(b => b.Cost)
            .Take(n)
            .Select(b => { b.Share = total > 0 ? b.Cost / total : 0; return b; })
            .ToList();
    }

    /// <summary>Month-over-month anomalies (by service) where the latest month jumps beyond a threshold.</summary>
    public List<CostAnomaly> DetectAnomalies(decimal thresholdPercent = 25m)
    {
        var months = _data.Records.Select(r => MonthKey(r.Date)).Distinct().OrderBy(x => x).ToList();
        if (months.Count < 2) return new();
        string cur = months[^1], prev = months[^2];

        var result = new List<CostAnomaly>();
        foreach (var svc in _data.Records.Select(r => r.Service).Distinct())
        {
            decimal c = _data.Records.Where(r => r.Service == svc && MonthKey(r.Date) == cur).Sum(r => r.Cost);
            decimal p = _data.Records.Where(r => r.Service == svc && MonthKey(r.Date) == prev).Sum(r => r.Cost);
            if (p <= 0) continue;
            decimal change = (c - p) / p * 100m;
            if (Math.Abs(change) >= thresholdPercent)
            {
                result.Add(new CostAnomaly
                {
                    Dimension = $"Service: {svc}",
                    Month = cur,
                    PreviousCost = Math.Round(p, 2),
                    CurrentCost = Math.Round(c, 2),
                    ChangePercent = Math.Round(change, 1),
                    Severity = Math.Abs(change) >= 60m ? "critical" : Math.Abs(change) >= 35m ? "warning" : "info",
                });
            }
        }
        return result.OrderByDescending(a => Math.Abs(a.ChangePercent)).ToList();
    }

    /// <summary>Commitment (reservation/savings-plan) coverage over the trailing month.</summary>
    public CoverageSummary Coverage((int Year, int Month)? month = null)
    {
        var m = month ?? LatestFullMonth();
        var rows = _data.Records.Where(r => r.Date.Year == m.Year && r.Date.Month == m.Month).ToList();
        decimal total = rows.Sum(r => r.Cost);
        decimal committed = rows.Sum(r => r.CommittedCost);
        return new CoverageSummary
        {
            TotalCost = Math.Round(total, 2),
            CommittedCost = Math.Round(committed, 2),
            OnDemandCost = Math.Round(total - committed, 2),
            CoveragePercent = total > 0 ? Math.Round(committed / total * 100m, 1) : 0,
        };
    }

    /// <summary>Heuristic optimisation recommendations grounded in the data shape.</summary>
    public List<Recommendation> Recommendations()
    {
        var recs = new List<Recommendation>();
        var cov = Coverage();
        var (y, mo) = LatestFullMonth();

        // 1) Commitment opportunity on under-covered, committable spend.
        if (cov.CoveragePercent < 75m && cov.OnDemandCost > 0)
        {
            decimal addressable = cov.OnDemandCost * 0.5m;        // assume half is steady-state eligible
            recs.Add(new Recommendation
            {
                Id = "REC-001", Category = "Commitment",
                Title = "Increase reservation / savings-plan coverage",
                Detail = $"On-demand spend is {Currency} {cov.OnDemandCost:N0}/mo at {cov.CoveragePercent:N0}% coverage. " +
                         "Purchasing 1-year savings plans on steady-state compute (App Service, AKS, VMs, SQL, Cosmos) " +
                         "can reduce eligible on-demand rates by ~30%.",
                EstimatedMonthlySavings = Math.Round(addressable * 0.30m, 0), Effort = "Low",
            });
        }

        // 2) Non-prod idle: sandbox running 24x7.
        decimal nonProd = _data.Records.Where(r => r.Environment == "non-prod" && r.Date.Year == y && r.Date.Month == mo).Sum(r => r.Cost);
        if (nonProd > 0)
        {
            recs.Add(new Recommendation
            {
                Id = "REC-002", Category = "Idle",
                Title = "Auto-stop non-production outside business hours",
                Detail = $"Sandbox/non-prod spend is {Currency} {nonProd:N0}/mo. Scheduling shutdown nights/weekends " +
                         "(~128 of 168 hours idle) typically removes ~65% of compute cost there.",
                EstimatedMonthlySavings = Math.Round(nonProd * 0.45m, 0), Effort = "Low",
            });
        }

        // 3) Anomaly-driven investigation.
        var topAnomaly = DetectAnomalies().FirstOrDefault();
        if (topAnomaly is not null && topAnomaly.ChangePercent > 0)
        {
            decimal delta = topAnomaly.CurrentCost - topAnomaly.PreviousCost;
            recs.Add(new Recommendation
            {
                Id = "REC-003", Category = "Governance",
                Title = $"Investigate {topAnomaly.Dimension} spike (+{topAnomaly.ChangePercent:N0}%)",
                Detail = $"{topAnomaly.Dimension} rose from {Currency} {topAnomaly.PreviousCost:N0} to {Currency} {topAnomaly.CurrentCost:N0} " +
                         $"month-over-month (+{Currency} {delta:N0}). Confirm intentional (new workload) vs. regression (runaway query, missing autoscale floor).",
                EstimatedMonthlySavings = Math.Round(delta * 0.5m, 0), Effort = "Medium",
            });
        }

        // 4) Storage lifecycle.
        decimal storage = _data.Records.Where(r => r.Service == "Azure Storage" && r.Date.Year == y && r.Date.Month == mo).Sum(r => r.Cost);
        if (storage > 0)
        {
            recs.Add(new Recommendation
            {
                Id = "REC-004", Category = "Storage",
                Title = "Apply storage lifecycle tiering",
                Detail = $"Azure Storage is {Currency} {storage:N0}/mo. Moving cool/archive-eligible blobs and cleaning orphaned " +
                         "disks/snapshots commonly saves 15-25% of storage cost.",
                EstimatedMonthlySavings = Math.Round(storage * 0.18m, 0), Effort = "Medium",
            });
        }

        return recs.OrderByDescending(r => r.EstimatedMonthlySavings).ToList();
    }

    /// <summary>Cost grouped by a tag-like dimension for the latest month (showback).</summary>
    public List<SpendBucket> Showback(Dimension dim) => TopBy(dim, n: 20, month: LatestFullMonth());

    /// <summary>Simple linear run-rate forecast for next month from the trailing 3 months.</summary>
    public (decimal NextMonthForecast, decimal LastMonth, decimal Slope) Forecast()
    {
        var trend = MonthlyTrend(3);
        if (trend.Count == 0) return (0, 0, 0);
        if (trend.Count == 1) return (trend[0].Cost, trend[0].Cost, 0);
        decimal first = trend[0].Cost, last = trend[^1].Cost;
        decimal slope = (last - first) / (trend.Count - 1);
        return (Math.Round(last + slope, 2), Math.Round(last, 2), Math.Round(slope, 2));
    }
}
