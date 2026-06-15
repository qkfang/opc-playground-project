using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj40.IntelligenceResearch.Tests;

/// <summary>
/// API integration tests booting the real app in-memory via <see cref="WebApplicationFactory{T}"/>.
/// Runs against the deterministic offline engine (Foundry disabled by default in appsettings).
/// </summary>
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Health_ReturnsHealthy_Offline()
    {
        var client = _factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("healthy", doc.GetProperty("status").GetString());
        Assert.Equal("offline", doc.GetProperty("engine").GetString());
        Assert.False(doc.GetProperty("foundryConfigured").GetBoolean());
    }

    [Fact]
    public async Task Inbox_ListsSeededEmails_WithDocuments()
    {
        var client = _factory.CreateClient();
        var arr = await client.GetFromJsonAsync<JsonElement>("/api/inbox");
        Assert.True(arr.GetArrayLength() >= 4);
        // At least one email carries an attached document.
        bool anyDoc = arr.EnumerateArray().Any(e => e.GetProperty("hasDocument").GetBoolean());
        Assert.True(anyDoc);
    }

    [Fact]
    public async Task Inbox_GetById_ReturnsFullDocumentContent()
    {
        var client = _factory.CreateClient();
        var e = await client.GetFromJsonAsync<JsonElement>("/api/inbox/eml-001");
        Assert.Equal("eml-001", e.GetProperty("id").GetString());
        Assert.False(string.IsNullOrWhiteSpace(e.GetProperty("document").GetProperty("content").GetString()));
    }

    [Fact]
    public async Task Process_Nordwind_RunsFullPipeline()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/process/eml-001", null);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("Nordwind Energy AG", c.GetProperty("entities").GetProperty("primaryOrganisation").GetString());
        Assert.True(c.GetProperty("insights").GetArrayLength() > 0);
        Assert.True(c.GetProperty("sourceHits").GetArrayLength() > 0);
        Assert.True(c.GetProperty("brief").GetProperty("keyFindings").GetArrayLength() > 0);
        Assert.False(string.IsNullOrWhiteSpace(c.GetProperty("reportEmail").GetProperty("to").GetString()));
    }

    [Fact]
    public async Task Process_Spam_IsQuarantined()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/process/eml-004", null);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, c.GetProperty("sourceHits").GetArrayLength());
        Assert.Contains("intake-triage", c.GetProperty("reportEmail").GetProperty("to").GetString());
    }

    [Fact]
    public async Task Process_AdHoc_WithPastedDocument_Works()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            fromName = "Test User",
            from = "test@acme.com",
            subject = "RFP for a payments resilience review",
            body = "See attached.",
            documentType = "RFP",
            documentContent = "AuroraPay needs a multi-region PCI-DSS payments architecture after an outage. Budget AUD 400k-600k."
        };
        var res = await client.PostAsJsonAsync("/api/process", payload);
        res.EnsureSuccessStatusCode();
        var c = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(c.GetProperty("insights").GetArrayLength() > 0);
        var caseId = c.GetProperty("caseId").GetString();

        // Report is downloadable as markdown.
        var report = await client.GetAsync($"/api/cases/{caseId}/report");
        report.EnsureSuccessStatusCode();
        Assert.Equal("text/markdown", report.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Process_UnknownEmail_Returns404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/process/does-not-exist", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Cases_AreListedAfterProcessing()
    {
        var client = _factory.CreateClient();
        await client.PostAsync("/api/process/eml-002", null);
        var arr = await client.GetFromJsonAsync<JsonElement>("/api/cases");
        Assert.True(arr.GetArrayLength() >= 1);
    }
}
