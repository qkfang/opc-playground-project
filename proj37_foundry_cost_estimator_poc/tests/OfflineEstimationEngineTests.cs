using Xunit;
using Proj37.CostEstimator.Web.Models;
using Proj37.CostEstimator.Web.Services;

namespace Proj37.CostEstimator.Tests;

public class OfflineEstimationEngineTests
{
    private static EstimationResult JobFrom(string text, string file = "brief.md")
    {
        var job = new EstimationResult();
        job.Documents.Add(new IngestedDocument { FileName = file, ExtractedText = text, WordCount = text.Split(' ').Length });
        return job;
    }

    [Fact]
    public async Task Estimate_produces_scope_requirements_and_costs()
    {
        var text = """
            Project: Contoso AI Intake
            Build a web app with an API that uses an AI/LLM (Foundry) prompt agent to ingest documents
            (PDF, DOCX) for RAG/file search. Store data in SQL and Cosmos DB. This is for an enterprise
            with high throughput, millions of requests, and contains PII subject to compliance.
            """;
        var engine = new OfflineEstimationEngine();
        var job = await engine.EstimateAsync(JobFrom(text));

        Assert.Equal("completed", job.Status);
        Assert.Equal("offline", job.Engine);
        Assert.False(string.IsNullOrWhiteSpace(job.Scope.ProjectName));
        Assert.Contains("Contoso AI Intake", job.Scope.ProjectName);
        Assert.NotEmpty(job.Requirements);
        Assert.NotEmpty(job.Cost.LineItems);
        Assert.True(job.Cost.MonthlyTotal > 0);
        Assert.Equal(3, job.AgentSteps.Count); // scope, requirements, cost
    }

    [Fact]
    public async Task Detects_ai_and_filesearch_signals()
    {
        var job = await new OfflineEstimationEngine().EstimateAsync(
            JobFrom("An AI agent with Foundry that does document file search and RAG grounding."));

        // AI workload should pull in Foundry token line items and an AI Search line item.
        Assert.Contains(job.Cost.LineItems, l => l.Category == "AI");
        Assert.Contains(job.Requirements, r => r.Category == "AI");
    }

    [Fact]
    public async Task Enterprise_scale_costs_more_than_poc_scale()
    {
        var poc = await new OfflineEstimationEngine().EstimateAsync(
            JobFrom("Small POC web app prototype demo for a single team."));
        var ent = await new OfflineEstimationEngine().EstimateAsync(
            JobFrom("Enterprise mission critical web app, global, millions of users, high throughput at scale."));

        Assert.True(ent.Cost.MonthlyTotalWithContingency > poc.Cost.MonthlyTotalWithContingency,
            $"enterprise {ent.Cost.MonthlyTotalWithContingency} should exceed poc {poc.Cost.MonthlyTotalWithContingency}");
    }

    [Fact]
    public async Task Always_includes_baseline_security_and_observability()
    {
        var job = await new OfflineEstimationEngine().EstimateAsync(JobFrom("Basic internal web app."));
        Assert.Contains(job.Cost.LineItems, l => l.Category == "Security");
        Assert.Contains(job.Cost.LineItems, l => l.Category == "Observability");
        Assert.Contains(job.Cost.LineItems, l => l.Service.Contains("Blob"));
    }

    [Fact]
    public async Task Contingency_total_is_greater_than_raw_total()
    {
        var job = await new OfflineEstimationEngine().EstimateAsync(JobFrom("Web app with API."));
        Assert.True(job.Cost.MonthlyTotalWithContingency > job.Cost.MonthlyTotal);
        Assert.Equal(Math.Round(job.Cost.MonthlyTotal * 12m, 2), job.Cost.AnnualTotal);
    }
}
