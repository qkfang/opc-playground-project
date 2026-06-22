using Proj43.FinOps.Web.Services;
using Xunit;

namespace Proj43.FinOps.Tests;

/// <summary>Deterministic checks on the seeded dataset + analytics math.</summary>
public sealed class FinOpsAnalyticsTests
{
    // Fixed "today" so the dataset is fully deterministic regardless of when tests run.
    private static FinOpsAnalytics Build()
    {
        var data = new FinOpsDataset(today: new DateOnly(2026, 6, 30), seed: 43);
        return new FinOpsAnalytics(data);
    }

    [Fact]
    public void Dataset_Is_Deterministic_For_Seed()
    {
        var a = new FinOpsDataset(today: new DateOnly(2026, 6, 30), seed: 43);
        var b = new FinOpsDataset(today: new DateOnly(2026, 6, 30), seed: 43);
        Assert.Equal(a.Records.Count, b.Records.Count);
        Assert.Equal(a.Records.Sum(r => r.Cost), b.Records.Sum(r => r.Cost));
        Assert.True(a.Records.Count > 1000, "expected a substantial number of daily records");
    }

    [Fact]
    public void TotalSpend_And_MonthlySpend_Are_Positive()
    {
        var an = Build();
        Assert.True(an.TotalSpend() > 0);
        var (y, m) = an.LatestFullMonth();
        Assert.True(an.SpendForMonth(y, m) > 0);
    }

    [Fact]
    public void MonthlyTrend_Returns_Requested_Window_In_Order()
    {
        var an = Build();
        var trend = an.MonthlyTrend(6);
        Assert.Equal(6, trend.Count);
        var ordered = trend.Select(t => t.Month).OrderBy(x => x).ToList();
        Assert.Equal(ordered, trend.Select(t => t.Month).ToList());
        Assert.All(trend, t => Assert.True(t.Cost > 0));
    }

    [Fact]
    public void TopBy_Service_Sums_And_Shares_Are_Sane()
    {
        var an = Build();
        var top = an.TopBy(FinOpsAnalytics.Dimension.Service, 5);
        Assert.Equal(5, top.Count);
        // Descending by cost.
        for (int i = 1; i < top.Count; i++)
            Assert.True(top[i - 1].Cost >= top[i].Cost);
        // Shares are fractions between 0 and 1.
        Assert.All(top, b => Assert.InRange(b.Share, 0m, 1m));
    }

    [Fact]
    public void DetectAnomalies_Flags_The_Seeded_Sql_Spike()
    {
        var an = Build();
        var anomalies = an.DetectAnomalies();
        Assert.NotEmpty(anomalies);
        Assert.Contains(anomalies, a => a.Dimension.Contains("Azure SQL Database") && a.ChangePercent > 0);
    }

    [Fact]
    public void Coverage_Is_Between_0_And_100_And_Adds_Up()
    {
        var an = Build();
        var c = an.Coverage();
        Assert.InRange(c.CoveragePercent, 0m, 100m);
        Assert.Equal(Math.Round(c.CommittedCost + c.OnDemandCost, 0), Math.Round(c.TotalCost, 0));
    }

    [Fact]
    public void Recommendations_Have_Positive_Savings_And_Ids()
    {
        var an = Build();
        var recs = an.Recommendations();
        Assert.NotEmpty(recs);
        Assert.All(recs, r =>
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Id));
            Assert.False(string.IsNullOrWhiteSpace(r.Title));
            Assert.True(r.EstimatedMonthlySavings >= 0);
        });
        // Sorted by savings descending.
        for (int i = 1; i < recs.Count; i++)
            Assert.True(recs[i - 1].EstimatedMonthlySavings >= recs[i].EstimatedMonthlySavings);
    }

    [Fact]
    public void Forecast_Returns_A_Value()
    {
        var an = Build();
        var (next, last, _) = an.Forecast();
        Assert.True(next > 0);
        Assert.True(last > 0);
    }
}
