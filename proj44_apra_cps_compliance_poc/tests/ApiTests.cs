using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj44.Compliance.Tests;

/// <summary>
/// End-to-end API tests hosting the real app via <see cref="WebApplicationFactory{TEntry}"/> (offline
/// engine, no Azure). Exercises every endpoint and the public tab pages so a green run proves the app
/// boots, serves the framework at scale, runs the pipeline and renders the UI.
/// </summary>
public sealed class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_is_healthy_and_offline_without_config()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Agent_instructions_returns_six_stages()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/agent-instructions");
        var stages = doc.GetProperty("stages").EnumerateArray().ToList();
        Assert.Equal(6, stages.Count);
        Assert.Equal(6, doc.GetProperty("order").GetArrayLength());
    }

    [Fact]
    public async Task Framework_returns_required_scale()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/framework");
        var counts = doc.GetProperty("counts");
        Assert.True(counts.GetProperty("policies").GetInt32() >= 130);
        Assert.True(counts.GetProperty("controls").GetInt32() >= 30);
        Assert.True(counts.GetProperty("requirements").GetInt32() >= 20);
        Assert.True(counts.GetProperty("standards").GetInt32() >= 30);
        Assert.True(counts.GetProperty("requirementToPolicyLinks").GetInt32() > 0);
        Assert.True(counts.GetProperty("policyToStandardLinks").GetInt32() > 0);
        Assert.True(counts.GetProperty("standardToControlLinks").GetInt32() > 0);
    }

    [Theory]
    [InlineData("/api/requirements")]
    [InlineData("/api/policies")]
    [InlineData("/api/standards")]
    [InlineData("/api/controls")]
    [InlineData("/api/clauses")]
    public async Task Layer_collections_return_arrays(string path)
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>(path);
        Assert.Equal(JsonValueKind.Array, doc.ValueKind);
        Assert.True(doc.GetArrayLength() > 0, $"{path} returned empty");
    }

    [Fact]
    public async Task Gaps_endpoint_reports_findings_and_coverage()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/gaps");
        Assert.True(doc.GetProperty("totalGaps").GetInt32() >= 7);
        Assert.True(doc.GetProperty("findings").GetArrayLength() > 0);
        var cov = doc.GetProperty("coverage");
        Assert.True(cov.GetProperty("endToEndCoverage").GetDouble() < 100.0);
        Assert.Equal(100.0, cov.GetProperty("controlCoverage").GetDouble());
    }

    [Fact]
    public async Task Run_pipeline_completes_and_logs_six_agent_steps()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/run", content: null);
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.Equal("completed", doc.GetProperty("status").GetString());

        var steps = doc.GetProperty("agentSteps").EnumerateArray()
            .Select(s => s.GetProperty("step").GetString()).ToArray();
        Assert.Equal(new[] { "ingestion", "requirements", "policies", "standards", "controls", "gap" }, steps);

        Assert.True(doc.GetProperty("counts").GetProperty("policies").GetInt32() >= 130);
        Assert.True(doc.GetProperty("gaps").GetProperty("totalGaps").GetInt32() >= 7);
    }

    [Fact]
    public async Task Traceability_returns_chain_for_known_good_and_404_for_unknown()
    {
        var client = _factory.CreateClient();

        // Find a requirement that has policies from the requirements list.
        var reqs = await client.GetFromJsonAsync<JsonElement>("/api/requirements");
        string? goodId = null;
        foreach (var r in reqs.EnumerateArray())
        {
            if (r.GetProperty("policyIds").GetArrayLength() > 0)
            {
                goodId = r.GetProperty("id").GetString();
                break;
            }
        }
        Assert.NotNull(goodId);

        var chain = await client.GetFromJsonAsync<JsonElement>($"/api/traceability/{goodId}");
        Assert.Equal(goodId, chain.GetProperty("requirement").GetProperty("id").GetString());
        Assert.True(chain.GetProperty("policies").GetArrayLength() > 0);

        var notFound = await client.GetAsync("/api/traceability/REQ-NOPE");
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/requirements")]
    [InlineData("/policies")]
    [InlineData("/standards")]
    [InlineData("/controls")]
    [InlineData("/mappings")]
    [InlineData("/gaps")]
    [InlineData("/traceability")]
    [InlineData("/pipeline")]
    public async Task Tab_pages_render_200_with_nav(string path)
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var html = await resp.Content.ReadAsStringAsync();
        Assert.Contains("APRA CPS 230 Compliance Mapper", html);
        Assert.Contains("mainnav", html);
    }

    [Fact]
    public async Task OpenApi_document_is_served()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
