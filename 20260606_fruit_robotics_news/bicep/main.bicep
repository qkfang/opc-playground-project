param projectId string = '20260606_fruit_robotics_news'
param apiLocation string = resourceGroup().location
param staticWebAppLocation string = 'eastasia'
param appServicePlanSkuName string = 'B1'
param appServicePlanSkuTier string = 'Basic'
param linuxFxVersion string = 'DOTNET|8.0'
param newsFeedUrl string = 'https://news.google.com/rss/search?q=robotics&hl=en-US&gl=US&ceid=US:en'

var suffix = toLower(uniqueString(subscription().subscriptionId, resourceGroup().id, projectId))
var appServicePlanName = 'plan-fruit-news-${suffix}'
var apiWebAppName = 'api-fruit-news-${suffix}'
var staticWebAppName = 'swa-fruit-news-${suffix}'

resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: apiLocation
  kind: 'linux'
  sku: {
    name: appServicePlanSkuName
    tier: appServicePlanSkuTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource apiWebApp 'Microsoft.Web/sites@2023-12-01' = {
  name: apiWebAppName
  location: apiLocation
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'NEWS_FEED_URL'
          value: newsFeedUrl
        }
      ]
    }
  }
}

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: staticWebAppLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
}

output apiHostname string = apiWebApp.properties.defaultHostName
output apiUrl string = 'https://${apiWebApp.properties.defaultHostName}'
output staticWebAppName string = staticWebApp.name
