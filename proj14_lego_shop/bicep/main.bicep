targetScope = 'resourceGroup'

@description('Base name used to derive deterministic resource names.')
param baseName string = 'legoshop20260613'

@description('Region for the App Service plan and web app.')
param location string = resourceGroup().location

@description('SKU name for the App Service plan. PremiumV3 (P1V3) so the Kudu build/deploy and Next.js runtime have enough CPU/memory; B1 OOMs during Kudu node-module optimize/rsync.')
param appServicePlanSku string = 'P1v3'

var uniqueSuffix = substring(toLower(uniqueString(resourceGroup().id, baseName)), 0, 6)
var appServicePlanName = '${baseName}-plan-${uniqueSuffix}'
var webAppName = '${baseName}-web-${uniqueSuffix}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
    capacity: 1
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
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|22-lts'
      alwaysOn: true
      // Next.js standalone server bundle. Deploy ships a prebuilt package
      // (.next/standalone) and starts it with `node server.js`. next listens on PORT.
      appCommandLine: 'node server.js'
      appSettings: [
        {
          // Prebuilt package: do not run Oryx build on the server.
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'false'
        }
        {
          // Normal prebuilt-zip deploy lands in wwwroot (no run-from-package mount).
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '0'
        }
        {
          // App Service Linux Node listens on 8080; next start binds to PORT.
          name: 'PORT'
          value: '8080'
        }
        {
          name: 'NODE_ENV'
          value: 'production'
        }
      ]
    }
  }
}

output appServicePlanName string = appServicePlan.name
output webAppName string = webApp.name
output webAppHostname string = webApp.properties.defaultHostName
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
