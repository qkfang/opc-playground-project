@description('Azure location')
param location string

@description('Key Vault name')
param keyVaultName string

@description('Tenant ID for the vault')
param tenantId string = subscription().tenantId

@description('Restore a soft-deleted Key Vault with the same name when present.')
param restoreSoftDeletedKeyVault bool = false

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: tenantId
    createMode: restoreSoftDeletedKeyVault ? 'recover' : 'default'
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    publicNetworkAccess: 'Enabled'
  }
}

output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultId string = keyVault.id
