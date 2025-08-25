# Customer Microservice

This microservice provides customer account management: registration, login/logout, and profile retrieval.

Tech stack:
- .NET 8 isolated Azure Functions
- Azure Cosmos DB (SQL) for storage
- Azure API Management for domain + authentication

Security notes:
- Use Azure AD / Azure AD B2C for user authentication and protect the API via APIM validate-jwt policy in production.
- Use Managed Identity for the function app to access Cosmos DB where possible.
- Store secrets in Azure Key Vault and reference them by managed identity in the function app.

Infra:
- Bicep files under `infra/` create Cosmos DB, Function App, and APIM resources (declarative only).

Usage:
- Configure `appsettings.json` or environment variables for Cosmos DB and JWT signing key for local development.
- Build and publish with `dotnet publish` or use the included GitHub Actions workflow as a starting point.
