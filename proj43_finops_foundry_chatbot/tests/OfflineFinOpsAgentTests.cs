using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Proj43.FinOps.Web.Models;
using Proj43.FinOps.Web.Services;
using Xunit;

namespace Proj43.FinOps.Tests;

/// <summary>Intent classification + grounded-answer checks for the deterministic offline agent.</summary>
public sealed class OfflineFinOpsAgentTests
{
    private static OfflineFinOpsAgent Build()
    {
        var data = new FinOpsDataset(today: new DateOnly(2026, 6, 30), seed: 43);
        var analytics = new FinOpsAnalytics(data);
        var env = new TestEnv();
        var store = new ConversationStore(
            Options.Create(new ChatOptions { PersistTranscripts = false }),
            Options.Create(new StorageOptions()),
            env, NullLogger<ConversationStore>.Instance);
        return new OfflineFinOpsAgent(analytics, store, new MarkdownRenderer());
    }

    [Theory]
    [InlineData("What did we spend last month?", OfflineFinOpsAgent.Intent.TotalSpend)]
    [InlineData("show me the 6-month trend", OfflineFinOpsAgent.Intent.Trend)]
    [InlineData("top 5 services by cost", OfflineFinOpsAgent.Intent.TopServices)]
    [InlineData("which resource groups cost the most", OfflineFinOpsAgent.Intent.TopResourceGroups)]
    [InlineData("any anomalies or spikes?", OfflineFinOpsAgent.Intent.Anomalies)]
    [InlineData("how is our reservation coverage", OfflineFinOpsAgent.Intent.Coverage)]
    [InlineData("where can we save money", OfflineFinOpsAgent.Intent.Recommendations)]
    [InlineData("break down cost by team", OfflineFinOpsAgent.Intent.Showback)]
    [InlineData("forecast next month spend", OfflineFinOpsAgent.Intent.Forecast)]
    [InlineData("help", OfflineFinOpsAgent.Intent.Help)]
    public void Classify_Maps_Expected_Intent(string message, OfflineFinOpsAgent.Intent expected)
    {
        var agent = Build();
        Assert.Equal(expected, agent.Classify(message));
    }

    [Fact]
    public void Answer_TotalSpend_Contains_Currency_And_Number()
    {
        var agent = Build();
        var (intent, md) = agent.Answer("what did we spend last month");
        Assert.Equal("TotalSpend", intent);
        Assert.Contains("USD", md);
        Assert.Contains("latest full month", md);
    }

    [Fact]
    public void Answer_Anomalies_Mentions_Sql_Spike()
    {
        var agent = Build();
        var (_, md) = agent.Answer("any anomalies?");
        Assert.Contains("Azure SQL Database", md);
    }

    [Fact]
    public void Answer_Trend_Renders_Markdown_Table()
    {
        var agent = Build();
        var (_, md) = agent.Answer("6-month trend");
        Assert.Contains("| Month | Spend | MoM |", md);
    }

    [Fact]
    public async Task ReplyAsync_Produces_Html_And_Persists_Conversation()
    {
        var agent = Build();
        var resp = await agent.ReplyAsync("", "top services by cost");
        Assert.False(string.IsNullOrWhiteSpace(resp.ConversationId));
        Assert.Equal("offline", resp.Engine);
        Assert.Contains("<table", resp.ReplyHtml);

        // Second turn reuses the conversation id.
        var resp2 = await agent.ReplyAsync(resp.ConversationId, "and by team?");
        Assert.Equal(resp.ConversationId, resp2.ConversationId);
    }

    [Fact]
    public async Task StreamAsync_Emits_Meta_Tokens_And_Done()
    {
        var agent = Build();
        var types = new List<string>();
        string? convId = null;
        await foreach (var ev in agent.StreamAsync("", "where can we save money"))
        {
            types.Add(ev.Type);
            convId ??= ev.ConversationId;
        }
        Assert.Contains("meta", types);
        Assert.Contains("token", types);
        Assert.Contains("done", types);
        Assert.False(string.IsNullOrWhiteSpace(convId));
    }

    private sealed class TestEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ApplicationName { get; set; } = "tests";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
    }
}
