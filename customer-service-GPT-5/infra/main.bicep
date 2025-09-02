targetScope = 'resourceGroup'

@description('Environment name (dev, test, prod) used for resource names and tags')
param environment string
@description('Base name / system name')
param systemName string = 'custsvc'
@description('Location for all resources')
param location string = resourceGroup().location
@description('API Management publisher email')
param apimPublisherEmail string
@description('API Management publisher name')
param apimPublisherName string
@description('Custom domain name for the customer API (apex or subdomain)')
param apiCustomDomain string
@description('Certificate resource Id or Key Vault secret Id for custom domain TLS cert')
param apiCustomDomainCertId string
@description('SKU for customer-facing APIM (e.g. Consumption, Developer, Premium)')
@allowed([
  'Consumption'
  'Developer'
  'Premium'
])
param customerApimSkuName string = (environment == 'prod' ? 'Developer' : 'Consumption')
@description('Admin (internal) APIM SKU (Developer for non-prod, Premium for prod)')
@allowed([
  'Developer'
  'Premium'
])
param adminApimSkuName string = (environment == 'prod' ? 'Premium' : 'Developer')
@description('Capacity for admin APIM (1 for Developer; Premium supports >1)')
param adminApimSkuCapacity int = 1
@description('VNet address space for internal resources (Option A)')
param vnetAddressSpace string = '10.0.0.0/16'
@description('Subnet prefix for Admin APIM')
param adminApimSubnetPrefix string = '10.0.0.0/24'
@description('Cosmos DB account consistency level')
@allowed([
  'Eventual'
  'ConsistentPrefix'
  'Session'
  'BoundedStaleness'
  'Strong'
])
param cosmosConsistency string = 'Session'
@description('Cosmos DB container throughput (autoscale max RU/s)')
param cosmosMaxThroughput int = 1000
@description('JWT issuer to embed in tokens')
param jwtIssuer string = 'customer-service'
@description('JWT audience to embed in tokens')
param jwtAudience string = 'customer-clients'
@description('JWT signing key (development only - override with Key Vault secret in prod)')
@secure()
param jwtSigningKey string

var namePrefix = '${systemName}-${environment}'
var cosmosAccountName = toLower(replace('${namePrefix}-cosmos','_',''))
var keyVaultName = take(toLower(replace('${namePrefix}-kv','_','')), 24)
var apimName = toLower('${namePrefix}-apim')
var adminApimName = toLower('${namePrefix}-apim-admin')
var appPlanName = '${namePrefix}-plan'
var functionAppName = toLower('${namePrefix}-func')
var containerRegistryName = toLower(replace('${namePrefix}acr','-',''))
var vnetName = '${namePrefix}-vnet'
var adminApimSubnetName = 'apim-admin-snet'
var adminApimSubnetId = resourceId('Microsoft.Network/virtualNetworks/subnets', vnetName, adminApimSubnetName)

// Tags
var commonTags = {
  system: systemName
  environment: environment
  owner: apimPublisherName
  'azd-env-name': environment
}

resource acr 'Microsoft.ContainerRegistry/registries@2023-06-01-preview' = {
  name: containerRegistryName
  location: location
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    policies: {
      quarantinePolicy: {
        status: 'disabled'
      }
      retentionPolicy: {
        days: 7
        status: 'enabled'
      }
    }
  }
  tags: commonTags
}

resource cosmos 'Microsoft.DocumentDB/databaseAccounts@2023-11-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: cosmosConsistency
    }
    capabilities: [
      { name: 'EnableServerless' }
    ]
    publicNetworkAccess: 'Enabled'
    disableLocalAuth: true
    enableFreeTier: false
    enableAnalyticalStorage: false
  }
  tags: commonTags
}

resource cosmosDb 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-11-15' = {
  name: 'customersdb'
  parent: cosmos
  properties: {
    resource: {
      id: 'customersdb'
    }
    options: {}
  }
}

resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-11-15' = {
  name: 'customers'
  parent: cosmosDb
  properties: {
    resource: {
      id: 'customers'
      partitionKey: {
        paths: [ '/tenantId' ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/"_etag"/?'
          }
        ]
      }
      uniqueKeyPolicy: {
        uniqueKeys: [
          {
            paths: [ '/tenantId', '/normalizedEmail' ]
          }
        ]
      }
      conflictResolutionPolicy: {
        mode: 'LastWriterWins'
        conflictResolutionPath: '/_ts'
      }
    }
    options: {
      autoscaleSettings: {
        maxThroughput: cosmosMaxThroughput
      }
    }
  }
}

resource kv 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: {
      name: 'standard'
      family: 'A'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
  enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
  tags: commonTags
}

// Placeholder secret for JWT signing key (dev). In prod use HSM or rotate via pipeline.
resource jwtSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'jwt-signing-key'
  parent: kv
  properties: {
    value: jwtSigningKey
  }
}

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appPlanName
  location: location
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  kind: 'functionapp'
  properties: {}
  tags: commonTags
}

resource func 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux,container'
  properties: {
    siteConfig: {
      linuxFxVersion: 'DOCKER|${acr.properties.loginServer}/${systemName}/customer-service:latest'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsFeatureFlags'
          value: 'EnableWorkerIndexing'
        }
        {
          name: 'WEBSITE_RUN_FROM_PACKAGE'
          value: ''
        }
        {
          name: 'COSMOS_DATABASE'
          value: 'customersdb'
        }
        {
          name: 'COSMOS_CONTAINER'
          value: 'customers'
        }
        {
          name: 'JWT_ISSUER'
          value: jwtIssuer
        }
        {
          name: 'JWT_AUDIENCE'
          value: jwtAudience
        }
        {
          name: 'JWT_SIGNING_KEY__KV_SECRET_NAME'
          value: 'jwt-signing-key'
        }
      ]
    }
    httpsOnly: true
  }
  identity: {
    type: 'SystemAssigned'
  }
  tags: commonTags
}

// API Management
resource apim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: apimName
  location: location
  sku: {
    name: customerApimSkuName
    capacity: (customerApimSkuName == 'Consumption' ? 0 : 1)
  }
  properties: {
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    publicIpAddressId: ''
  }
  tags: commonTags
}

// Virtual Network for internal Admin APIM (Option A)
resource vnet 'Microsoft.Network/virtualNetworks@2023-11-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [ vnetAddressSpace ]
    }
    subnets: [
      {
        name: adminApimSubnetName
        properties: {
          addressPrefix: adminApimSubnetPrefix
        }
      }
    ]
  }
  tags: commonTags
}

// Internal Admin APIM instance (ingress restricted to private network/VPN)
resource adminApim 'Microsoft.ApiManagement/service@2022-08-01' = {
  name: adminApimName
  location: location
  sku: {
    name: adminApimSkuName
    capacity: adminApimSkuCapacity
  }
  properties: {
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    virtualNetworkType: 'Internal'
    virtualNetworkConfiguration: {
      subnetResourceId: adminApimSubnetId
    }
  }
  tags: commonTags
  dependsOn: [ vnet ]
}

// Customer API
resource apimApi 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  name: 'customer-api'
  parent: apim
  properties: {
    path: 'customer'
    displayName: 'Customer Service API'
    protocols: [ 'https' ]
    serviceUrl: 'https://${func.name}.azurewebsites.net/api'
  }
}

// Admin API (internal) - shares same Function backend; NOTE: function remains public; consider separate app or private endpoint for stricter isolation.
resource adminApimApi 'Microsoft.ApiManagement/service/apis@2022-08-01' = {
  name: 'admin-api'
  parent: adminApim
  properties: {
    path: 'admin'
    displayName: 'Customer Admin API'
    protocols: [ 'https' ]
    serviceUrl: 'https://${func.name}.azurewebsites.net/api'
  }
}

resource adminOpGet 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'adminGetCustomer'
  parent: adminApimApi
  properties: {
    displayName: 'Get Customer (Admin)'
    method: 'GET'
    urlTemplate: '/admin/customers/{id}'
    templateParameters: [
      {
        name: 'id'
        required: true
        type: 'string'
      }
    ]
    responses: []
  }
}

resource adminOpUpdate 'Microsoft.ApiManagement/service/apis/operations@2022-08-01' = {
  name: 'adminUpdateCustomer'
  parent: adminApimApi
  properties: {
    displayName: 'Update Customer (Admin)'
    method: 'PATCH'
    urlTemplate: '/admin/customers/{id}'
    templateParameters: [
      {
        name: 'id'
        required: true
        type: 'string'
      }
    ]
    responses: []
  }
}

// Operations (create account, login, me)
resource opCreate 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  name: 'createAccount'
  parent: apimApi
  properties: {
    displayName: 'Create Account'
    method: 'POST'
    urlTemplate: '/customers'
    responses: []
    request: {
      queryParameters: []
    }
  }
}

resource opLogin 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  name: 'login'
  parent: apimApi
  properties: {
    displayName: 'Login'
    method: 'POST'
    urlTemplate: '/customers/login'
    responses: []
  }
}

resource opMe 'Microsoft.ApiManagement/service/apis/operations@2023-05-01-preview' = {
  name: 'me'
  parent: apimApi
  properties: {
    displayName: 'Get Me'
    method: 'GET'
    urlTemplate: '/customers/me'
    responses: []
  }
}

// Custom domain mapping for APIM (requires existing certificate in Key Vault or resource) - simplified
resource apimHostname 'Microsoft.ApiManagement/service/hostnameConfigurations@2022-08-01' = {
  name: 'proxy'
  parent: apim
  properties: {
    type: 'Proxy'
    hostName: apiCustomDomain
    keyVaultId: apiCustomDomainCertId
    negotiateClientCertificate: false
  }
}

// Outputs
output functionAppName string = func.name
output apimName string = apim.name
output adminApimName string = adminApim.name
output customerApiId string = apimApi.name
output adminApiId string = adminApimApi.name
output keyVaultName string = kv.name
output containerRegistryName string = acr.name
output cosmosAccountName string = cosmos.name
