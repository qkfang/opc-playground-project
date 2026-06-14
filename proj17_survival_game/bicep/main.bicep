// Infra for 20260613_survival_game (Last Stand)
// The game is a 100% client-side Next.js static export (no backend/API/DB),
// so it deploys to a single Azure Static Web App (Free tier).
targetScope = 'resourceGroup'

@description('Base name used to derive resource names.')
param baseName string = 'proj17'

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
