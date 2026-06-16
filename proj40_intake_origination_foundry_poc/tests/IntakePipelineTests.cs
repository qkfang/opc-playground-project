using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj40.IntakeOrigination.Tests;

/// <summary>
/// End-to-end API tests over the offline pipeline (Foundry disabled by default in appsettings),
/// so they run deterministically in CI without any Azure connectivity.
/// </summary>
public class IntakePipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntakePipelineTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_offline_engine()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/health");
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Inbox_is_seeded()
    {
        var client = _factory.CreateClient();
        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/inbox");
        Assert.True(inbox.GetArrayLength() >= 5, "Expected a seeded mock inbox.");
    }

    [Fact]
    public async Task Strategic_bank_email_scores_P1_with_full_extraction()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "priya.nair@globalbankcorp.com",
            fromName = "Priya Nair",
            subject = "RFP: Enterprise AI & data platform for fraud analytics",
            body = "Hi, I'm the Chief Data Officer at GlobalBank Corp, a multinational bank with 18,000 employees " +
                   "headquartered in Singapore. We're running an RFP this quarter. Budget approved (deal value around $1.2M). " +
                   "We're currently evaluating Databricks and Snowflake too. Company: GlobalBank Corp",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        // Extraction
        var records = c.GetProperty("records");
        Assert.Equal("GlobalBank Corp", records.GetProperty("account").GetProperty("companyName").GetString());
        Assert.Equal("Strategic", records.GetProperty("account").GetProperty("segment").GetString());
        Assert.True(records.GetProperty("lead").GetProperty("isDecisionMaker").GetBoolean());
        Assert.True(records.GetProperty("opportunity").GetProperty("estimatedAnnualValue").GetDecimal() >= 1_000_000m);

        // Triage
        var triage = c.GetProperty("triage");
        Assert.Equal("P1", triage.GetProperty("priority").GetString());
        Assert.True(triage.GetProperty("leadScore").GetInt32() >= 75);
        Assert.False(triage.GetProperty("disqualified").GetBoolean());

        // Pipeline produced all four stages.
        Assert.Equal(4, c.GetProperty("trace").GetArrayLength());
        Assert.Equal("completed", c.GetProperty("status").GetString());

        // Report exists.
        Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("report").GetProperty("executiveSummary").GetString()));
    }

    [Fact]
    public async Task Spam_email_is_disqualified()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "deals@cheap-seo-now.biz",
            fromName = "SEO Deals",
            subject = "Boost your ranking - buy followers + SEO services!!!",
            body = "Special offer!!! We provide SEO services and can buy followers. Unsubscribe anytime. Crypto giveaway!",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Spam/Disqualified", c.GetProperty("triage").GetProperty("classification").GetString());
        Assert.True(c.GetProperty("triage").GetProperty("disqualified").GetBoolean());
        Assert.Equal("disqualified", c.GetProperty("status").GetString());
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
