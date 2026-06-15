using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Proj39.IntakeOrigination.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_healthy()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/health");
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<HealthDto>();
        Assert.Equal("healthy", body!.Status);
    }

    [Fact]
    public async Task Emails_are_seeded()
    {
        var client = _factory.CreateClient();
        var emails = await client.GetFromJsonAsync<List<EmailListDto>>("/api/emails");
        Assert.NotNull(emails);
        Assert.True(emails!.Count >= 5);
    }

    [Fact]
    public async Task Process_known_email_returns_completed_case()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/cases/process/eml-001", null);
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CaseDto>();
        Assert.Equal("completed", dto!.Status);
        Assert.Equal("Hot", dto.Triage.Classification);
        Assert.False(string.IsNullOrWhiteSpace(dto.Report.GeneratedMarkdown));
    }

    [Fact]
    public async Task Process_adhoc_email_runs_pipeline()
    {
        var client = _factory.CreateClient();
        var payload = new { from = "test@acme.com", fromName = "Test", subject = "Need a data platform", body = "We have budget of AUD $250k approved and want to go live this quarter. Our CTO is sponsoring." };
        var res = await client.PostAsJsonAsync("/api/cases/process", payload);
        res.EnsureSuccessStatusCode();
        var dto = await res.Content.ReadFromJsonAsync<CaseDto>();
        Assert.Equal("completed", dto!.Status);
    }

    [Fact]
    public async Task Process_unknown_email_returns_404()
    {
        var client = _factory.CreateClient();
        var res = await client.PostAsync("/api/cases/process/does-not-exist", null);
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Report_download_returns_markdown()
    {
        var client = _factory.CreateClient();
        var run = await client.PostAsync("/api/cases/process/eml-003", null);
        var dto = await run.Content.ReadFromJsonAsync<CaseDto>();
        var res = await client.GetAsync($"/api/cases/{dto!.CaseId}/report");
        res.EnsureSuccessStatusCode();
        Assert.Equal("text/markdown", res.Content.Headers.ContentType!.MediaType);
        var md = await res.Content.ReadAsStringAsync();
        Assert.Contains("Origination Study", md);
    }

    // ---- DTOs ----
    private sealed record HealthDto(string Status, string Engine, bool FoundryConfigured);
    private sealed record EmailListDto(string Id, string Subject);
    private sealed record CaseDto(string CaseId, string Status, TriageDto Triage, ReportDto Report);
    private sealed record TriageDto(string Classification, int Score);
    private sealed record ReportDto(string Disposition, string GeneratedMarkdown);
}
