@description('Azure location')
param location string

@description('Web App name')
param webAppName string

@description('App Service plan name')
param appServicePlanName string

@description('SKU for the App Service plan (Standard tier per repo policy)')
param appServiceSku string = 'S1'

@description('Application Insights connection string')
param appInsightsConnectionString string

@description('Foundry project endpoint (AI Foundry API)')
param foundryProjectEndpoint string

@description('Model deployment name used by the prompt agents')
param foundryModelDeploymentName string

@description('Enable the live Foundry agent path (false => deterministic offline pipeline)')
param foundryEnabled bool = true

@description('Storage account name for the submission journal / artefacts')
param storageAccountName string

@description('Local data folder for durable case-journal persistence on App Service')
param localDataFolder string = '/home/site/data'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServiceSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      alwaysOn: true
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appCommandLine: 'dotnet Proj45.RelayDesk.Web.dll'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          name: 'Foundry__Enabled'
          value: string(foundryEnabled)
        }
        {
          name: 'Foundry__ProjectEndpoint'
          value: foundryProjectEndpoint
        }
        {
          name: 'Foundry__ModelDeployment'
          value: foundryModelDeploymentName
        }
        {
          name: 'Storage__BlobAccountUrl'
          value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}'
        }
        {
          name: 'Storage__DataDirectory'
          value: localDataFolder
        }
      ]
    }
  }
}

output webAppName string = webApp.name
output defaultHostName string = webApp.properties.defaultHostName
output principalId string = webApp.identity.principalId
