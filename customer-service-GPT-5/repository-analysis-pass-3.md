# repository-analysis-pass-3

## Conversation Log (Most Recent First Append)

USER: can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

USER: As you produce output into this chat, keep a word-for-word record of my inputs and your outputs in a file called repository-analysis-pass-3.md in the root of the workspace.

ASSISTANT (analysis starting): Gathering code and preparing analysis.

ASSISTANT (full analysis to follow below)...

---

## Analysis Output

<!-- The detailed analysis will be appended below in the next assistant message to preserve exact wording requirements. -->

ASSISTANT: PURPOSE
This project implements a customer account service delivered as an Azure Functions (isolated .NET 8) backend exposing public customer endpoints and restricted admin endpoints (intended for internal/private ingress). It manages customer identities, basic profile data, authentication (password + JWT issuance), rudimentary MFA enrollment (TOTP scaffold), and account self‑service update. It also defines infrastructure-as-code (Bicep) provisioning Cosmos DB, Key Vault, API Management (public & internal), Function App (containerized), Container Registry, and networking for segregation of admin operations.

ASSISTANT: ARCHITECTURE OVERVIEW
Core runtime: Azure Functions (dotnet isolated) hosted in a Linux container pulled from ACR.
Ingress: Two API Management instances: public (customer) and internal (admin) – admin one optionally inside VNet.
Persistence: Azure Cosmos DB (SQL API) with containers for customers (and planned refresh tokens) using tenantId partitioning.
Security services: TokenService for JWT (HMAC signing or future Key Vault key), AuthorizationMiddleware for authn/z, ClaimsMappingService to map external AAD groups => internal permission claims, PermissionPolicyRegistry for higher-level policies.
Secrets & keys: Azure Key Vault (jwt-signing-key secret; potential asymmetric key). Application settings reference Key Vault secret name (future integration logic needed).
Monitoring: Application Insights telemetry + Serilog console logging.
Build & deployment: Container image stored in ACR; infra via Bicep.

ASSISTANT: TECH STACK DIAGRAM (ASCII)
Legend: [P]=Platform Service, (C)=Container/Code, {S}=Security Boundary

												+-----------------------------+
												|   Client Apps (Web/Mobile)  |
												+---------------+-------------+
																				|
																				v
													{Public Edge / Internet}
																				|
												 +--------------v---------------+   [P]
												 |  API Management (Public)     |
												 |  customer-api (HTTPS)        |
												 +--------------+---------------+
																				|
																				v
									 +-------------------+--------------------+
									 | Azure Function App (Linux Container)   | (C)
									 |  - .NET 8 isolated worker              |
									 |  - AuthorizationMiddleware             |
									 |  - Customer & Admin Functions code    |
									 +-----------+----------------------------+
															 | Services (DI)
					+--------------------+--------------------+---------------------+
					| TokenService       | ClaimsMappingSvc    | AuthorizationSvc    |
					| (JWT, refresh)     | (AAD groups->claims)| (permission checks) |
					+--------------------+--------------------+---------------------+
															 |
															 v
										+-------------------------------+   [P]
										| Cosmos DB (customers, tokens) |
										+-------------------------------+
															 |
				+----------------------+------------------+
				|                                         |
				v                                         v
	[P] Key Vault (jwt key)                [P] App Insights (telemetry)

				^
				|
	[P] Azure Container Registry (image source for Function App)

ADMIN PATH:
		Admin User -> (VPN / Private Network) -> API Management (Admin, internal VNet) -> same Function App (admin routes)

ASSISTANT: PROGRAM FLOW DIAGRAMS
1. Account Creation (POST /customers)
Client -> Public APIM -> Function (CreateAccount)
	-> CosmosCustomerRepository.GetByEmailAsync
	-> If absent: BCrypt hash password -> CosmosCustomerRepository.CreateAsync
	-> 201 Created (minimal account payload)

2. Login (POST /customers/login)
Client -> Public APIM -> Function (Login)
	-> CosmosCustomerRepository.GetByEmailAsync
	-> BCrypt.Verify(password, hash) ? else 401
	-> TokenService.IssueTokens (build claims, sign JWT, store refresh token in-memory) 
	-> 200 {access_token, refresh_token, expires_in}

3. Authenticated Self Data (GET /customers/me)
Client supplies Authorization: Bearer <JWT>
-> Public APIM -> Function Host -> AuthorizationMiddleware:
	 - Extract Authorization header
	 - Parse JWT (NOT validating signature currently – only decoding) *
	 - Enrich claims via ClaimsMappingService (AAD groups -> cs.permission claims)
	 - LoadCustomerAccountAttribute triggers repository GetByIdAsync
	 - Attach Principal + CustomerAccount to context
-> Execute GetMe returning filtered account fields

4. Update Self (PATCH /customers/me)
Same as above path plus validation of themeMode and repository Upsert (UpdateAsync).

5. Admin Get/Update (GET/PATCH /admin/customers/{id})
Admin Client -> (Private) Admin APIM -> Function Host -> AuthorizationMiddleware:
	- JWT decode & enrichment
	- AuthorizePolicyAttribute("AdminRead" / "AdminWrite") -> PermissionPolicyRegistry -> checks presence of permission claims
	- On success -> repository Get/Upsert

6. Refresh Token (POST /customers/refresh)
Currently placeholder: validates refresh token via TokenService.TryValidateRefreshToken (in-memory) then returns NOT IMPLEMENTED for new token issuance bound to user.

PROGRAM FLOW (GENERIC PROTECTED REQUEST ASCII)
Client -> APIM -> Function Host -> AuthorizationMiddleware
	 -> Extract JWT & build ClaimsPrincipal
	 -> ClaimsMappingService.Enrich
	 -> Policy/Permission evaluation
	 -> (Optional) LoadCustomerAccount (Cosmos)
	 -> Target Function Handler
	 -> Cosmos (CRUD) -> Response

ASSISTANT: SECURITY CONTROLS DIAGRAM

														+------------------------------+
														|   Identity & Access Layer    |
														|  - JWT (HMAC)                |
														|  - AAD Group -> Permission   |
														+---------------+--------------+
																						|
	+---------------------+    +--------------v--------------+    +---------------------------+
	| Secrets & Keys      |    |  Authorization Middleware   |    | Data Protection           |
	| - Key Vault (jwt)   |--> |  - Token extraction         |--> | - Cosmos DB (partitioned) |
	| - Future asymmetric |    |  - Claims enrichment        |    | - BCrypt password hashes  |
	+----------+----------+    |  - Policy & permission eval |    | - (Planned) hashed refresh|
						 |               +--------------+--------------+    +---------------------------+
						 |                                 |
						 v                                 v
			Monitoring & Logs                 Business Functions
			- App Insights                    - Admin / Customer APIs
			- Serilog console

Network Segmentation:
	Public APIM (Internet) vs Admin APIM (Internal VNet) -> same Function backend (note: backend still public; improvement needed for full isolation).

ASSISTANT: SECURITY CONTROL MAPPING
Authentication: JWT access tokens (HMAC signed with symmetric key). MFA enrollment scaffold (Totp) but no integration with auth flow yet.
Authorization: Custom middleware; permission claims (cs.permission) derived from external groups; policy registry for composite policies (AdminRead/AdminWrite).
Least Privilege & Segregation: Distinct policies for read vs write; admin surface separated by internal APIM (network layer). Function App identity (system-assigned) available for RBAC to Cosmos/Key Vault.
Credential & Secret Management: jwt-signing-key stored as Key Vault secret via Bicep. (App currently uses JWT_SIGNING_KEY config rather than fetching secret dynamically; improvement needed.)
Password Security: BCrypt hashing of user passwords; PasswordVersion field for potential migration.
Token Security: Access token short-lived (configurable, default 15m). Refresh tokens stored in-memory (non-persistent) – insecure for scale; Cosmos repository for refresh tokens exists but unused by TokenService yet.
Data Integrity & Partitioning: Cosmos uses tenantId partition key & unique key (tenantId, normalizedEmail). Upsert operations ensure idempotent updates.
Auditing: AuditMetadata captures creation/update metadata; Serilog/Application Insights for operational telemetry.
MFA: TOTP enrollment & verification endpoints; not bound to account Security state (missing persistence of secret & enable flag). Recovery codes fields exist but unused.
Observability: Application Insights worker telemetry plus structured logging potential via Serilog.
Infrastructure Security: Key Vault with purge protection & soft delete; Cosmos local auth disabled (RBAC assumed). Admin APIM can be internal with VNet.

ASSISTANT: IDENTIFIED GAPS / RISKS
1. JWT Validation: Middleware decodes JWT without validating signature, issuer, audience, expiration -> Allows forged tokens. Must add token validation (e.g., TokenValidationParameters + JwtSecurityTokenHandler.ValidateToken).
2. Key Vault Integration: TokenService attempts asymmetric path but throws NotImplementedException leading to fallback; actual Key Vault signing not implemented.
3. Refresh Tokens: In-memory dictionary loses tokens on restart; not bound to user ID; repository for persistent refresh tokens is unused.
4. MFA: Generated secret not stored; verification not tied to account; MfaEnabled flag never set; no enforcement in login flow.
5. Authorization Bypass Risk: Missing robust error handling for absent Authorization header handled, but no expiry check due to lack of validation.
6. Input Validation & Rate Limiting: Minimal validation; no throttling (APIM policies could mitigate). Potential brute-force on login.
7. Password Policies: No complexity / reuse / lockout logic (fields exist: FailedLoginAttempts, LockoutEndUtc but unused).
8. Sensitive Data Exposure: Update endpoints echo some fields; ensure PII minimization & consider Data Loss Prevention.
9. Logging: Potential to log sensitive data (currently minimal, but ensure no secrets in logs if expanded).
10. Network Isolation: Admin API shares publicly reachable Function backend; private endpoint or separate Function App recommended.
11. Secrets Retrieval: JWT_SIGNING_KEY__KV_SECRET_NAME set but code doesn\'t read secret; using plain config key instead.
12. TOTP Algorithm/Config Hardcoded: Only SHA1 & 30s step; consider supporting stronger algorithms or rate limiting verification attempts.

ASSISTANT: RECOMMENDATIONS (PRIORITIZED)
Critical:
	- Implement proper JWT signature & claims validation (issuer, audience, exp, nbf).
	- Persist refresh tokens using CosmosRefreshTokenRepository; bind to userId; store hashed token; implement rotation & revocation on refresh.
	- Store & encrypt TOTP secret (TotpSecretEncrypted) and enforce MFA during login when enabled.
	- Fetch JWT signing key from Key Vault at startup (or periodically) instead of static config; support key rotation via kid header.
High:
	- Implement account lockout & incremental backoff on failed login attempts.
	- Add APIM policies: rate limit, IP filtering (admin), JWT validate (if offloaded), request/response masking.
	- Separate admin Function App behind private endpoint or restrict public access.
Medium:
	- Add unit tests for AuthorizationMiddleware, Token expiry scenarios, MFA.
	- Implement structured audit events (who changed what) to App Insights.
	- Introduce password complexity & version migration path.
	- Add input validation library (FluentValidation) & consistent error model (RFC 7807).
Low:
	- Implement Key Vault based asymmetric signing (RSA) for non-repudiation & easier rotation.
	- Support multiple tenants (currently hardcoded tenantId).
	- Implement recovery code generation & secure storage (hashed) for MFA.

ASSISTANT: SUMMARY
The solution is a foundational customer identity/profile microservice with custom auth middleware and infrastructure scaffolding suited for Azure-native deployment. Security model groundwork exists (permissions, policies, MFA placeholders), but critical production hardening (token validation, refresh persistence, MFA enforcement, secret retrieval) is unfinished. Addressing the listed critical & high recommendations will materially improve security posture and reliability.



