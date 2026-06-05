@description('Azure location for the App Service plan and Web App.')
param location string = resourceGroup().location

@description('Azure region for the Static Web App.')
@allowed([
  'Central US'
  'East US 2'
  'West Europe'
])
param staticWebAppLocation string = 'East US 2'

@description('Name of the App Service plan.')
param appServicePlanName string = 'plan-robotics-news-01'

@description('Name of the Azure Web App that hosts the API.')
param webAppName string = 'app-robotics-news-api-01'

@description('Name of the Azure Static Web App that hosts the frontend.')
param staticWebAppName string = 'swa-robotics-news-01'

@description('SKU for the App Service plan.')
@allowed([
  'B1'
  'S1'
])
param appServicePlanSku string = 'B1'

@description('Allowed frontend origins for the API CORS policy.')
param corsAllowedOrigins array = []

@description('Robotics RSS feeds consumed by the API.')
param rssFeedUrls array = [
  'https://www.therobotreport.com/feed/'
  'https://roboticsandautomationnews.com/feed/'
]

var appServicePlanTier = appServicePlanSku == 'S1' ? 'Standard' : 'Basic'
var corsAppSettings = [for (origin, index) in corsAllowedOrigins: {
  name: 'Cors__AllowedOrigins__${index}'
  value: string(origin)
}]
var rssFeedAppSettings = [for (feedUrl, index) in rssFeedUrls: {
  name: 'NewsFeeds__FeedUrls__${index}'
  value: string(feedUrl)
}]

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  sku: {
    name: appServicePlanSku
    tier: appServicePlanTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      appSettings: concat(corsAppSettings, rssFeedAppSettings, [
        {
          name: 'NewsFeeds__CacheDurationMinutes'
          value: '5'
        }
      ])
    }
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    allowConfigFileUpdates: true
    stagingEnvironmentPolicy: 'Enabled'
  }
}

output apiUrl string = 'https://${webApp.properties.defaultHostName}'
output staticWebAppDefaultHostName string = staticWebApp.properties.defaultHostname
