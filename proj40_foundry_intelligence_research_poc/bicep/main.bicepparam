using 'main.bicep'

// Base name drives all resource names; keep it short.
param baseName = 'proj40'

// Region for the POC. Australia East matches the template-repo-agent reference.
param location = 'australiaeast'

// App Service plan SKU. S1 Standard is a cost-effective production-class tier for a POC.
param appServiceSku = 'S1'

// Set to true to provision + use the live Foundry prompt agent. The app also works with this false,
// using its deterministic offline research engine (handy if model quota is constrained).
param foundryEnabled = true

// Model deployment for the prompt agent. gpt-4o is broadly available; adjust to your quota/region.
param modelDeploymentName = 'gpt-4o'
param modelName = 'gpt-4o'
param modelVersion = '2024-11-20'

param containerName = 'cases'
