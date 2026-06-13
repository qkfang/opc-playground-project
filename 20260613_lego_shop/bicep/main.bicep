targetScope = 'resourceGroup'

@description('Base name used to derive deterministic resource names.')
param baseName string = 'legoshop20260613'

@description('Region for the App Service plan and web app.')
param location string = resourceGroup().location

@description('SKU name for the App Service plan.')
param appServicePlanSku string = 'B1'

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
      // Next.js standalone/standard server. `next start` honours process.env.PORT.
      appCommandLine: 'npm run start'
      appSettings: [
        {
          // Let Oryx run `npm install` + `npm run build` on deploy.
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
        {
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
        {
          // Ensure devDependencies (tailwind/postcss/typescript) are installed for the build.
          name: 'NPM_CONFIG_PRODUCTION'
          value: 'false'
        }
      ]
    }
  }
}

output appServicePlanName string = appServicePlan.name
output webAppName string = webApp.name
output webAppHostname string = webApp.properties.defaultHostName
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
