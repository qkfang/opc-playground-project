// Static Web App for the LEGO marketplace frontend (hybrid Next.js)
@description('Azure region for the Static Web App. SWA is only available in a subset of regions.')
param location string = 'eastasia'

@description('Name of the Static Web App resource.')
param staticWebAppName string

@description('SKU for the Static Web App.')
@allowed([
  'Free'
  'Standard'
])
param sku string = 'Free'

resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: location
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
