using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj45.RelayDesk.Tests;

/// <summary>
/// End-to-end API tests over the offline inbound-email orchestration pipeline (Foundry disabled by
/// default), so they run deterministically in CI with no Azure connectivity. Exercises the real DI,
/// seed data, MCP mock and human-review queue through <see cref="WebApplicationFactory{TEntryPoint}"/>.
/// </summary>
public class RelayPipelineTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public RelayPipelineTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_reports_offline_engine()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
        Assert.False(doc.GetProperty("foundryEnabled").GetBoolean());
    }

    [Fact]
    public async Task Foundry_probe_reports_offline_mode_by_design()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health/foundry");
        Assert.Equal("offline", doc.GetProperty("foundryMode").GetString());
        Assert.False(doc.GetProperty("foundryLive").GetBoolean());
    }

    [Fact]
    public async Task Inbox_is_seeded_with_emails()
    {
        var client = _factory.CreateClient();
        var inbox = await client.GetFromJsonAsync<JsonElement>("/api/inbox");
        Assert.True(inbox.GetArrayLength() >= 5, "Expected a seeded watched mailbox.");
    }

    [Fact]
    public async Task Agent_catalog_exposes_five_stage_agents()
    {
        var client = _factory.CreateClient();
        var agents = await client.GetFromJsonAsync<JsonElement>("/api/agents");
        Assert.Equal(5, agents.GetArrayLength());
        // Each agent must surface an explicit, non-empty instruction set.
        foreach (var a in agents.EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(a.GetProperty("name").GetString()));
            Assert.True(a.GetProperty("instructions").GetString()!.Length > 80,
                "Each Foundry agent must surface explicit instructions.");
        }
    }

    [Fact]
    public async Task Mcp_catalog_exposes_lookup_and_operation_tools()
    {
        var client = _factory.CreateClient();
        var tools = await client.GetFromJsonAsync<JsonElement>("/api/mcp/tools");
        Assert.True(tools.GetArrayLength() >= 8);
        var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToHashSet();
        Assert.Contains("customer.search", names);
        Assert.Contains("account.get", names);
        Assert.Contains("case.create", names);
        Assert.Contains("creditmemo.raise", names);
    }

    [Fact]
    public async Task Billing_dispute_extracts_invoice_and_requires_approval()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "ap@brightwave-retail.com",
            fromName = "Priya Nair",
            subject = "Overcharged on invoice INV-44821 - urgent",
            body = "Hi team, we were just charged $4,800 on invoice INV-44821 for our March subscription but our " +
                   "plan should be $3,200. Please correct this billing error and refund the difference.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Billing", c.GetProperty("triage").GetProperty("category").GetString());
        Assert.Equal("Billing Dispute", c.GetProperty("intent").GetProperty("intent").GetString());
        Assert.False(c.GetProperty("intent").GetProperty("requiresHuman").GetBoolean());
        // A credit memo against a billing dispute is a sensitive operation requiring approval.
        Assert.True(c.GetProperty("task").GetProperty("plan").GetProperty("requiresApproval").GetBoolean());
        // Five pipeline stages recorded on the agent timeline.
        Assert.Equal(5, c.GetProperty("trace").GetArrayLength());
    }

    [Fact]
    public async Task Cancellation_email_is_classified_and_flags_churn()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "ops@nordpeak-logistics.com",
            fromName = "Tom Becker",
            subject = "Cancelling our account at renewal",
            body = "Hello, We've decided not to renew our contract when it expires next month. The platform hasn't " +
                   "delivered the ROI we expected. Please confirm the cancellation process and how we export our data.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Cancellation", c.GetProperty("triage").GetProperty("category").GetString());
        Assert.Equal("Cancellation Request", c.GetProperty("intent").GetProperty("intent").GetString());
        Assert.False(c.GetProperty("intent").GetProperty("requiresHuman").GetBoolean());
    }

    [Fact]
    public async Task Technical_outage_opens_a_high_priority_case_via_mcp()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "devops@helioscloud.io",
            fromName = "Marcus Webb",
            subject = "API returning 503 in production - need help",
            body = "Our integration against your Orders API has been returning intermittent 503 errors since this " +
                   "morning. This is impacting production. Order ref ORD-99812. Please help urgently.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Technical Support", c.GetProperty("triage").GetProperty("category").GetString());
        Assert.Equal("P1", c.GetProperty("triage").GetProperty("urgency").GetString());
        Assert.Equal("Technical Issue", c.GetProperty("intent").GetProperty("intent").GetString());
        // The task agent must have made at least one D365 MCP tool call.
        Assert.True(c.GetProperty("task").GetProperty("toolCalls").GetArrayLength() >= 1);
        Assert.Equal("completed", c.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ambiguous_email_is_routed_to_the_human_queue()
    {
        // Use a fresh isolated factory so the queue assertions are not affected by other tests.
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var email = new
        {
            from = "info@meridian-foods.com",
            fromName = "K. Osei",
            subject = "Not sure who to ask",
            body = "Hi, we had a chat internally and there are a few things on our side we're not totally happy " +
                   "about. Before we decide anything we wanted to reach out and see how you'd suggest handling it.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(c.GetProperty("intent").GetProperty("requiresHuman").GetBoolean(),
            "A low-confidence intent must be routed to a human.");
        Assert.Equal("awaiting-human", c.GetProperty("status").GetString());

        // It must appear in the human-review queue, and resolving it must close the case.
        var caseId = c.GetProperty("caseId").GetString();
        var queue = await client.GetFromJsonAsync<JsonElement>("/api/queue");
        Assert.Contains(queue.EnumerateArray(), q => q.GetProperty("caseId").GetString() == caseId);

        var resolve = await client.PostAsJsonAsync($"/api/queue/{caseId}/resolve",
            new { intent = "Complaint Escalation", resolvedBy = "QA reviewer" });
        resolve.EnsureSuccessStatusCode();

        var after = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}");
        Assert.False(after.GetProperty("intent").GetProperty("requiresHuman").GetBoolean());
        Assert.Equal("completed", after.GetProperty("status").GetString());
        Assert.Contains(after.GetProperty("trace").EnumerateArray(),
            s => s.GetProperty("stage").GetString() == "Human Review");
    }

    [Fact]
    public async Task Spam_is_closed_with_no_action()
    {
        var client = _factory.CreateClient();
        var email = new
        {
            from = "promo@best-deals.example",
            fromName = "Best Deals",
            subject = "Congratulations!!! You have WON a $1000 gift card!!!",
            body = "CLICK HERE NOW to claim your FREE $1000 gift card!!! Limited time offer, act now! Unsubscribe.",
            channel = "email"
        };

        var res = await client.PostAsJsonAsync("/api/cases", email);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Spam", c.GetProperty("triage").GetProperty("category").GetString());
        Assert.Equal("closed-spam", c.GetProperty("status").GetString());
        // No downstream MCP operations should be executed for spam.
        Assert.Equal(0, c.GetProperty("task").GetProperty("toolCalls").GetArrayLength());
    }

    [Fact]
    public async Task Run_demo_processes_whole_mailbox_with_one_human_case()
    {
        await using var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        var res = await client.PostAsync("/api/cases/run-demo", null);
        res.EnsureSuccessStatusCode();
        var doc = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(doc.GetProperty("processed").GetInt32() >= 7);
        Assert.Equal("offline", doc.GetProperty("engine").GetString());

        // The seeded mailbox is designed to exercise exactly one human-review path.
        var queue = await client.GetFromJsonAsync<JsonElement>("/api/queue");
        Assert.True(queue.GetArrayLength() >= 1, "Demo mailbox should route at least one case to a human.");
    }
}
