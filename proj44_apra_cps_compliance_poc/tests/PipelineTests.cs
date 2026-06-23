using Proj44.Compliance.Web.Services;
using Xunit;

namespace Proj44.Compliance.Tests;

/// <summary>
/// Tests for the six-stage agent pipeline contract: the offline engine must emit one ordered
/// agent-step per stage, the instruction catalogue must expose all six stages with distinct agent
/// personas, and the engine must be deterministic and complete with no Azure access.
/// </summary>
public sealed class PipelineTests
{
    [Fact]
    public void Agent_instructions_expose_the_six_ordered_stages()
    {
        Assert.Equal(6, AgentInstructions.Stages.Count);
        Assert.Equal(
            new[] { "ingestion", "requirements", "policies", "standards", "controls", "gap" },
            AgentInstructions.Order);

        foreach (var key in AgentInstructions.Order)
        {
            var s = AgentInstructions.Stage(key);
            Assert.False(string.IsNullOrWhiteSpace(s.Agent), $"stage {key} missing agent name");
            Assert.False(string.IsNullOrWhiteSpace(s.Goal), $"stage {key} missing goal");
            Assert.False(string.IsNullOrWhiteSpace(s.Instructions), $"stage {key} missing instructions");
        }
    }

    [Fact]
    public void Each_stage_has_a_distinct_agent_persona()
    {
        var agents = AgentInstructions.Stages.Select(s => s.Agent).ToArray();
        Assert.Equal(agents.Length, agents.Distinct().Count());
    }

    [Fact]
    public async Task Offline_engine_logs_all_six_stages_in_order()
    {
        var engine = new OfflineComplianceEngine();
        var fw = await engine.BuildAsync();

        Assert.Equal("offline", fw.Engine);
        Assert.Equal("completed", fw.Status);

        var steps = fw.AgentSteps.Select(s => s.Step).ToArray();
        Assert.Equal(AgentInstructions.Order, steps);

        // Each logged step carries the matching stage agent persona.
        foreach (var step in fw.AgentSteps)
        {
            var stage = AgentInstructions.Stage(step.Step);
            Assert.Equal(stage.Agent, step.Agent);
            Assert.False(string.IsNullOrWhiteSpace(step.Summary));
        }
    }

    [Fact]
    public async Task Offline_engine_is_deterministic_in_scale()
    {
        var engine = new OfflineComplianceEngine();
        var a = await engine.BuildAsync();
        var b = await engine.BuildAsync();

        Assert.Equal(a.Policies.Count, b.Policies.Count);
        Assert.Equal(a.Controls.Count, b.Controls.Count);
        Assert.Equal(a.Requirements.Count, b.Requirements.Count);
        Assert.Equal(a.Standards.Count, b.Standards.Count);
        Assert.True(a.Policies.Count >= 130);
        Assert.True(a.Controls.Count >= 30);
    }
}
