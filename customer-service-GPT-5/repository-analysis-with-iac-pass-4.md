USER:
can you analyse all the code in this workspace (including all IaC and configuration files) and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

As you produce output into this chat, keep a word-for-word record of my inputs and your outputs in a file called repository-analysis-with-iac-pass-4.md in the root of the workspace.

Ignore existing .md files - don't read their content as part of your analysis. Where possible, use mermaid diagrams for ease of viewing. Ensure the syntax and characters used in the mermaid diagrams are highly compatible with most parsers. For example, don't use parentheses, ( or ), anywhere in the mermaid diagram syntax.

ASSISTANT:
## Purpose
This repository implements a customer identity and profile microservice exposed via Azure Functions (isolated worker .NET 8) and fronted by two Azure API Management (APIM) gateways: one public customer facing and one internal admin facing. It manages customer accounts (create, authenticate, view, update), issues JWT access and refresh tokens (refresh path currently stubbed), maps external Azure AD group memberships to internal permission claims, and provides admin endpoints protected by policy based authorization. Data is stored in Azure Cosmos DB. Secrets and signing keys are intended to reside in Azure Key Vault (with a development symmetric key fallback). Telemetry flows to Application Insights. Deployment and infra are defined as code with Bicep, including APIM, Cosmos DB, Key Vault, Function App (container deployed from ACR), and networking for an internal admin APIM.

## High Level Architectural Diagram
```mermaid
graph TD
  userBrowser[Customer Client Web Or Mobile] -->|HTTPS REST JSON| publicApim[APIM Customer Facing]
  adminOperator[Admin Operator Secure Network] -->|HTTPS Internal| adminApim[APIM Admin Internal]
  publicApim -->|Forward api requests| funcApp[Azure Functions CustomerService]
  adminApim -->|Forward admin api requests| funcApp
  funcApp -->|CRUD CustomerAccount documents| cosmos[Azure Cosmos DB]
  funcApp -->|Read jwt signing key secret| keyvault[Azure Key Vault]
  funcApp -->|Emit telemetry| appInsights[Azure Application Insights]
  DevOps[CI CD Pipeline] -->|Push container image| acr[Azure Container Registry]
  acr -->|Pull image| funcApp
  keyvault -->|Stores jwt signing key secret| jwtSecret[Secret jwt signing key]
```

## Program Flow (Key User Journeys)
```mermaid
flowchart TD
  A[Create Account POST customers] --> B[Check existing by email]
  B -->|Exists| E[Conflict 409]
  B -->|Not found| C[Hash password with bcrypt]
  C --> D[Persist record in cosmos]
  D --> F[Return 201 with id email displayName]

  subgraph Login Flow
    L1[Login POST customers login] --> L2[Lookup user email]
    L2 -->|Not found or password mismatch| LErr[401 invalid credentials]
    L2 -->|Match| L3[IssueTokens build JWT claims]
    L3 --> L4[Sign JWT using symmetric key fallback]
    L4 --> L5[Generate refresh token in memory]
    L5 --> L6[Return 200 access token refresh token]
  end

  subgraph Get Profile
    G1[GET customers me] --> G2[Authorization middleware extracts bearer token]
    G2 --> G3[Map groups to permission claims]
    G3 --> G4[Load customer account by subject tid]
    G4 -->|Found| G5[Return profile fields]
    G4 -->|Missing| GErr[404]
  end

  subgraph Update Profile
    U1[PATCH customers me] --> U2[Auth middleware same steps]
    U2 --> U3[Validate payload themeMode]
    U3 --> U4[Apply immutable record with new fields]
    U4 --> U5[Upsert cosmos]
    U5 --> U6[Return updated projection]
  end
```

## Security Focus Diagram
```mermaid
graph LR
  subgraph Identity And Auth
    TknService[Token Service In Memory refresh map]
    AuthMW[Authorization Middleware]
    ClaimsMap[Claims Mapping Service]
  end
  subgraph Data Layer
    Cosmos[Cosmos DB customers refreshTokens future]
    KeyVault[Key Vault jwt signing key secret]
  end
  subgraph Edge
    PublicAPIM[Public APIM]
    AdminAPIM[Admin APIM Internal]
  end
  subgraph Observability
    AppInsights[Application Insights]
  end
  Client[Customer Client] --> PublicAPIM --> AuthMW --> Cosmos
  AdminUser[Admin Operator] --> AdminAPIM --> AuthMW
  AuthMW --> ClaimsMap --> KeyVault
  TknService --> KeyVault
  AuthMW --> AppInsights
  Cosmos --> AppInsights
```

## Security Controls And Gaps
Controls:
- Segregated ingress: separate internal admin APIM instance intended for restricted network access
- Policy based authorization: attribute driven checks plus dynamic mapping from external groups to internal cs.permission claims and policies AdminRead and AdminWrite
- Principle of least privilege support: fine grained permission constants for read only vs read write admin operations
- Data partitioning: Cosmos DB partition key tenantId enabling multi tenant isolation patterns
- Unique key constraint on tenantId + normalizedEmail preventing duplicate accounts per tenant
- Secret management: Key Vault defined for jwt signing key secret storage with soft delete purge protection enabled and RBAC auth model
- HTTPS enforced: Function App httpsOnly true APIM uses TLS custom domain
- Telemetry: Application Insights integration for logging metrics aiding detection monitoring
- Dependency containerization: image pulled from private ACR (admin user disabled)

Notable gaps or risks:
- Refresh tokens not persisted securely In memory dictionary only no hashing no binding to user or revocation semantics implemented fully
- JWT signing via Key Vault not actually implemented fallback symmetric key always used and key rotation story incomplete
- Missing token validation pipeline no verification of signature audience issuer expiration implemented in middleware only parsing without validation
- MFA flow is placeholder not linked to account storage does not store or enforce secret
- Password policy absent no complexity rate limiting or lockout beyond fields in model unused
- No rate limiting throttling DDoS protection definitions in APIM policies not included
- No explicit logging of security relevant events like failed logins admin updates or token issuance
- Admin and public APIs currently share same backend function app without network isolation at function app level internal APIM does not enforce private endpoint to function
- Lack of encryption enforcement for Cosmos client side there is no at rest key rotation config referenced
- No data classification masking or PII minimization beyond simple model

## Recommendations (Prioritized)
1 Implement proper JWT validation middleware with signature audience issuer expiry and optionally Key Vault backed signing with cryptography client
2 Persist refresh tokens hashed with user binding tenant binding rotation chain and revocation audit using Cosmos refreshTokens container or alternate store
3 Complete MFA implementation tie secret to CustomerAccount.Security store encrypted secret implement verification during login issue step up tokens
4 Add APIM policies for rate limiting ip filtering request validation and response headers security
5 Separate admin function app or restrict function app access via private endpoints service endpoints and disable public network for Cosmos and Key Vault
6 Implement password complexity rules and lockout threshold leveraging existing FailedLoginAttempts and LockoutEndUtc
7 Centralize security audit logging include structured events for authentication authorization data changes
8 Add unit and integration tests for authorization middleware paths including policies and claim enrichment
9 Remove symmetric key from config in production rely solely on Key Vault and configure managed identity for function app with least privilege access to secret get
10 Implement secure secret retrieval JWT_SIGNING_KEY__KV_SECRET_NAME currently set but not read by code integrate retrieval at startup

## Textual Overview
The system is a foundational customer account microservice designed for extensibility into a fuller identity management solution. It already models core entities and authorization primitives while infrastructure as code provisions a production like baseline including dual APIM tier separation Key Vault and Cosmos DB. The critical next steps revolve around solidifying cryptographic key usage token lifecycle management MFA completion and network isolation.

ASSISTANT:
Duplicate of analysis provided in chat (maintaining transcript integrity).
## Purpose
This repository implements a customer identity and profile microservice exposed via Azure Functions (isolated worker .NET 8) and fronted by two Azure API Management (APIM) gateways: one public customer facing and one internal admin facing. It manages customer accounts (create, authenticate, view, update), issues JWT access and refresh tokens (refresh path currently stubbed), maps external Azure AD group memberships to internal permission claims, and provides admin endpoints protected by policy based authorization. Data is stored in Azure Cosmos DB. Secrets and signing keys are intended to reside in Azure Key Vault (with a development symmetric key fallback). Telemetry flows to Application Insights. Deployment and infra are defined as code with Bicep, including APIM, Cosmos DB, Key Vault, Function App (container deployed from ACR), and networking for an internal admin APIM.

## High Level Architectural Diagram
```mermaid
graph TD
  userBrowser[Customer Client Web Or Mobile] -->|HTTPS REST JSON| publicApim[APIM Customer Facing]
  adminOperator[Admin Operator Secure Network] -->|HTTPS Internal| adminApim[APIM Admin Internal]
  publicApim -->|Forward api requests| funcApp[Azure Functions CustomerService]
  adminApim -->|Forward admin api requests| funcApp
  funcApp -->|CRUD CustomerAccount documents| cosmos[Azure Cosmos DB]
  funcApp -->|Read jwt signing key secret| keyvault[Azure Key Vault]
  funcApp -->|Emit telemetry| appInsights[Azure Application Insights]
  DevOps[CI CD Pipeline] -->|Push container image| acr[Azure Container Registry]
  acr -->|Pull image| funcApp
  keyvault -->|Stores jwt signing key secret| jwtSecret[Secret jwt signing key]
```

## Program Flow (Key User Journeys)
```mermaid
flowchart TD
  A[Create Account POST customers] --> B[Check existing by email]
  B -->|Exists| E[Conflict 409]
  B -->|Not found| C[Hash password with bcrypt]
  C --> D[Persist record in cosmos]
  D --> F[Return 201 with id email displayName]

  subgraph Login Flow
    L1[Login POST customers login] --> L2[Lookup user email]
    L2 -->|Not found or password mismatch| LErr[401 invalid credentials]
    L2 -->|Match| L3[IssueTokens build JWT claims]
    L3 --> L4[Sign JWT using symmetric key fallback]
    L4 --> L5[Generate refresh token in memory]
    L5 --> L6[Return 200 access token refresh token]
  end

  subgraph Get Profile
    G1[GET customers me] --> G2[Authorization middleware extracts bearer token]
    G2 --> G3[Map groups to permission claims]
    G3 --> G4[Load customer account by subject tid]
    G4 -->|Found| G5[Return profile fields]
    G4 -->|Missing| GErr[404]
  end

  subgraph Update Profile
    U1[PATCH customers me] --> U2[Auth middleware same steps]
    U2 --> U3[Validate payload themeMode]
    U3 --> U4[Apply immutable record with new fields]
    U4 --> U5[Upsert cosmos]
    U5 --> U6[Return updated projection]
  end
```

## Security Focus Diagram
```mermaid
graph LR
  subgraph Identity And Auth
    TknService[Token Service In Memory refresh map]
    AuthMW[Authorization Middleware]
    ClaimsMap[Claims Mapping Service]
  end
  subgraph Data Layer
    Cosmos[Cosmos DB customers refreshTokens future]
    KeyVault[Key Vault jwt signing key secret]
  end
  subgraph Edge
    PublicAPIM[Public APIM]
    AdminAPIM[Admin APIM Internal]
  end
  subgraph Observability
    AppInsights[Application Insights]
  end
  Client[Customer Client] --> PublicAPIM --> AuthMW --> Cosmos
  AdminUser[Admin Operator] --> AdminAPIM --> AuthMW
  AuthMW --> ClaimsMap --> KeyVault
  TknService --> KeyVault
  AuthMW --> AppInsights
  Cosmos --> AppInsights
```

## Security Controls And Gaps
Controls:
- Segregated ingress: separate internal admin APIM instance intended for restricted network access
- Policy based authorization: attribute driven checks plus dynamic mapping from external groups to internal cs.permission claims and policies AdminRead and AdminWrite
- Principle of least privilege support: fine grained permission constants for read only vs read write admin operations
- Data partitioning: Cosmos DB partition key tenantId enabling multi tenant isolation patterns
- Unique key constraint on tenantId + normalizedEmail preventing duplicate accounts per tenant
- Secret management: Key Vault defined for jwt signing key secret storage with soft delete purge protection enabled and RBAC auth model
- HTTPS enforced: Function App httpsOnly true APIM uses TLS custom domain
- Telemetry: Application Insights integration for logging metrics aiding detection monitoring
- Dependency containerization: image pulled from private ACR (admin user disabled)

Notable gaps or risks:
- Refresh tokens not persisted securely In memory dictionary only no hashing no binding to user or revocation semantics implemented fully
- JWT signing via Key Vault not actually implemented fallback symmetric key always used and key rotation story incomplete
- Missing token validation pipeline no verification of signature audience issuer expiration implemented in middleware only parsing without validation
- MFA flow is placeholder not linked to account storage does not store or enforce secret
- Password policy absent no complexity rate limiting or lockout beyond fields in model unused
- No rate limiting throttling DDoS protection definitions in APIM policies not included
- No explicit logging of security relevant events like failed logins admin updates or token issuance
- Admin and public APIs currently share same backend function app without network isolation at function app level internal APIM does not enforce private endpoint to function
- Lack of encryption enforcement for Cosmos client side there is no at rest key rotation config referenced
- No data classification masking or PII minimization beyond simple model

## Recommendations (Prioritized)
1 Implement proper JWT validation middleware with signature audience issuer expiry and optionally Key Vault backed signing with cryptography client
2 Persist refresh tokens hashed with user binding tenant binding rotation chain and revocation audit using Cosmos refreshTokens container or alternate store
3 Complete MFA implementation tie secret to CustomerAccount.Security store encrypted secret implement verification during login issue step up tokens
4 Add APIM policies for rate limiting ip filtering request validation and response headers security
5 Separate admin function app or restrict function app access via private endpoints service endpoints and disable public network for Cosmos and Key Vault
6 Implement password complexity rules and lockout threshold leveraging existing FailedLoginAttempts and LockoutEndUtc
7 Centralize security audit logging include structured events for authentication authorization data changes
8 Add unit and integration tests for authorization middleware paths including policies and claim enrichment
9 Remove symmetric key from config in production rely solely on Key Vault and configure managed identity for function app with least privilege access to secret get
10 Implement secure secret retrieval JWT_SIGNING_KEY__KV_SECRET_NAME currently set but not read by code integrate retrieval at startup

## Textual Overview
The system is a foundational customer account microservice designed for extensibility into a fuller identity management solution. It already models core entities and authorization primitives while infrastructure as code provisions a production like baseline including dual APIM tier separation Key Vault and Cosmos DB. The critical next steps revolve around solidifying cryptographic key usage token lifecycle management MFA completion and network isolation.
