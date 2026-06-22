using System.ComponentModel;
using Microsoft.Extensions.AI;
using Proj43.FinOps.Web.Models;

namespace Proj43.FinOps.Web.Services.Foundry;

/// <summary>
/// Lightweight function-tool stub that represents the published Microsoft Fabric data agent to the
/// Foundry model. In a fully-provisioned environment the Fabric data agent is added as a Foundry
/// project connection (workspace-id + artifact-id) and invoked with identity passthrough; the model
/// sees it as a "Fabric tool". This stub gives the agent a callable surface + description in code so
/// the integration path is explicit and testable, and documents exactly where the live preview tool
/// type is substituted in. It returns a clear marker rather than fabricating data.
/// </summary>
internal static class FabricToolStub
{
    public static AITool Create(FabricOptions fabric)
    {
        [Description("Query governed enterprise cost & usage data from Microsoft Fabric (OneLake) via the published Fabric data agent. Input is a natural-language analytical question about Azure spend, usage, trends, anomalies, commitment coverage, or showback.")]
        string QueryFabric(
            [Description("Natural-language FinOps question to run against Fabric, e.g. 'total spend by service for last month'.")] string question)
        {
            // Live wiring substitutes the Foundry Fabric data agent connection here
            // (connection id: fabric.ConnectionId). Identity passthrough (OBO) runs NL2SQL over OneLake.
            return $"[FABRIC_DATA_AGENT not live in this environment] " +
                   $"Connection='{fabric.ConnectionId ?? "(unset)"}'. " +
                   $"Question='{question}'. The application falls back to its deterministic FinOps engine " +
                   $"for grounded figures when the live Fabric data agent is unavailable.";
        }

        return AIFunctionFactory.Create(QueryFabric);
    }
}
