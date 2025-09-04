USER INPUT:
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

As you produce output into this chat, keep a word-for-word record of my inputs and your outputs in a file called repository-analysis-pass-6.md in the root of the workspace.

Ignore existing .md files - don't read their content as part of your analysis

ASSISTANT OUTPUT:
Task receipt & plan:
Checklist:
1. Determine purpose of software.
2. Architectural tech stack diagram (ASCII).
3. Program flow diagram(s).
4. Security concerns mapping diagram.
5. Persist verbatim conversation (this file).

Purpose:
This project implements a customer account management micro-service (customer identity & profile) as an Azure Functions (isolated .NET 8) application fronted by Azure API Management (separate public customer and private admin gateways). It provides endpoints for account creation, authentication (JWT + refresh token scaffold), profile retrieval & update, and administrative read/update operations. Data is stored in Azure Cosmos DB. Security layers include JWT-based auth, claims/permission mapping, policy-based authorization, and planned MFA & refresh token persistence. Infrastructure is provisioned via Bicep including Cosmos DB, Key Vault, Function App (containerized), API Management instances, Container Registry, and optional VNet for internal admin surface.

Key components (runtime):
- Azure Function App (.NET isolated) hosting HTTP-triggered functions.
- CustomerFunctions: public customer endpoints (create, login, refresh (stub), MFA enroll/verify (demo), logout, get/update self).
- AdminFunctions: internal admin endpoints (get/update arbitrary customer) with stricter policies.
- AuthorizationMiddleware: custom pipeline performing JWT extraction, claims enrichment, permission/policy evaluation, optional customer record loading.
- TokenService: issues JWT access + in-memory refresh tokens; optional Azure Key Vault for asymmetric keys (currently falls back to symmetric secret key).
- Cosmos repositories: customer accounts & refresh tokens (refresh token persistence not yet wired in TokenService usage; in-memory dictionary currently used).
- Models: immutable record types for customer accounts & security/audit metadata.
- Logging & telemetry: Serilog + Application Insights.

High-level architectural diagram (ASCII):
```
                +-----------------------------+
                |     Developer / DevOps      |
                |  (Build & Push Container)   |
                +--------------+--------------+
                               |
                               v
                      +------------------+
                      | Azure Container  |
                      |    Registry      |
                      +---------+--------+
                                |
                   (linuxFxVersion pulls image)
                                |
        +-----------------------v-----------------------+
        |            Azure Function App (DOTNET)        |
        |  - CustomerFunctions                          |
        |  - AdminFunctions                             |
        |  - AuthorizationMiddleware                    |
        |  - TokenService                               |
        +-----------+---------------+-------------------+
                    |               |
                    |               | (Key retrieval / secrets)
          (CRUD)    |               v
                    |       +---------------+
                    v       | Azure Key     |
        +----------------+  |   Vault       |
        |  Cosmos DB     |  +---------------+
        |  (Accounts,    |
        |   RefreshTokens)|
        +----------------+
              ^   ^
              |   | (Future: store refresh tokens fully)
              |   +-------------------------------+
              |                                   |
   +----------+----------+             +----------+-----------+
   | Public Clients      |             | Internal Admin Tools |
   | (Web/Mobile, etc.)  |             |  (Ops Portal, etc.)  |
   +----------+----------+             +----------+-----------+
              |                                   |
              v                                   v
      +-------------------+             +-------------------+
      | API Management    |             | API Management    |
      | (Customer - Pub)  |             | (Admin - Internal)|
      +---------+---------+             +---------+---------+
                \_________________________/|
                              (HTTPS)      |
                                            (Private VNet / Restricted Access)
```

Program flow (Login & authorized request):
```
Client -> Customer APIM -> Function (Login)
 1. POST /customers/login {email,password}
 2. Function loads account via ICustomerRepository (Cosmos)
 3. Verify BCrypt password
 4. TokenService.IssueTokens -> create JWT (HMAC) + refresh token (in-memory)
 5. Return {access_token, refresh_token, expires_in}

Subsequent authorized call (GET /customers/me):
Client -> Customer APIM -> Function App (GetMe)
 1. AuthorizationMiddleware extracts bearer token
 2. Parses JWT (no signature validation implemented yet beyond parse)
 3. ClaimsMappingService adds internal permission claims from group IDs (if any)
 4. LoadCustomerAccount attribute -> repository fetch by sub (customer id)
 5. Function returns profile JSON

Admin update flow (PATCH /admin/customers/{id}):
Internal Admin Client -> Admin APIM -> Function App (AdminUpdateCustomer)
 1. Middleware authenticates & enriches principal
 2. AuthorizePolicy("AdminWrite") evaluated (permission mapping)
 3. Repository fetch existing account
 4. Apply updates + audit metadata (UpdatedBy from principal)
 5. Upsert to Cosmos, return updated account
```

State & data model highlights:
- CustomerAccount (immutable record) with nested Security, Preferences, Audit.
- Partition key: tenantId enabling multi-tenant scaling.
- Unique key on (tenantId, normalizedEmail) enforced via Cosmos container configuration.
- RefreshTokenRecord model prepared for persistent storage (revocation chains, replacedByTokenHash) though current TokenService uses in-memory dictionary (gap).

Security mapping diagram (concerns -> implementation):
```
Confidentiality:  HTTPS enforced (APIM, Function), Key Vault for JWT key, Cosmos (encrypted at rest by Azure).
Integrity:        JWT signing (symmetric currently), unique keys prevent duplicate accounts, audit metadata captures updates.
Availability:     Serverless Functions (consumption/elastic), Cosmos serverless, APIM fronting.
Authentication:   JWT bearer tokens issued at login; MFA enrollment endpoints (demo) + placeholder for verification.
Authorization:    Custom attributes + middleware (RequireAuthentication, AuthorizePolicy, permission claims) & group->permission mapping.
Least Privilege:  Separation of customer vs admin APIs (public vs internal APIM + optional VNet for admin).
Secrets Mgmt:     Key Vault for jwt-signing-key (Bicep secret resource) + environment variables.
Password Security: BCrypt hashed passwords (PasswordHash), versioning field (PasswordVersion) enabling future migrations.
Token Security:   Short-lived access token, refresh token concept (not fully persisted / bound yet = improvement area).
MFA:              TOTP enrollment & verification sample (secret not yet stored encrypted; placeholder email & missing persistence = gap).
Auditing:         AuditMetadata (CreatedUtc/By, UpdatedUtc/By) updated in admin & self-update paths.
Input Validation: Basic null/format checks; need stronger validation & rate limiting (future via APIM policies).
Logging/Monitoring: Serilog console + Application Insights telemetry service.
Network Segmentation: Separate internal APIM intended inside VNet; future private endpoints for Function & Cosmos recommended.
```

Security-focused component diagram:
```
 [Client] --HTTPS--> [Customer APIM] --HTTPS--> [Auth Middleware] --calls--> [Cosmos DB]
                                         |                 ^
                                         v                 |
                                   [Claims Mapping]        |
                                         |                 |
                                         v                 |
                                      [Policies]           |
                                         |
                                         v
                                [Function Endpoint]
                                         |
                                         v
                                    [TokenService] --(secret key)--> [Key Vault]

Internal Admin Path:
 [Admin Client] --Private/HTTPS--> [Admin APIM] -> same chain as above (adds Admin policies)
```

Notable gaps / improvement opportunities:
- JWT validation currently only parses token; should validate signature, issuer, audience, expiration using TokenValidationParameters.
- Refresh tokens stored in-memory only; integrate CosmosRefreshTokenRepository for persistence & rotation; bind refresh tokens to user & device, capture IP, revoke chains.
- MFA secrets not stored (TotpSecretEncrypted field unused); implement encryption (e.g., Key Vault + Protectors) and enforcement in auth pipeline.
- Password complexity, rate limiting, lockout strategy (fields exist, logic not implemented) need completion.
- Consider moving admin endpoints to separate Function App behind private endpoint to ensure isolation.
- Add structured logging, correlation IDs, and failure metrics; centralize problem responses for consistency.

Summary:
The service delivers a foundational customer identity/profile API with custom authorization middleware, Cosmos persistence, and infrastructure-as-code for a production-aligned Azure deployment. Core security primitives are present (hashing, JWT issuance, permission policies, Key Vault), with clear next steps to harden token validation, refresh token persistence, MFA storage/enforcement, and network isolation.
