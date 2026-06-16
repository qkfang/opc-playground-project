using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj41.Underwriting.Tests;

/// <summary>
/// End-to-end API tests over the offline underwriting pipeline (Foundry disabled by default),
/// so they run deterministically in CI with no Azure connectivity.
/// </summary>
public class UnderwritingPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public UnderwritingPipelineTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_reports_offline_engine()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Inbox_is_seeded_with_submissions()
    {
        var client = _factory.CreateClient();
        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/inbox");
        Assert.True(inbox.GetArrayLength() >= 5, "Expected a seeded broker mailbox.");
    }

    [Fact]
    public async Task Property_submission_extracts_records_and_routes()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "james.okafor@summit-risk.com",
            fromName = "James Okafor",
            subject = "New Business Submission: Property + BI for Atlas Steel Fabrication Inc",
            body = "Please find a new business submission for our client, Atlas Steel Fabrication Inc, a metal " +
                   "fabrication manufacturer established 2004 with 320 employees across 3 locations in Houston, Texas. " +
                   "Total insurable value is approximately $85M. They are seeking commercial property cover with a $50M " +
                   "limit and a $100k deductible, effective 01/08/2026. Currently with Travelers. Brokerage: Summit Risk Partners.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        var rec = c.GetProperty("records");
        Assert.Equal("Atlas Steel Fabrication Inc", rec.GetProperty("insured").GetProperty("companyName").GetString());
        Assert.Equal("Property", rec.GetProperty("submission").GetProperty("lineOfBusiness").GetString());
        Assert.Equal(50_000_000m, rec.GetProperty("submission").GetProperty("requestedLimit").GetDecimal());
        Assert.Equal(85_000_000m, rec.GetProperty("insured").GetProperty("totalInsurableValue").GetDecimal());

        // High limit (>= $25M) must trigger a referral.
        var triage = c.GetProperty("triage");
        Assert.Equal("Refer to Underwriter", triage.GetProperty("appetiteClass").GetString());
        Assert.Contains(triage.GetProperty("referralTriggers").EnumerateArray(), e => e.GetString()!.Contains("High limit"));

        // Four pipeline stages, completed, with a study.
        Assert.Equal(4, c.GetProperty("trace").GetArrayLength());
        Assert.Equal("completed", c.GetProperty("status").GetString());
        Assert.True(c.GetProperty("study").GetProperty("indicatedPremium").GetDecimal() > 0);
    }

    [Fact]
    public async Task Cyber_submission_is_classified_and_priced()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "priya.shah@aon.com",
            fromName = "Priya Shah",
            subject = "Cyber submission - Meridian Health Systems",
            body = "Cyber & privacy liability submission for Meridian Health Systems, a healthcare provider with 2,400 " +
                   "employees operating 11 clinics. Requesting a $10M cyber limit effective ASAP. MFA and EDR deployed. Incumbent is Chubb.",
            channel = "broker-portal"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Cyber", c.GetProperty("records").GetProperty("submission").GetProperty("lineOfBusiness").GetString());
        // Healthcare cyber should produce a regulatory signal in research.
        Assert.Contains(c.GetProperty("research").GetProperty("signals").EnumerateArray(),
            s => s.GetProperty("category").GetString() == "Regulatory" || s.GetProperty("category").GetString() == "IndustryHazard");
        Assert.False(c.GetProperty("triage").GetProperty("declined").GetBoolean());
    }

    [Fact]
    public async Task Prohibited_class_is_out_of_appetite()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "broker@redstar-agency.com",
            fromName = "Redstar Agency",
            subject = "Property cover for Big Bang Fireworks Manufacturing",
            body = "New submission for Big Bang Fireworks Manufacturing, a fireworks manufacturer with 60 employees. Seeking $20M property cover.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Out of Appetite", c.GetProperty("triage").GetProperty("appetiteClass").GetString());
        Assert.True(c.GetProperty("triage").GetProperty("declined").GetBoolean());
        Assert.Equal("declined", c.GetProperty("status").GetString());
        Assert.Equal("Decline", c.GetProperty("study").GetProperty("overallRecommendation").GetString());
    }

    [Fact]
    public async Task Run_demo_processes_whole_inbox()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/cases/run-demo", null);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.GetProperty("processed").GetInt32() >= 5);
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
    }
}
