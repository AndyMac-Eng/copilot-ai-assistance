param environmentName string
param location string = resourceGroup().location

var resourceToken = uniqueString(subscription().id, resourceGroup().id, location, environmentName)

resource userIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: 'az-id-${resourceToken}'
  location: location
}

resource cosmosDbAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: 'az-cdb-${resourceToken}'
  location: location
  kind: 'GlobalDocumentDB'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userIdentity.id}': {}
    }
  }
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    enableFreeTier: true
  }
}


resource customerDb 'Microsoft.DocumentDB/sqlDatabases@2023-04-15' = {
  name: 'CustomerDb'
  scope: cosmosDbAccount
  properties: {
    resource: {
      id: 'CustomerDb'
    }
    options: {}
  }
}

resource customersContainer 'Microsoft.DocumentDB/sqlContainers@2023-04-15' = {
  name: 'Customers'
  scope: customerDb
  properties: {
    resource: {
      id: 'Customers'
      partitionKey: {
        paths: ['/id']
        kind: 'Hash'
      }
    }
    options: {}
  }
}

resource apiManagement 'Microsoft.ApiManagement/service@2024-05-01' = {
  name: 'az-apim-${resourceToken}'
  location: location
  sku: {
    name: 'Developer'
    capacity: 1
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${userIdentity.id}': {}
    }
  }
  properties: {
    publisherEmail: 'admin@yourdomain.com'
    publisherName: 'Customer API Publisher'
    hostnameConfigurations: [] // Add custom domain config here
  }
}

output cosmosDbConnectionString string = listConnectionStrings(cosmosDbAccount.id, cosmosDbAccount.apiVersion).connectionStrings[0].connectionString
output RESOURCE_GROUP_ID string = resourceGroup().id
