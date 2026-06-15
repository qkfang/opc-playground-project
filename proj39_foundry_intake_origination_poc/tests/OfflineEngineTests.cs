using Proj39.IntakeOrigination.Web.Models;
using Proj39.IntakeOrigination.Web.Services;
using Xunit;

namespace Proj39.IntakeOrigination.Tests;

public class OfflineEngineTests
{
    private static InboundEmail HotEnterprise() => new()
    {
        From = "amelia.nguyen@globex-mining.com",
        FromName = "Amelia Nguyen",
        Subject = "Urgent: data platform for new processing sites (budget approved)",
        Body =
            "I'm the VP of Digital Transformation at Globex Mining Corporation. We're a 6,500-person resources " +
            "company. Annual revenue is around AUD $2.1B. We've had board approval for a AUD $1.4M data " +
            "modernisation program this financial year. Key drivers:\n- Safety compliance reporting is slow\n" +
            "- We can't see production throughput in real time\nI'm the budget owner and decision maker. " +
            "We'd want to go live before Q3 FY26.\nAmelia Nguyen\nVP Digital Transformation, Globex Mining Corporation"
    };

    private static InboundEmail Spam() => new()
    {
        From = "winner@prize-rewards-intl.biz",
        FromName = "Rewards Team",
        Subject = "Congratulations!!! You have been selected for a $5,000,000 reward",
        Body = "Dear Lucky Winner, you have WON. Reply with your bank details and a processing fee of $250 to claim your prize."
    };

    [Fact]
    public void Extract_pulls_account_lead_opportunity()
    {
        var x = OfflineOriginationEngine.Extract(HotEnterprise());
        Assert.Equal("Globex Mining Corporation", x.Account.Name);
        Assert.Equal("Mining & Resources", x.Account.Industry);
        Assert.Equal("VP", x.Lead.Seniority);
        Assert.True(x.Lead.IsDecisionMaker);
        // Deal value should be the program budget ($1.4M), not company revenue ($2.1B).
        Assert.Equal(1_400_000m, x.Opportunity.EstimatedValue);
        Assert.Equal("Budget approved", x.Opportunity.BudgetStatus);
        Assert.True(x.Opportunity.Drivers.Count >= 2);
    }

    [Fact]
    public void Extract_employee_band_handles_hyphenated_person()
    {
        var x = OfflineOriginationEngine.Extract(HotEnterprise());
        Assert.Equal("5,001-10,000", x.Account.EmployeeBand);
    }

    [Fact]
    public void Triage_marks_hot_for_funded_senior_urgent_lead()
    {
        var email = HotEnterprise();
        var x = OfflineOriginationEngine.Extract(email);
        var t = OfflineOriginationEngine.Triage(email, x);
        Assert.Equal("Hot", t.Classification);
        Assert.True(t.Score >= 70);
        Assert.Equal("Enterprise Sales", t.RoutedTo);   // 6,500 staff => enterprise routing
        Assert.NotEmpty(t.Factors);
    }

    [Fact]
    public void Triage_quarantines_spam()
    {
        var email = Spam();
        var x = OfflineOriginationEngine.Extract(email);
        var t = OfflineOriginationEngine.Triage(email, x);
        Assert.Equal("Spam", t.Classification);
        Assert.Equal(0, t.Score);
        Assert.Equal("Quarantine", t.RoutedTo);
    }

    [Fact]
    public async Task Pipeline_runs_all_stages_and_produces_report()
    {
        var engine = new OfflineOriginationEngine();
        var c = await engine.ProcessAsync(HotEnterprise());
        Assert.Equal("completed", c.Status);
        Assert.Equal("offline", c.Engine);
        // One step per agent stage.
        Assert.Contains(c.AgentSteps, s => s.Agent == "Extraction");
        Assert.Contains(c.AgentSteps, s => s.Agent == "Triage");
        Assert.Contains(c.AgentSteps, s => s.Agent == "LeadResearch");
        Assert.Contains(c.AgentSteps, s => s.Agent == "Report");
        Assert.NotEmpty(c.Research.DemandSignals);
        Assert.False(string.IsNullOrWhiteSpace(c.Report.GeneratedMarkdown));
        Assert.Equal("Pursue", c.Report.Disposition);
    }

    [Fact]
    public async Task Pipeline_dispositions_spam_as_disqualify()
    {
        var engine = new OfflineOriginationEngine();
        var c = await engine.ProcessAsync(Spam());
        Assert.Equal("completed", c.Status);
        Assert.Equal("Disqualify", c.Report.Disposition);
        Assert.Empty(c.Research.DemandSignals);
    }
}
