using 'main.bicep'

// Base name drives all resource names; keep it short. Must be the project_id per repo policy.
param baseName = 'proj40'

// Region for the POC.
param location = 'australiaeast'

// App Service plan SKU — Standard tier (repo policy: Azure Web App must use standard SKU).
param appServiceSku = 'S1'

// Set to true to provision + use the live Foundry prompt agents. The app also works with this false,
// using its deterministic offline pipeline (handy if model quota is constrained).
param foundryEnabled = true

// Model deployment for the prompt agents. gpt-4o is broadly available; adjust to your quota/region.
param modelDeploymentName = 'gpt-4o'
param modelName = 'gpt-4o'
param modelVersion = '2024-11-20'

param containerName = 'intake'
