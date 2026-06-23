using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Proj43.FinOps.Web.Models;
using Proj43.FinOps.Web.Services;
using Proj43.FinOps.Web.Services.Foundry;
using Xunit;

namespace Proj43.FinOps.Tests;

/// <summary>
/// Verifies the chatbot is wired to a REAL Microsoft Foundry agent (created from config) using the
/// hosted Microsoft Fabric data-agent tool - not a plain LLM wrapper and not a function stub.
/// These tests run fully offline: they assert tool/agent construction + DI selection + diagnostics
/// surface, without contacting Azure (no agent is actually run here).
/// </summary>
public sealed class FoundryAgentWiringTests
{
    [Fact]
    public void Fabric_Tool_Is_Real_Hosted_Foundry_Tool_Not_A_Function_Stub()
    {
        var fabric = new FabricOptions
        {
            ConnectionId = "/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/acct/projects/proj/connections/fabricdataagent",
        };

        AITool? tool = FoundryAgentFactory.TryCreateFabricTool(fabric);

        Assert.NotNull(tool);
        // It must be a genuine hosted Foundry tool, NOT a locally-defined function tool
        // (the old FabricToolStub used AIFunctionFactory.Create -> an AIFunction).
        Assert.False(tool is AIFunction, "Fabric tool must be the hosted Foundry Fabric tool, not a local AIFunction stub.");
        // Hosted Responses-API tools surface as ResponseToolAITool (a real server-side tool wrapper),
        // never as a locally-built function stub.
        Assert.Equal("ResponseToolAITool", tool!.GetType().Name);
    }

    [Fact]
    public void No_Fabric_Connection_Yields_No_Tool()
    {
        var tool = FoundryAgentFactory.TryCreateFabricTool(new FabricOptions { ConnectionId = null });
        Assert.Null(tool);
    }

    [Fact]
    public void Instructions_Reference_Fabric_And_Forbid_Fabrication()
    {
        var text = FoundryAgentFactory.BuildInstructions();
        Assert.Contains("Fabric", text);
        Assert.Contains("Never fabricate", text);
    }

    [Fact]
    public void Diagnostics_Record_And_Snapshot_RoundTrip()
    {
        var diag = new FoundryAgentDiagnostics();
        Assert.False(diag.Provisioned);

        diag.RecordProvisioned("agent_abc123", "proj43-finops-assistant", "gpt-4o-mini", fabricToolWired: true, "conn/fabric", mcpToolCount: 0);
        diag.RecordTurn("foundry");

        Assert.True(diag.Provisioned);
        Assert.Equal("agent_abc123", diag.AgentId);
        Assert.True(diag.FabricToolWired);
        Assert.Equal("foundry", diag.LastEngineUsed);

        var json = JsonSerializer.Serialize(diag.Snapshot());
        Assert.Contains("agent_abc123", json);
        Assert.Contains("\"fabricToolWired\":true", json);
        Assert.Contains("\"lastEngineUsed\":\"foundry\"", json);
    }

    [Fact]
    public void DI_Selects_Foundry_Agent_When_Configured()
    {
        // A configured Foundry endpoint must cause the live FoundryFinOpsAgent to back IFinOpsAgent,
        // proving the chat path routes to the agent (it still falls back to offline at runtime if the
        // endpoint is unreachable, but the selected implementation must be the Foundry one).
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Foundry:Enabled", "true");
            b.UseSetting("Foundry:ProjectEndpoint", "https://example-foundry.services.ai.azure.com/api/projects/proj43-proj");
            b.UseSetting("Foundry:ModelDeploymentName", "gpt-4o-mini");
            b.UseSetting("Foundry:AgentName", "proj43-finops-assistant");
            b.UseSetting("Fabric:ConnectionId", "/subscriptions/x/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/a/projects/p/connections/fabricdataagent");
        });

        using var scope = factory.Services.CreateScope();
        var selected = scope.ServiceProvider.GetRequiredService<IFinOpsAgent>();
        Assert.Equal("foundry", selected.Name);
        Assert.IsType<FoundryFinOpsAgent>(selected);
    }

    [Fact]
    public async Task Agent_Endpoint_Reports_Configured_Foundry_And_Fabric()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Foundry:Enabled", "true");
            b.UseSetting("Foundry:ProjectEndpoint", "https://example-foundry.services.ai.azure.com/api/projects/proj43-proj");
            b.UseSetting("Foundry:ModelDeploymentName", "gpt-4o-mini");
            b.UseSetting("Foundry:AgentName", "proj43-finops-assistant");
            b.UseSetting("Fabric:ConnectionId", "/subscriptions/x/resourceGroups/rg/providers/Microsoft.CognitiveServices/accounts/a/projects/p/connections/fabricdataagent");
        });

        var client = factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/agent");

        Assert.True(doc.GetProperty("foundryConfigured").GetBoolean());
        Assert.True(doc.GetProperty("fabricConfigured").GetBoolean());
        Assert.Equal("proj43-finops-assistant", doc.GetProperty("configuredAgentName").GetString());
        Assert.Equal("gpt-4o-mini", doc.GetProperty("configuredModel").GetString());
        Assert.True(doc.GetProperty("projectEndpointConfigured").GetBoolean());
    }

    [Fact]
    public async Task Health_Reports_Foundry_Engine_When_Configured()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Foundry:Enabled", "true");
            b.UseSetting("Foundry:ProjectEndpoint", "https://example-foundry.services.ai.azure.com/api/projects/proj43-proj");
            b.UseSetting("Foundry:ModelDeploymentName", "gpt-4o-mini");
        });

        var client = factory.CreateClient();
        var doc = await client.GetFromJsonAsync<JsonElement>("/api/health");
        Assert.Equal("ok", doc.GetProperty("status").GetString());
        Assert.Equal("foundry", doc.GetProperty("engine").GetString());
        Assert.True(doc.GetProperty("foundryConfigured").GetBoolean());
    }
}
