// Infra for proj36_robot_website_v2 (Cogsworth Robotics 2.0 website)
// Static front-end (HTML/CSS/vanilla JS, no build step) PLUS a small managed
// Azure Functions API (Node v4) for the feedback form, persisting to an
// in-memory store. Both ship to a single Azure Static Web App (Free tier) in
// rg-playground-01 — SWA serves the static assets and the /api/* Functions.
// (No external DB: feedback lives in the Functions host process memory.)
targetScope = 'resourceGroup'

@description('Base name used to derive resource names (must be project_id).')
param baseName string = 'proj36'

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
