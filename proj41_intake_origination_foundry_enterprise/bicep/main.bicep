targetScope = 'resourceGroup'

@description('Azure location for all resources')
param location string = resourceGroup().location

@description('Base name used to derive resource names (keep short; <= 13 chars)')
@minLength(3)
@maxLength(13)
param baseName string = 'proj41'

@description('SKU for the App Service plan (Standard tier per repo policy)')
@allowed([
  'S1'
  'S2'
  'P0v3'
  'P1v3'
])
param appServiceSku string = 'S1'

@description('Enable the live Foundry agent path. When false the app uses its deterministic offline pipeline.')
param foundryEnabled bool = true

@description('Restore a soft-deleted Foundry/AI Services account with the same deterministic name if one exists.')
param restoreDeletedFoundryAccount bool = false

@description('Restore a soft-deleted Key Vault with the same deterministic name if one exists.')
param restoreDeletedKeyVault bool = false

@description('Model deployment name for the prompt agents')
param modelDeploymentName string = 'gpt-4o'

@description('Model name')
param modelName string = 'gpt-4o'

@description('Model version')
param modelVersion string = '2024-11-20'

@description('Storage blob container for the submission journal / artefacts')
param containerName string = 'submissions'

// Deterministic, short, lower-cased resource names.
var suffix = toLower(uniqueString(resourceGroup().id, baseName))
var shortSuffix = substring(suffix, 0, 6)

var logAnalyticsName = '${baseName}-law-${shortSuffix}'
var appInsightsName = '${baseName}-appi-${shortSuffix}'
var storageAccountName = toLower(replace('${baseName}st${shortSuffix}', '-', ''))
var keyVaultName = take(toLower('${baseName}kv${shortSuffix}'), 24)
var foundryServicesName = '${baseName}-ais-${shortSuffix}'
var foundryProjectName = '${baseName}-proj'
var appServicePlanName = '${baseName}-plan-${shortSuffix}'
var webAppName = '${baseName}-web-${shortSuffix}'

// Built-in role definition IDs.
var roleStorageBlobDataContributor = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var roleKeyVaultSecretsUser = '4633458b-17de-408a-b874-0445c86b69e6'
var roleCognitiveServicesUser = 'a97b65f3-24c7-4388-baec-2e87135dc908'
var roleCognitiveServicesOpenAIUser = '5e0bd9bd-7b93-4f28-af87-19fc36ad61bd'

module monitoring './modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    location: location
    logAnalyticsName: logAnalyticsName
    appInsightsName: appInsightsName
  }
}

module storage './modules/storage.bicep' = {
  name: 'storage'
  params: {
    location: location
    storageAccountName: storageAccountName
    containerName: containerName
  }
}

module keyvault './modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    location: location
    keyVaultName: keyVaultName
    restoreSoftDeletedKeyVault: restoreDeletedKeyVault
  }
}

module foundry './modules/foundry.bicep' = {
  name: 'foundry'
  params: {
    location: location
    foundryServicesName: foundryServicesName
    foundryProjectName: foundryProjectName
    restoreDeletedFoundryAccount: restoreDeletedFoundryAccount
    modelDeploymentName: modelDeploymentName
    modelName: modelName
    modelVersion: modelVersion
  }
}

module appService './modules/appservice.bicep' = {
  name: 'appservice'
  params: {
    location: location
    webAppName: webAppName
    appServicePlanName: appServicePlanName
    appServiceSku: appServiceSku
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    foundryProjectEndpoint: foundry.outputs.aiProjectEndpoint
    foundryModelDeploymentName: foundry.outputs.modelDeploymentName
    foundryEnabled: foundryEnabled
    storageAccountName: storage.outputs.storageAccountName
  }
}

// ---- RBAC: grant the web app's managed identity least-privilege access ----

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' existing = {
  name: storageAccountName
}

resource keyVaultRes 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: keyVaultName
}

resource foundryAccount 'Microsoft.CognitiveServices/accounts@2025-10-01-preview' existing = {
  name: foundryServicesName
}

resource blobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: storageAccount
  name: guid(storageAccount.id, webAppName, roleStorageBlobDataContributor)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleStorageBlobDataContributor)
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource kvRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: keyVaultRes
  name: guid(keyVaultRes.id, webAppName, roleKeyVaultSecretsUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleKeyVaultSecretsUser)
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource cogUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundryAccount
  name: guid(foundryAccount.id, webAppName, roleCognitiveServicesUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleCognitiveServicesUser)
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

resource openAiUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: foundryAccount
  name: guid(foundryAccount.id, webAppName, roleCognitiveServicesOpenAIUser)
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', roleCognitiveServicesOpenAIUser)
    principalId: appService.outputs.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppName string = appService.outputs.webAppName
output webAppUrl string = 'https://${appService.outputs.defaultHostName}'
output foundryProjectEndpoint string = foundry.outputs.aiProjectEndpoint
output storageAccountName string = storage.outputs.storageAccountName
output keyVaultName string = keyvault.outputs.keyVaultName
