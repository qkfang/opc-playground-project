@description('Azure location')
param location string

@description('Storage account name')
param storageAccountName string

@description('Blob container name for the intake journal / artefacts')
param containerName string = 'intake'

// Key-based auth disabled (repo policy: storage must use managed identity + RBAC).
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
