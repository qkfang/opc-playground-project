using Xunit;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Proj37.CostEstimator.Tests;

/// <summary>
/// End-to-end API tests via WebApplicationFactory. Foundry is unconfigured in test, so the
/// deterministic offline engine runs — exercising the full ingest -> estimate -> Excel pipeline.
/// </summary>
public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok_and_offline_engine()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/health");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("healthy", body);
        Assert.Contains("offline", body); // no Foundry config in tests
    }

    [Fact]
    public async Task Sample_estimation_runs_and_workbook_downloads()
    {
        var client = _factory.CreateClient();

        var run = await client.PostAsync("/api/estimations/sample", content: null);
        run.EnsureSuccessStatusCode();
        var job = await run.Content.ReadFromJsonAsync<JobDto>();
        Assert.NotNull(job);
        Assert.Equal("completed", job!.status);
        Assert.False(string.IsNullOrWhiteSpace(job.jobId));
        Assert.True(job.cost!.lineItems!.Count > 0);

        // Download the generated workbook.
        var wb = await client.GetAsync($"/api/estimations/{job.jobId}/workbook");
        wb.EnsureSuccessStatusCode();
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            wb.Content.Headers.ContentType?.MediaType);
        var bytes = await wb.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2000);

        // It should now appear in the list.
        var list = await client.GetFromJsonAsync<List<ListDto>>("/api/estimations");
        Assert.NotNull(list);
        Assert.Contains(list!, x => x.jobId == job.jobId);
    }

    [Fact]
    public async Task Upload_estimation_via_multipart_works()
    {
        var client = _factory.CreateClient();
        using var form = new MultipartFormDataContent();
        var doc = new StringContent("Project: Upload Test\nA web app with API and an AI Foundry agent for document file search.",
            Encoding.UTF8, "text/markdown");
        form.Add(doc, "files", "upload-brief.md");

        var run = await client.PostAsync("/api/estimations", form);
        run.EnsureSuccessStatusCode();
        var job = await run.Content.ReadFromJsonAsync<JobDto>();
        Assert.Equal("completed", job!.status);
        Assert.Contains("Upload Test", job.scope!.projectName);
    }

    [Fact]
    public async Task Missing_job_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/estimations/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // Minimal DTOs mirroring the JSON shape we assert on.
    private sealed record JobDto(string jobId, string status, ScopeDto? scope, CostDto? cost);
    private sealed record ScopeDto(string projectName);
    private sealed record CostDto(List<LineDto>? lineItems);
    private sealed record LineDto(string service, decimal monthlyCost);
    private sealed record ListDto(string jobId, string project);
}
