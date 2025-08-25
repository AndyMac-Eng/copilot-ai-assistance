// Bicep file for Customer Microservice infrastructure
// Includes Cosmos DB, Key Vault, API Management, and networking

param location string = resourceGroup().location
param customerDbName string = 'customerdb'
param keyVaultName string = 'customerkeyvault'
param apiManagementName string = 'customerapim'
param vnetName string = 'customer-vnet'
param subnetName string = 'customer-subnet'

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts@2023-03-15' = {
  name: customerDbName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    locations: [
      {
        locationName: location
        failoverPriority: 0
      }
    ]
    enableAutomaticFailover: true
    enableMultipleWriteLocations: true
    publicNetworkAccess: 'Disabled'
  }
  tags: {
    'azd-env-name': 'customer-service'
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    accessPolicies: []
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enableSoftDelete: true
    enablePurgeProtection: true
    publicNetworkAccess: 'Disabled'
  }
  tags: {
    'azd-env-name': 'customer-service'
  }
}

resource apim 'Microsoft.ApiManagement/service@2023-05-01-preview' = {
  name: apiManagementName
  location: location
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  properties: {
    publisherEmail: 'admin@yourdomain.com'
    publisherName: 'CustomerService'
    virtualNetworkType: 'External'
    hostnameConfigurations: [
      {
        type: 'Proxy'
        hostName: 'api.customer.yourdomain.com'
        certificateSource: 'KeyVault'
        keyVaultId: '/subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.KeyVault/vaults/${keyVaultName}'
        // For CI/CD, parameterize the Key Vault certificate resource ID
      }
    ]
  }
  tags: {
    'azd-env-name': 'customer-service'
  }
}

resource vnet 'Microsoft.Network/virtualNetworks@2023-02-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [ '10.0.0.0/16' ]
    }
    subnets: [
      {
        name: subnetName
        properties: {
          addressPrefix: '10.0.1.0/24'
        }
      }
    ]
  }
  tags: {
    'azd-env-name': 'customer-service'
  }
}

output cosmosDbEndpoint string = cosmosDb.properties.documentEndpoint
output keyVaultUri string = keyVault.properties.vaultUri
output apimHostname string = apim.properties.hostnameConfigurations[0].hostName
