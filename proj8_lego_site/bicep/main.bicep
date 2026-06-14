// Main infra for 20260606_lego_site
// Deploys: Static Web App (frontend), Azure Functions + Storage (backend), Cosmos DB.
targetScope = 'resourceGroup'

@description('Base name used to derive resource names.')
param baseName string = 'lego20260606'

@description('Primary region for most resources.')
param location string = resourceGroup().location

@description('Region for the Static Web App (limited region availability).')
param swaLocation string = 'eastasia'

@description('Provision Cosmos DB. Leave false for the mock-store (in-memory) first cut.')
param deployCosmos bool = false

var uniqueSuffix = uniqueString(resourceGroup().id, baseName)
var swaName = '${baseName}-swa'
var functionAppName = '${baseName}-func-${uniqueSuffix}'
var storageName = toLower('st${baseName}${uniqueSuffix}')
var cosmosName = toLower('cosmos-${baseName}-${uniqueSuffix}')
var cosmosDatabaseName = 'legodb'

module cosmos 'cosmos.bicep' = if (deployCosmos) {
  name: 'cosmos'
  params: {
    location: location
    accountName: cosmosName
    databaseName: cosmosDatabaseName
  }
}

module swa 'swa.bicep' = {
  name: 'swa'
  params: {
    location: swaLocation
    staticWebAppName: swaName
    sku: 'Free'
  }
}

module func 'functionapp.bicep' = {
  name: 'functionapp'
  params: {
    location: location
    functionAppName: functionAppName
    storageAccountName: take(storageName, 24)
    cosmosEndpoint: deployCosmos ? cosmos.outputs.endpoint : ''
    cosmosKey: deployCosmos ? listKeys(resourceId('Microsoft.DocumentDB/databaseAccounts', cosmosName), '2024-11-15').primaryMasterKey : ''
    cosmosDatabase: cosmosDatabaseName
  }
}

output staticWebAppName string = swa.outputs.staticWebAppName
output staticWebAppHostname string = swa.outputs.defaultHostname
output functionAppName string = func.outputs.functionAppName
output functionAppHostname string = func.outputs.functionAppHostname
output cosmosAccountName string = deployCosmos ? cosmos.outputs.accountName : ''
