@description('Project identifier used as the resource name prefix.')
param projectId string = '20260606_fruit_robotics_news'

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('App Service plan SKU.')
@allowed([
  'B1'
  'S1'
  'P1v3'
])
param appServicePlanSku string = 'B1'

var resourceNamePrefix = replace(toLower(projectId), '_', '-')
var appServicePlanName = '${resourceNamePrefix}-plan'
var webAppName = '${resourceNamePrefix}-api-${uniqueString(resourceGroup().id)}'
var staticWebAppName = '${resourceNamePrefix}-web-${uniqueString(resourceGroup().id)}'
var appServicePlanSkuTier = appServicePlanSku == 'B1'
  ? 'Basic'
  : appServicePlanSku == 'S1'
    ? 'Standard'
    : 'PremiumV3'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: appServicePlanSku
    tier: appServicePlanSkuTier
    size: appServicePlanSku
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
    }
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
}

output appServicePlanName string = appServicePlan.name
output webAppName string = webApp.name
output webAppDefaultHostName string = webApp.properties.defaultHostName
output staticWebAppName string = staticWebApp.name
output staticWebAppDefaultHostName string = staticWebApp.properties.defaultHostname
