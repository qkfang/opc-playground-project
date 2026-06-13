targetScope = 'resourceGroup'

@description('Base name used to derive deterministic resource names.')
param baseName string = 'legoshop20260613'

@description('Region for the App Service plan and web app.')
param location string = resourceGroup().location

@description('SKU name for the App Service plan.')
param appServicePlanSku string = 'B1'

@description('Object (principal) ID of the deploy service principal that needs blob data access to publish the run-from-package zip. Defaults to the playground CI SP.')
param deployPrincipalId string = '0e872ca6-4149-4c58-9b10-64121fe089a5'

var uniqueSuffix = substring(toLower(uniqueString(resourceGroup().id, baseName)), 0, 6)
var appServicePlanName = '${baseName}-plan-${uniqueSuffix}'
var webAppName = '${baseName}-web-${uniqueSuffix}'
var packageStorageName = toLower('st${take(replace(baseName, '-', ''), 11)}${uniqueSuffix}')

// Dedicated storage account to host the prebuilt app package for
// WEBSITE_RUN_FROM_PACKAGE. Shared-key access stays enabled so the deploy
// workflow can mint a read-only SAS URL the app mounts read-only. This avoids
// the Kudu node-optimizer/rsync path that 502s on B1.
resource packageStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: packageStorageName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    allowSharedKeyAccess: true
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

resource packageBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: packageStorage
  name: 'default'
}

resource packageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: packageBlobService
  name: 'app-packages'
  properties: {
    publicAccess: 'None'
  }
}

// Storage Blob Data Contributor so the deploy SP can upload the package and
// mint a user-delegation SAS over Entra (shared-key is disabled by policy).
var blobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
resource packageStorageRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(packageStorage.id, deployPrincipalId, blobDataContributorRoleId)
  scope: packageStorage
  properties: {
    principalId: deployPrincipalId
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', blobDataContributorRoleId)
    principalType: 'ServicePrincipal'
  }
}

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
output packageStorageName string = packageStorage.name
