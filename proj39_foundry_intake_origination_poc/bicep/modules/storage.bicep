@description('Azure location')
param location string

@description('Storage account name')
param storageAccountName string

@description('Blob container name for origination cases / reports')
param containerName string = 'origination'

// Key-based auth disabled; the web app connects with its managed identity (RBAC assigned in main.bicep).
resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
  }
}

resource container 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  name: '${storage.name}/default/${containerName}'
  properties: {
    publicAccess: 'None'
  }
}

output storageAccountName string = storage.name
output storageAccountId string = storage.id
output containerName string = containerName
output blobEndpoint string = storage.properties.primaryEndpoints.blob
