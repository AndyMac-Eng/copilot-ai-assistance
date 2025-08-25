@description('Name prefix for resources')
param namePrefix string = 'custsvc'
param location string = resourceGroup().location

// Cosmos DB account
resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2021-10-15' = {
  name: '${namePrefix}-cosmos'
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
      }
    ]
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
  }
}

// Cosmos DB SQL database
resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2021-10-15' = {
  parent: cosmos
  name: 'customerdb'
  properties: {
    resource: {
      id: 'customerdb'
    }
  }
}

// Cosmos DB container
resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2021-10-15' = {
  parent: cosmosDb
  name: 'customers'
  properties: {
    resource: {
      id: 'customers'
      partitionKey: {
        paths: ['/Email']
        kind: 'Hash'
      }
    }
  }
}

// Function App - Linux, containerized
resource acr 'Microsoft.ContainerRegistry/registries@2022-02-01' = {
  name: '${namePrefix}acr'
  location: location
  sku: {
    name: 'Basic'
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2022-03-01' = {
  name: '${namePrefix}-plan'
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
}

resource storage 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: toLower('${namePrefix}storage')
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}

resource functionApp 'Microsoft.Web/sites@2022-03-01' = {
  name: '${namePrefix}-func'
  location: location
  kind: 'functionapp'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      appSettings: [
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: '1'
        }
        {
          name: 'COSMOS_DATABASE'
          value: 'customerdb'
        }
        {
          name: 'COSMOS_CONTAINER'
          value: 'customers'
        }
      ]
    }
  }
  dependsOn: [storage]
}

// Key Vault and user-assigned managed identity (placeholders)
resource keyVault 'Microsoft.KeyVault/vaults@2022-07-01' = {
  name: '${namePrefix}-kv'
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    accessPolicies: []
  }
}

resource userAssignedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2018-11-30' = {
  name: '${namePrefix}-identity'
  location: location
}

// Note: Assign the userAssignedIdentity to function app and grant it access to Key Vault and Cosmos during deployment pipeline.

// API Management API mapping to function app
resource apimApi 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  parent: apim
  name: 'customers-api'
  properties: {
    displayName: 'Customer API'
    path: 'customers'
    protocols: [ 'https' ]
    serviceUrl: 'https://${functionApp.name}.azurewebsites.net/api'
  }
}

// APIM policy resource (attach JWT validation policy)
resource apimApiPolicy 'Microsoft.ApiManagement/service/apis/policies@2022-08-01' = {
  parent: apimApi
  name: 'policy'
  properties: {
    format: 'rawxml'
    // The policy XML should be provided during deployment via parameter or by uploading through the APIM REST API in CI/CD.
    value: '<policies><!-- replace with APIM policy XML during deployment --></policies>'
  }
}

// Key Vault & Managed Identity placeholders
// In production, create a Key Vault and store secrets (e.g., JwtKey) and assign a user-assigned managed identity to the function app.

// API Management
resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: '${namePrefix}-apim'
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: 'admin@contoso.com'
    publisherName: 'Contoso'
  }
}

output functionAppName string = functionApp.name
output apimName string = apim.name
