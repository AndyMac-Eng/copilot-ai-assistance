# Customer Microservice

## Overview
A .NET 8 isolated Azure Functions microservice providing customer account management:
- Create customer accounts
- Login (JWT issuance)
- Fetch current (authenticated) customer profile

Secured and production-hardened design using Azure Cosmos DB (serverless) for storage, Azure Key Vault for secret management, Azure Container Registry + Functions (container) for compute, and Azure API Management (Consumption) for domain, authentication, throttling, and observability.

## Architecture Choices & Rationale
- Azure Functions (isolated, .NET 8): Event-driven, low idle cost, easy horizontal scaling, supports container image for portability.
- Cosmos DB (serverless): Rapid development, elastic scale, multi-tenant partition strategy using `/tenantId`, unique key on `(tenantId, normalizedEmail)` ensures per-tenant uniqueness and efficient point reads.
- Key Vault + Managed Identity: Secrets (JWT signing key placeholder, future per-tenant secrets) and key rotation without code changes.
- API Management (Consumption): Lightweight global gateway for auth enforcement (validate JWT), rate limiting, custom domain termination, central policy management.
- JWT Auth: Symmetric key for now; recommend migration to asymmetric (Key Vault stored) for multi-service validation without shared secret risk.
- Bicep IaC: Modular, declarative, idempotent; easily integrated into CI/CD. Parameter file separates environment-specific config.
- Serilog + App Insights: Structured logs, distributed tracing readiness through APIM + Functions + Cosmos diagnostics.

## Security Considerations
- Passwords hashed with BCrypt (work factor via library defaults). Recommend parameterizing work factor and enabling staged rehash.
- Lockout & MFA fields reserved for future use.
- Disable Cosmos DB local auth; rely on AAD + managed identity.
- HTTPS enforced end-to-end; APIM provides TLS offload for custom domain.
- Principle of least privilege: Function app pulls secrets at runtime via managed identity (not yet wired in sample code; currently env vars + TODO to integrate Key Vault references / SDK retrieval).
- Suggest APIM policy: JWT validation (issuer, audience, exp, signature) & anti-abuse (rate-limit-by-key), not included directly in code to keep cross-cutting concerns centralized.

## Repository Layout
```
src/CustomerService/        # Function app source
infra/                      # Bicep IaC
.github/workflows/          # CI pipeline
Dockerfile                  # Container image definition
```

## Running Locally
1. Ensure Cosmos DB emulator or set env `COSMOS_CONNECTION_STRING` to a valid account.
2. Provide a development JWT signing key:
```
setx JWT_SIGNING_KEY "local-dev-key-change"
```
3. Start Functions:
```
dotnet build src/CustomerService/CustomerService.csproj
func start --csharp --script-root src/CustomerService/bin/Debug/net8.0
```

## HTTP Endpoints
| Method | Route | Description |
|--------|-------|-------------|
| POST | /api/customers | Create account |
| POST | /api/customers/login | Login, returns JWT |
| GET | /api/customers/me | Returns current user profile |

## Sample cURL
```
# Create
curl -X POST http://localhost:7071/api/customers -H "Content-Type: application/json" -d '{"email":"a@b.com","password":"Passw0rd!"}'
# Login
curl -X POST http://localhost:7071/api/customers/login -H "Content-Type: application/json" -d '{"email":"a@b.com","password":"Passw0rd!"}'
# Use token
curl http://localhost:7071/api/customers/me -H "Authorization: Bearer <token>"
```

## Deployment (Conceptual)
1. Build & push image (CI does this for main branch).
2. Deploy infra:
```
az deployment group create -g <rg> -f infra/main.bicep -p infra/main.parameters.json \
  -p environment=dev jwtSigningKey="generate-temp" apimPublisherEmail=... apimPublisherName=... apiCustomDomain=... apiCustomDomainCertId=...
```
3. Update Function app configuration / Key Vault secret rotations as needed.

## Next Steps / Enhancements
Implemented so far:
- Initial unit test for TokenService.

Planned (requested):
1. Asymmetric JWT signing (Key Vault key + kid header; rotate via KV versioning) with fallback to symmetric for local dev.
2. Refresh token & logout strategy (short-lived access token ~15m, refresh ~7d, revocation via distributed cache e.g., Azure Cache for Redis + jti blacklist TTL; or rotate signing keys).
3. MFA: TOTP enrollment (provisioning URI + QR) and verification endpoints (Otp.NET) with recovery codes stored hashed.
4. APIM policies: inbound JWT validate, rate limiting (by IP & user), CORS allowlist, security headers (X-Content-Type-Options, X-Frame-Options=DENY, Referrer-Policy, Content-Security-Policy baseline), response masking of sensitive headers.
5. Resilience: Polly policies (retry w/ exponential backoff, circuit breaker) around Cosmos & Key Vault.
6. Telemetry correlation: propagate traceparent from APIM to Functions and enrich logs with customerId/tenantId (PII safe).
7. Secret retrieval & caching: use KeyClient/CryptographyClient to sign JWT (RS256) and local cache with refresh before expiry.
8. Additional test coverage: repository integration (using Cosmos emulator), API contract tests (e.g., with Verify or Snapshots), security tests (password policies, lockout), performance micro-bench for token issue.
9. Authorization refinement: per-role & future scopes claims; consider integrating Azure Entra for external identity federation.
10. Observability: structured request logging middleware & custom metrics (accounts_created, login_failures, mfa_challenges).

## License
Internal use microservice template.
