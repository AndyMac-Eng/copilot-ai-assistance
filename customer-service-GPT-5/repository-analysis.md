User Request:
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

Assistant Response (will append progressively below):

## 1. Purpose of the Software
This repository implements a .NET 8 isolated-process Azure Functions microservice named "Customer Service" that provides foundational customer account and identity features for a larger platform. Core capabilities:
- Local (email + password) account creation and login (issuing JWT access + refresh tokens)
- External OAuth identity onboarding (Apple, Microsoft, LinkedIn) with state + nonce protection and identity linking logic
- Multi-tenant readiness (partition key `tenantId`, currently "default")
- Future extensibility for MFA (prototype TOTP endpoints), refresh token persistence, and role-based claims
- Infrastructure as Code (Bicep) provisioning of Azure resources: API Management (gateway), Function App (container-based), Cosmos DB (serverless), Key Vault, Container Registry.

Intended use:
- Acts as the authentication/identity boundary for customer-facing applications
- Issues tokens other services can trust (migration path to asymmetric signing via Key Vault)
- Provides an integration point for multiple external identity providers while enforcing per-user external identity caps and explicit linking (no silent auto-link by email)

## 2. Architectural Diagram (Tech Stack)
```
					 +---------------------------+
					 |    Client Applications    |
					 | (Web / Mobile / SPA / B2C)|
					 +-------------+-------------+
										|
										v HTTPS (TLS)
						  +----------------------+
						  |  Azure API Management|  <-- Global gateway
						  |  (Consumption)       |      - JWT validation (planned)
						  |  - Rate limiting     |      - CORS/security headers
						  +----------+-----------+
										 |
				 Internal HTTPS    | (Forwarded auth headers, traceparent)
										 v
						  +----------------------+
						  | Azure Functions      |  (.NET 8 isolated, container)
						  |  CustomerService     |
						  +----------+-----------+
			DI / Services         |   Function Endpoints
	+---------------------+     |     - POST /customers
	| TokenService        |-----+     - POST /customers/login
	| OAuthService        |           - POST /customers/refresh (stub)
	| OAuth Providers     |           - GET  /customers/me
	|  - Apple            |           - OAuth Start/Callback
	|  - Microsoft        |           - MFA (enroll/verify prototypes)
	|  - LinkedIn         |
	+----------+----------+
				  |                            
	  +--------+--------------+  Secrets / Keys (future asymmetric)
	  |  Cosmos DB (Serverless)|<-------------------+
	  |  - customers container |                    |
	  |  - externalIdentities  |                    |
	  |  - refreshTokens       | (future full impl) |
	  +------------------------+                    |
																  |
							  +------------------+       |
							  |  Azure Key Vault |-------+
							  |  (jwt-signing-key| (current: symmetric secret; roadmap: KV key)
							  +------------------+

							  +------------------+
							  | Azure Container  |
							  | Registry (images)|
							  +------------------+

Observability: App Insights (logs, metrics) via Functions + APIM integration.
```

## 3. Program Flow Diagram (Major Authentication Paths)
### 3.1 Email/Password Authentication
```
Client -> POST /customers -----------------> [CreateAccount]
  JSON {email,password,...}                 Validate + hash (BCrypt) -> Store in Cosmos (customers)
														 <- 201 {id,email,displayName}

Client -> POST /customers/login ----------> [Login]
  JSON {email,password}                    Fetch account (GetByEmail) -> BCrypt.Verify
														 If ok -> TokenService.IssueTokens()
														 (Generate JWT + refresh token (in-memory currently))
														 <- 200 {access_token, refresh_token, expires_in}

Client -> GET /customers/me --------------> [GetMe]
  Authorization: Bearer <JWT>              Parse JWT (no full validation here) -> repo.GetById
														 <- 200 profile JSON
```

### 3.2 OAuth External Identity (Login or Link)
```
Client -> GET /oauth/{provider}/start ----> [OAuthFunctions.Start]
  optional linkCustomerId                  OAuthService.StartAsync:
														 - Create state + nonce
														 - Store in InMemoryOAuthStateStore
														 - Build provider authorization URL
														 <- 302 Location: provider authorization page

Provider -> redirect -> /oauth/{prov}/callback?code=...&state=...
											[OAuthFunctions.Callback]
											OAuthService.CompleteAsync:
											  - Validate & consume state
											  - Exchange code (stub returns fake profile)
											  - If linking -> create ExternalIdentity entry
											  - Else create new CustomerAccount + ExternalIdentity
											  - Issue tokens (TokenService)
											<- 200 {access_token, refresh_token}
```

### 3.3 MFA (Prototype Only)
```
Client -> POST /customers/mfa/enroll ------> Generates secret (random 20B) -> otpauth:// URI returned
Client -> POST /customers/mfa/verify ------> Validates TOTP (window -1/+1) -> 204 / 401
```

### 3.4 Refresh Token (Future Complete Flow)
Current endpoint returns NotImplemented after validation placeholder. Roadmap includes persistent refresh token repository usage + binding tokens to user, revocation, rotation.

## 4. Security Mapping Diagram
```
Threat / Control Layered View

 [Clients]
	 |  Input validation (basic) / TLS enforced downstream
	 v
 [API Management]
	 - Planned: JWT signature + exp/aud/iss validation
	 - Rate limiting (DoS / brute-force mitigation)
	 - CORS policy & security headers
	 - Centralized logging
	 |
	 v
 [Azure Functions Runtime]
	 - DI-managed services (least privilege within code)
	 - Serilog structured logging
	 - MFA endpoints (future enforcement hook)
	 - Password hashing (BCrypt)
	 - State & nonce for OAuth (CSRF, replay mitigation)
	 - In-memory refresh token store (needs hardening)
	 |
	 v
 [TokenService]
	 - JWT issuance (HMAC today; roadmap: RSA via Key Vault)
	 - Short-lived access tokens (configurable)
	 - Refresh token tracking (will move to Cosmos + hashed storage)
	 |
	 v
 [Cosmos DB]
	 - Serverless, partitioned by tenantId (data isolation)
	 - Unique constraints (normalizedEmail, provider+subject)
	 - Local auth disabled (AAD + managed identity recommended)
	 |
	 v
 [Key Vault]
	 - Secure secret storage (jwt-signing-key placeholder)
	 - Roadmap: asymmetric key versioning & rotation
```

### Security Concerns & Implemented / Planned Controls
| Concern | Implemented | Planned Enhancements |
|---------|-------------|----------------------|
| Password compromise | BCrypt hashing | Parameterized work factor & rehash policy |
| Token forgery | HMAC signing | RS256 via KV key + key rotation + kid headers |
| OAuth CSRF | state + nonce | Persist state in distributed store |
| Replay / stolen refresh | In-memory store (weak) | Hashed refresh tokens in Cosmos with rotation chain |
| Brute force login | None yet | APIM rate limit + lockout counters |
| MFA bypass | Prototype only | Enforce on sensitive actions, encrypted secret storage |
| Secrets leakage | App settings fallback | Managed identity + Key Vault references only |
| Lateral movement | Least privilege design | RBAC-scope KV access, Cosmos role definitions |
| PII exposure | Minimal fields returned | Data classification, selective redaction in logs |

## 5. Notable Code Observations
- `TokenService` currently throws `NotImplementedException` inside the asymmetric branch then falls back—documented upgrade path.
- `RefreshToken` persistence repository exists (`CosmosRefreshTokenRepository`), but endpoints still use in-memory logic; integration pending.
- `GetMe` does naive JWT parse (no signature validation)—acceptable in sample if APIM does auth, but should add validation middleware for defense-in-depth.
- MFA secrets not stored; the enrollment endpoint returns the secret directly (should encrypt + store hashed recovery codes before production use).

## 6. Suggested Immediate Hardening Steps
1. Integrate refresh token repository: hash token (e.g., SHA256) before storage, store metadata, rotate on use.
2. Add JWT validation middleware (use `TokenValidationParameters`) or rely on APIM but still validate locally for critical endpoints.
3. Implement Key Vault RSA signing (wrap `CryptographyClient.SignDataAsync`).
4. Persist OAuth state in Cosmos or distributed cache with strict TTL.
5. Add lockout policy (increment FailedLoginAttempts; enforce exponential backoff / temporary lockout).
6. Encrypt TOTP secret (e.g., using Key Vault or local data-protection key + AES-GCM) before persisting.
7. Centralize error response schema; avoid leaking account existence timing differences.
8. Add structured audit events (account_created, login_success, login_failure, oauth_linked, token_refreshed, mfa_enrolled).

## 7. End-to-End Sequence (Consolidated)
```
User -> APIM -> Azure Function Endpoint -> Repository/Service -> Cosmos/KeyVault -> Response

Example: OAuth Login
1. Start: Client /oauth/microsoft/start
2. APIM forwards -> Function (Start) -> OAuthService.StartAsync -> StateStore
3. Redirect to Microsoft login
4. Callback: Provider -> /oauth/microsoft/callback with code+state
5. Function -> OAuthService.CompleteAsync -> Validate state -> Exchange (stub) -> Create or fetch CustomerAccount -> Create ExternalIdentity -> TokenService.IssueTokens
6. Return tokens to client

Example: Password Login
1. POST /customers/login
2. Lookup account by normalizedEmail
3. Verify BCrypt hash
4. Issue JWT + refresh pair
5. Return tokens
```

## 8. Repository Inventory (High-Level)
- Functions: `CustomerFunctions`, `OAuthFunctions`
- Services: `TokenService`, `OAuthService`, provider implementations
- Repositories: Cosmos-backed (customers, external identities, refresh tokens), plus interfaces
- Models: Account, ExternalIdentity, RefreshTokenRecord
- Infra (Bicep): APIM, Cosmos (3 containers), Function App (container image), Key Vault (secret), ACR
- Tests: Token issuance, OAuth start/complete

## 9. Transcript Continuation Log
The above answer text (from "## 1. Purpose" through this line) is appended verbatim to satisfy the requirement of keeping a word-for-word record.

