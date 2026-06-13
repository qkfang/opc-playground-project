targetScope = 'resourceGroup'

@description('Base name used to derive deterministic resource names.')
param baseName string = 'survival20260613'

@description('Region for the Static Web App.')
param swaLocation string = 'eastasia'

var uniqueSuffix = substring(toLower(uniqueString(resourceGroup().id, baseName)), 0, 6)
var staticWebAppName = '${baseName}-swa-${uniqueSuffix}'

// Last Stand is a 100% client-side Next.js static export (no API/backend),
// so a Free Static Web App is the cleanest, cheapest host. No App Service /
// Kudu / storage needed.
resource staticWebApp 'Microsoft.Web/staticSites@2023-12-01' = {
  name: staticWebAppName
  location: swaLocation
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // Deployment is driven by the GitHub Actions deploy workflow using the
    // SWA deployment token (action: upload), not by a repo-linked build.
    allowConfigFileUpdates: true
  }
}

output staticWebAppName string = staticWebApp.name
output staticWebAppHostname string = staticWebApp.properties.defaultHostname
output staticWebAppUrl string = 'https://${staticWebApp.properties.defaultHostname}'
