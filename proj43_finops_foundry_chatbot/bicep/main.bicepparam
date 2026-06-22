using 'main.bicep'

// Base name drives all resource names; keep it short.
param baseName = 'proj43'

// Region for the POC. Australia East matches the template-repo-agent reference.
param location = 'australiaeast'

// App Service plan SKU. P0v3 is a cost-effective production-class tier for a POC.
param appServiceSku = 'P0v3'

// Set to true to provision + use the live Foundry FinOps agent. The app also works with this false,
// using its deterministic offline FinOps engine (handy if model quota is constrained).
param foundryEnabled = true

// Model deployment for the FinOps agent. A smaller model (gpt-4o-mini) is recommended for Fabric-tool
// orchestration per Microsoft guidance; adjust to your quota/region.
param modelDeploymentName = 'gpt-4o-mini'
param modelName = 'gpt-4o-mini'
param modelVersion = '2024-07-18'

param containerName = 'transcripts'

// Microsoft Fabric data agent connection (optional). Leave empty for the POC; set after publishing a
// Fabric data agent and creating a Foundry connection (workspace-id + artifact-id) to enable the
// live Fabric data-agent tool with identity passthrough.
param fabricConnectionId = ''
param fabricWorkspaceId = ''
param fabricArtifactId = ''
