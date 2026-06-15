using Proj40.IntelligenceResearch.Web.Models;
using Proj40.IntelligenceResearch.Web.Services;
using Xunit;

namespace Proj40.IntelligenceResearch.Tests;

/// <summary>
/// Unit tests for the deterministic offline intelligence &amp; research engine. These exercise the
/// pure pipeline logic (entities → insights → sources → brief → report email) without HTTP.
/// </summary>
public class OfflineEngineTests
{
    private static OfflineResearchEngine NewEngine() => new(TestCorpus.Load());

    private static InboundEmail NordwindEmail() => new()
    {
        Id = "t-001",
        From = "elena.fischer@nordwind-energy.com",
        FromName = "Elena Fischer",
        Subject = "RFP attached — grid-scale battery analytics platform (Nordwind Energy)",
        Body = "Please find attached our RFP. Kind regards, Elena Fischer, Head of Digital Grid, Nordwind Energy AG",
        Document = new CustomerDocument
        {
            FileName = "nordwind-rfp.md",
            DocType = "RFP",
            Content = "Nordwind Energy AG is a renewable IPP with annual revenue of EUR 1.3 billion. " +
                      "We are deploying 900 MWh of grid-scale battery storage and need automated bidding into EPEX and Nord Pool. " +
                      "Regulatory reporting currently takes our team three weeks per quarter. " +
                      "Must integrate with SAP S/4HANA and an OSIsoft PI historian. Indicative budget: EUR 2.5M - 3.5M. Target go-live Q2 2027."
        }
    };

    [Fact]
    public async Task FullPipeline_Nordwind_ProducesAllStages()
    {
        var engine = NewEngine();
        var c = new ResearchCase { Email = NordwindEmail() };
        await engine.RunAsync(c);

        Assert.Equal("offline", c.Engine);
        Assert.Equal("Nordwind Energy AG", c.Entities.PrimaryOrganisation);
        Assert.Equal("Energy & Utilities", c.Entities.Industry);
        Assert.NotEmpty(c.Insights);
        Assert.NotEmpty(c.SourceHits);
        Assert.NotEmpty(c.Brief.KeyFindings);
        Assert.False(string.IsNullOrWhiteSpace(c.ReportEmail.RenderedMarkdown));
        // Five agent stages logged.
        Assert.Contains(c.AgentSteps, s => s.Step == "entities");
        Assert.Contains(c.AgentSteps, s => s.Step == "report-email");
    }

    [Fact]
    public void ExtractEntities_PullsOrgPeopleTechAndTopics()
    {
        var engine = NewEngine();
        var x = engine.ExtractEntities(NordwindEmail());

        Assert.Equal("Nordwind Energy AG", x.PrimaryOrganisation);
        Assert.Contains("Elena Fischer", x.People);
        Assert.Contains(x.Technologies, t => t is "SAP" or "S/4HANA" or "OSIsoft");
        Assert.Contains(x.Topics, t => t is "battery" or "grid" or "arbitrage");
        Assert.Contains("Energy & Utilities", x.Industry);
    }

    [Fact]
    public void PullSources_MatchesInternalAndExternalByEntity()
    {
        var engine = NewEngine();
        var x = engine.ExtractEntities(NordwindEmail());
        var hits = engine.PullSources(x);

        Assert.Contains(hits, h => h.SourceType == "Internal");
        Assert.Contains(hits, h => h.SourceType == "External");
        // Internal-first ordering.
        Assert.Equal("Internal", hits.First().SourceType);
    }

    [Fact]
    public void GenerateInsights_FlagsStatedPainPoints()
    {
        var engine = NewEngine();
        var email = NordwindEmail();
        var x = engine.ExtractEntities(email);
        var insights = OfflineResearchEngine.GenerateInsights(email, x);

        Assert.Contains(insights, i => i.Category == "Risk");      // stated pain
        Assert.Contains(insights, i => i.Category == "Need");      // intent
        Assert.All(insights, i => Assert.False(string.IsNullOrWhiteSpace(i.Evidence)));
    }

    [Fact]
    public async Task BriefBudget_PrefersDealBudgetOverRevenue()
    {
        var engine = NewEngine();
        var c = new ResearchCase { Email = NordwindEmail() };
        await engine.RunAsync(c);
        // Executive summary should reference the deal budget (2.5M), not the 1.3 billion revenue.
        Assert.Contains("2.5", c.Brief.ExecutiveSummary);
        Assert.DoesNotContain("1.3 billion", c.Brief.ExecutiveSummary);
    }

    [Fact]
    public async Task Spam_IsQuarantined_NoResearch()
    {
        var engine = NewEngine();
        var spam = new InboundEmail
        {
            Id = "t-spam",
            From = "winner@prize-rewards.biz",
            FromName = "Rewards Team",
            Subject = "Congratulations!!! You have WON a $5,000,000 prize",
            Body = "Reply with your bank details and a processing fee to claim your prize. Act now!"
        };
        var c = new ResearchCase { Email = spam };
        await engine.RunAsync(c);

        Assert.Single(c.Insights);
        Assert.Contains("spam", c.Insights[0].Headline, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(c.SourceHits);
        Assert.Contains("intake-triage", c.ReportEmail.To);
    }

    [Fact]
    public async Task ReportEmail_RoutesByIndustry()
    {
        var engine = NewEngine();
        var c = new ResearchCase { Email = NordwindEmail() };
        await engine.RunAsync(c);
        Assert.Equal("energy-vertical@contoso.com", c.ReportEmail.To);
        Assert.Contains("Nordwind", c.ReportEmail.Subject);
    }
}
