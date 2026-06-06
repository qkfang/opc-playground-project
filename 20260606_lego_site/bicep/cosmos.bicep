// Cosmos DB (NoSQL) for sets + listings
@description('Azure region.')
param location string = resourceGroup().location

@description('Cosmos DB account name (3-44 chars, lowercase letters, numbers, hyphens).')
param accountName string

@description('Database name.')
param databaseName string = 'legodb'

var setsContainerName = 'sets'
var listingsContainerName = 'listings'

resource account 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: accountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: false
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
  }
}

resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: account
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource setsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: setsContainerName
  properties: {
    resource: {
      id: setsContainerName
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
  }
}

resource listingsContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: database
  name: listingsContainerName
  properties: {
    resource: {
      id: listingsContainerName
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
    }
  }
}

output accountName string = account.name
output databaseName string = database.name
output endpoint string = account.properties.documentEndpoint
