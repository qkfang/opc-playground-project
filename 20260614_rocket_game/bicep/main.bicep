// Infra for 20260614_rocket_game (Star Ascent)
// The game is a 100% client-side static site (plain HTML/CSS/JS ES modules,
// no backend/API/DB and NO build step), so it deploys to a single
// Azure Static Web App (Free tier) in rg-playground-01.
targetScope = 'resourceGroup'

@description('Base name used to derive resource names.')
param baseName string = 'rocketgame20260614'

@description('Region for the Static Web App (limited region availability).')
param swaLocation string = 'eastasia'

@description('SKU for the Static Web App.')
@allowed([
  'Free'
  'Standard'
])
param sku string = 'Free'

var uniqueSuffix = take(uniqueString(resourceGroup().id, baseName), 6)
var swaName = '${baseName}-swa-${uniqueSuffix}'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: swaName
  location: swaLocation
  sku: {
    name: sku
    tier: sku
  }
  properties: {
    // Token-based deploy from GitHub Actions (no repo integration wired here).
    allowConfigFileUpdates: true
  }
}

output staticWebAppName string = staticWebApp.name
output defaultHostname string = staticWebApp.properties.defaultHostname
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
