using Microsoft.Extensions.AI;
using Microsoft.Agents.AI.Foundry;
using Azure.AI.Projects.Agents;
using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services.Foundry;

/// <summary>
/// Builds the tool set that is attached to the Microsoft Foundry agent at creation time.
///
/// This is the single, testable place that turns configuration into <b>real</b> Foundry tools:
///   . The published Microsoft Fabric data agent is attached via
///     <see cref="FoundryAITool.CreateMicrosoftFabricTool(FabricDataAgentToolOptions)"/> using the
///     Foundry project connection id (<see cref="FabricOptions.ConnectionId"/>). This is the genuine
///     hosted Fabric tool the model invokes with identity passthrough - <b>not</b> a function stub.
/// The agent itself is then created from these tools by
/// <see cref="AzureAIProjectChatClientExtensions.AsAIAgent"/> in <see cref="FoundryFinOpsAgent"/>.
/// </summary>
internal static class FoundryAgentFactory
{
    /// <summary>Instructions (persona) given to the created Foundry agent.</summary>
    public static string BuildInstructions() =>
        AgentPersona.SystemPersona +
        "\n\nWhen you need cost/usage figures, call the Microsoft Fabric data-agent tool. " +
        "Never fabricate numbers - if the tool returns nothing, say so.";

    /// <summary>
    /// Construct the real Fabric data-agent tool from the Foundry connection id. Returns null when
    /// no Fabric connection is configured (the agent is then created without the Fabric tool).
    /// </summary>
    public static AITool? TryCreateFabricTool(FabricOptions fabric)
    {
        if (!fabric.IsConfigured) return null;

        var options = new FabricDataAgentToolOptions();
        options.ProjectConnections.Add(new ToolProjectConnection(fabric.ConnectionId!));
        return FoundryAITool.CreateMicrosoftFabricTool(options);
    }
}
