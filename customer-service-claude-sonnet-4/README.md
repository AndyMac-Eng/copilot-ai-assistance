# Customer Microservice

A comprehensive customer management microservice built with .NET 8 Azure Functions and deployed to Azure using Infrastructure as Code (IaC) with Bicep templates.

## 🚀 Features

- **Customer Account Management**: Create, read, update, and delete customer accounts
- **Authentication & Authorization**: JWT-based authentication with secure token handling
- **Login/Logout Functionality**: Complete authentication flow with session management
- **Account Information Retrieval**: Secure access to customer profile data
- **Azure Integration**: Fully integrated with Azure services for scalability and security

## 🏗️ Architecture

### Technology Stack
- **.NET 8**: Latest version with isolated worker model
- **Azure Functions**: Serverless compute platform
- **Azure Cosmos DB**: NoSQL database with SQL API
- **Azure API Management**: API gateway with security policies
- **Azure Key Vault**: Secure secrets management
- **Azure Application Insights**: Monitoring and telemetry
- **Azure Virtual Network**: Network isolation and security

### Security Features
- 🔐 **Managed Identity**: Azure AD managed identity for service-to-service authentication
- 🛡️ **Virtual Network Integration**: Network isolation for enhanced security
- 🔑 **JWT Authentication**: Secure token-based authentication
- 📊 **Rate Limiting**: API throttling to prevent abuse
- 🔒 **Key Vault Integration**: Secure storage of connection strings and secrets

## 📁 Project Structure

```
customer-service/
├── src/
│   ├── CustomerService.csproj      # Project file with dependencies
│   ├── Program.cs                  # Application startup and DI configuration
│   ├── host.json                   # Azure Functions host configuration
│   ├── local.settings.json         # Local development settings
│   ├── Configuration/
│   │   └── AppConfiguration.cs     # Application configuration models
│   ├── Functions/
│   │   └── CustomerFunctions.cs    # HTTP triggered Azure Functions
│   ├── Models/
│   │   ├── Customer.cs             # Customer entity model
│   │   └── DTOs/
│   │       └── CustomerDtos.cs     # Data Transfer Objects
│   ├── Repositories/
│   │   ├── CosmosDbCustomerRepository.cs  # Cosmos DB data access
│   │   └── Interfaces/
│   │       └── ICustomerRepository.cs     # Repository interface
│   └── Services/
│       ├── AuthenticationService.cs       # JWT authentication service
│       ├── CustomerService.cs             # Business logic service
│       └── Interfaces/
│           ├── IAuthenticationService.cs  # Authentication interface
│           └── ICustomerService.cs        # Customer service interface
├── infra/
│   ├── main.bicep                  # Main infrastructure template
│   ├── apim.bicep                  # API Management configuration
│   ├── main.parameters.json        # Infrastructure parameters
│   └── abbreviations.json          # Azure resource naming abbreviations
├── .github/workflows/
│   └── azure-deploy.yml            # CI/CD pipeline
├── azure.yaml                      # Azure Developer CLI configuration
├── openapi.json                    # OpenAPI specification
└── README.md                       # This file
```

## 🔧 API Endpoints

### Customer Management
- `POST /api/customers/register` - Register a new customer account
- `POST /api/customers/login` - Authenticate customer and get JWT token
- `POST /api/customers/logout` - Logout customer (token invalidation)
- `GET /api/customers/profile` - Get authenticated customer's profile
- `PUT /api/customers/profile` - Update customer profile
- `DELETE /api/customers/profile` - Delete customer account
- `GET /api/health` - Health check endpoint

### Authentication
All protected endpoints require a valid JWT token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

## 🏃‍♂️ Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [Azure Developer CLI (azd)](https://docs.microsoft.com/en-us/azure/developer/azure-developer-cli/install-azd)
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- Azure subscription with appropriate permissions

### Local Development

1. **Clone the repository**
   ```bash
   git clone <repository-url>
   cd customer-service
   ```

2. **Configure local settings**
   ```bash
   cd src
   cp local.settings.json.template local.settings.json
   # Update the connection strings and settings in local.settings.json
   ```

3. **Install dependencies**
   ```bash
   dotnet restore
   ```

4. **Run locally**
   ```bash
   func start
   ```

The API will be available at `http://localhost:7071`

### Azure Deployment

1. **Login to Azure**
   ```bash
   azd auth login
   ```

2. **Initialize the environment**
   ```bash
   azd init
   ```

3. **Deploy to Azure**
   ```bash
   azd up
   ```

This will:
- Provision all Azure resources using Bicep templates
- Build and deploy the Function App
- Configure API Management
- Set up monitoring and security

## 🔧 Configuration

### Environment Variables

| Variable | Description | Required |
|----------|-------------|----------|
| `CosmosDb__ConnectionString` | Cosmos DB connection string | Yes |
| `Jwt__SecretKey` | JWT signing key | Yes |
| `Jwt__Issuer` | JWT token issuer | Yes |
| `Jwt__Audience` | JWT token audience | Yes |
| `Jwt__ExpiryMinutes` | Token expiration time in minutes | No (default: 60) |

### Azure Resources

The deployment creates the following Azure resources:
- **Resource Group**: Container for all resources
- **Azure Functions**: Serverless compute for the API
- **Cosmos DB Account**: NoSQL database
- **API Management**: API gateway and management
- **Key Vault**: Secure storage for secrets
- **Application Insights**: Application monitoring
- **Log Analytics Workspace**: Centralized logging
- **Virtual Network**: Network isolation
- **Managed Identity**: Service authentication

## 🧪 Testing

### Unit Tests
```bash
dotnet test
```

### API Testing
Use the provided OpenAPI specification (`openapi.json`) with tools like:
- Postman
- Swagger UI
- curl

Example customer registration:
```bash
curl -X POST "https://your-api-url/api/customers/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "user@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "password": "SecurePassword123!"
  }'
```

## 📊 Monitoring

### Application Insights
- Real-time application monitoring
- Performance metrics
- Error tracking
- Custom telemetry

### Health Checks
- Built-in health check endpoint at `/api/health`
- Monitors database connectivity
- Returns detailed health status

### Logging
- Structured logging with Serilog
- Centralized logs in Azure Log Analytics
- Correlation IDs for request tracking

## 🔒 Security

### Authentication & Authorization
- JWT-based authentication
- Token validation at API Management level
- Managed Identity for Azure service authentication

### Data Protection
- Data encryption at rest (Cosmos DB)
- Data encryption in transit (HTTPS)
- Secure connection strings in Key Vault

### Network Security
- Virtual Network integration
- Private endpoints for Azure services
- API Management in internal VNet mode

## 🚀 CI/CD Pipeline

The project includes a complete GitHub Actions workflow that:

1. **Validation**: Validates Bicep templates and builds .NET code
2. **Security Scanning**: Runs Trivy vulnerability scanner
3. **Deployment**: Automated deployment to development and production environments
4. **Environment Management**: Separate environments for dev and prod

### Required Secrets
Configure these secrets in your GitHub repository:
- `AZURE_CLIENT_ID`: Service principal client ID
- `AZURE_TENANT_ID`: Azure tenant ID
- `AZURE_SUBSCRIPTION_ID`: Azure subscription ID

## 📖 API Documentation

The complete API documentation is available in the OpenAPI specification (`openapi.json`). After deployment, you can access the interactive documentation through API Management developer portal.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Ensure all tests pass
6. Submit a pull request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Support

For issues and questions:
1. Check the [GitHub Issues](../../issues)
2. Review the Azure documentation
3. Check Application Insights for runtime issues

## 🔄 Version History

- **v1.0.0**: Initial release with complete customer management functionality
  - Customer registration and authentication
  - Profile management
  - Azure infrastructure with security best practices
  - CI/CD pipeline setup
