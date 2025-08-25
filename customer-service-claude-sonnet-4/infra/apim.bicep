@description('API Management service name')
param apimServiceName string

@description('Function App name')
param functionAppName string

@description('Environment name')
param environmentName string

// Get references to existing resources
resource apiManagement 'Microsoft.ApiManagement/service@2024-05-01' existing = {
  name: apimServiceName
}

resource functionApp 'Microsoft.Web/sites@2024-04-01' existing = {
  name: functionAppName
}

// Customer Service API
resource customerServiceApi 'Microsoft.ApiManagement/service/apis@2024-05-01' = {
  parent: apiManagement
  name: 'customer-service-api'
  properties: {
    displayName: 'Customer Service API'
    description: 'Customer account management and authentication API'
    path: 'customer'
    protocols: ['https']
    serviceUrl: 'https://${functionApp.properties.defaultHostName}/api'
    subscriptionRequired: true
    apiVersion: 'v1'
    apiVersionSetId: customerServiceApiVersionSet.id
    format: 'openapi+json'
    value: loadTextContent('./openapi.json')
  }
}

// API Version Set
resource customerServiceApiVersionSet 'Microsoft.ApiManagement/service/apiVersionSets@2024-05-01' = {
  parent: apiManagement
  name: 'customer-service-versions'
  properties: {
    displayName: 'Customer Service API Versions'
    description: 'Version set for Customer Service API'
    versioningScheme: 'Segment'
  }
}

// JWT Validation Policy
resource jwtValidationPolicy 'Microsoft.ApiManagement/service/policies@2024-05-01' = {
  parent: apiManagement
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <cors allow-credentials="false">
            <allowed-origins>
              <origin>*</origin>
            </allowed-origins>
            <allowed-methods>
              <method>GET</method>
              <method>POST</method>
              <method>PUT</method>
              <method>DELETE</method>
              <method>OPTIONS</method>
            </allowed-methods>
            <allowed-headers>
              <header>*</header>
            </allowed-headers>
          </cors>
          <set-header name="X-Forwarded-For" exists-action="override">
            <value>@(context.Request.IpAddress)</value>
          </set-header>
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
          <set-header name="X-Powered-By" exists-action="delete" />
          <set-header name="Server" exists-action="delete" />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

// API Operations with detailed policies
resource registerOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'register'
  properties: {
    displayName: 'Register Customer'
    method: 'POST'
    urlTemplate: '/customers/register'
    description: 'Register a new customer account'
    request: {
      description: 'Customer registration details'
      queryParameters: []
      headers: []
      representations: [
        {
          contentType: 'application/json'
          schemaId: 'CustomerRegistrationDto'
        }
      ]
    }
    responses: [
      {
        statusCode: 201
        description: 'Customer registered successfully'
        representations: [
          {
            contentType: 'application/json'
          }
        ]
      }
      {
        statusCode: 400
        description: 'Bad request'
      }
    ]
  }
}

resource loginOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'login'
  properties: {
    displayName: 'Login Customer'
    method: 'POST'
    urlTemplate: '/customers/login'
    description: 'Authenticate customer and return access token'
    request: {
      description: 'Customer login credentials'
      representations: [
        {
          contentType: 'application/json'
          schemaId: 'CustomerLoginDto'
        }
      ]
    }
    responses: [
      {
        statusCode: 200
        description: 'Login successful'
      }
      {
        statusCode: 401
        description: 'Invalid credentials'
      }
    ]
  }
}

resource logoutOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'logout'
  properties: {
    displayName: 'Logout Customer'
    method: 'POST'
    urlTemplate: '/customers/logout'
    description: 'Logout customer'
  }
}

resource getProfileOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'get-profile'
  properties: {
    displayName: 'Get Customer Profile'
    method: 'GET'
    urlTemplate: '/customers/profile'
    description: 'Get customer profile information'
  }
}

resource updateProfileOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'update-profile'
  properties: {
    displayName: 'Update Customer Profile'
    method: 'PUT'
    urlTemplate: '/customers/profile'
    description: 'Update customer profile information'
  }
}

resource deactivateAccountOperation 'Microsoft.ApiManagement/service/apis/operations@2024-05-01' = {
  parent: customerServiceApi
  name: 'deactivate-account'
  properties: {
    displayName: 'Deactivate Customer Account'
    method: 'DELETE'
    urlTemplate: '/customers/account'
    description: 'Deactivate customer account'
  }
}

// JWT Authentication policy for protected operations
resource authPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: logoutOperation
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Access token is invalid">
            <openid-config url="https://${apiManagement.properties.gatewayUrl}/.well-known/openid-configuration" />
            <required-claims>
              <claim name="customerId" match="all">
                <value>@(context.Request.Headers.GetValueOrDefault("Authorization","").Replace("Bearer ",""))</value>
              </claim>
            </required-claims>
          </validate-jwt>
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

resource getProfileAuthPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: getProfileOperation
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Access token is invalid">
            <openid-config url="https://${apiManagement.properties.gatewayUrl}/.well-known/openid-configuration" />
            <required-claims>
              <claim name="customerId" match="all">
                <value>@(context.Request.Headers.GetValueOrDefault("Authorization","").Replace("Bearer ",""))</value>
              </claim>
            </required-claims>
          </validate-jwt>
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

resource updateProfileAuthPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: updateProfileOperation
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Access token is invalid">
            <openid-config url="https://${apiManagement.properties.gatewayUrl}/.well-known/openid-configuration" />
            <required-claims>
              <claim name="customerId" match="all">
                <value>@(context.Request.Headers.GetValueOrDefault("Authorization","").Replace("Bearer ",""))</value>
              </claim>
            </required-claims>
          </validate-jwt>
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

resource deactivateAccountAuthPolicy 'Microsoft.ApiManagement/service/apis/operations/policies@2024-05-01' = {
  parent: deactivateAccountOperation
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <validate-jwt header-name="Authorization" failed-validation-httpcode="401" failed-validation-error-message="Access token is invalid">
            <openid-config url="https://${apiManagement.properties.gatewayUrl}/.well-known/openid-configuration" />
            <required-claims>
              <claim name="customerId" match="all">
                <value>@(context.Request.Headers.GetValueOrDefault("Authorization","").Replace("Bearer ",""))</value>
              </claim>
            </required-claims>
          </validate-jwt>
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

// Product for the API
resource customerServiceProduct 'Microsoft.ApiManagement/service/products@2024-05-01' = {
  parent: apiManagement
  name: 'customer-service-product'
  properties: {
    displayName: 'Customer Service'
    description: 'Customer account management and authentication services'
    subscriptionRequired: true
    approvalRequired: false
    state: 'published'
  }
}

// Associate API with Product
resource productApi 'Microsoft.ApiManagement/service/products/apis@2024-05-01' = {
  parent: customerServiceProduct
  name: customerServiceApi.name
}

// Rate limiting policy for the product
resource productPolicy 'Microsoft.ApiManagement/service/products/policies@2024-05-01' = {
  parent: customerServiceProduct
  name: 'policy'
  properties: {
    value: '''
      <policies>
        <inbound>
          <base />
          <rate-limit calls="100" renewal-period="60" />
          <quota calls="10000" renewal-period="3600" />
        </inbound>
        <backend>
          <base />
        </backend>
        <outbound>
          <base />
        </outbound>
        <on-error>
          <base />
        </on-error>
      </policies>
    '''
  }
}

output customerServiceApiId string = customerServiceApi.id
output customerServiceProductId string = customerServiceProduct.id
