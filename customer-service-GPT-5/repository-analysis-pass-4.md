User Input:
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

Assistant Output:

## 1. Purpose of the Software
This repository implements a lightweight Customer Account microservice using Azure Functions (.NET 8 isolated worker) that provides:
- Customer self-service endpoints (account creation, login, profile retrieval/update, MFA enrollment/verification, logout, refresh token placeholder).
- Administrative endpoints for internal operators (get/update arbitrary customer accounts) gated by a custom authorization layer built on AAD group -> internal permission claim mapping.
- JWT access token issuance with optional Azure Key Vault integration (fallback to symmetric key) plus a simple in-memory refresh token mechanism (placeholder for production persistence).
- Cosmos DB persistence for customer accounts and refresh tokens (though refresh token storage service isn't yet wired into the auth flows—currently refresh tokens stored in-memory inside TokenService; TODO to integrate IRefreshTokenRepository for durability and revocation).

Strategic intent: Provide a foundation for a secure, multi-tenant-ready customer identity/profile subsystem with extensible authorization policies and auditable profile management, suitable to sit behind an API gateway or APIM instance.

## 2. Architectural Diagram (Tech Stack)
ASCII high-level components and their relationships.

```
			 +----------------------------+
			 |      External Clients      |
			 |  (Web / Mobile / Admin UI) |
			 +--------------+-------------+
						 |
						 | HTTPS (JWT Bearer)
						 v
			    +-------------------------+
			    |  Azure API Management   |  (policies, throttling) [implied by infra/apim]
			    +------------+------------+
						  |
						  v
			    +-------------------------+
			    |  Azure Functions App    |  (.NET 8 isolated)
			    |  (CustomerService)      |
			    +-----------+-------------+
						 |
	 +------------------------+---------------------------+
	 |                        |                           |
	 v                        v                           v
 +-----------+        +----------------+          +-----------------+
 |Auth Layer |        | Token Service  |          |  Repositories   |
 |Middleware |        | (JWT Issue/    |          | (Cosmos DB)     |
 |Attributes |        |  Refresh Mgmt) |          |  - Customers    |
 +-----------+        +----------------+          |  - RefreshTokens|
	 |                        |                  +--------+--------+
	 |                        |                           |
	 |                        +---------------------------+
	 |                                (User & token data)
	 |
	 v
 +------------------+
 | Claims Mapping   | (AAD groups -> internal permission claims)
 +------------------+
	 |
	 v
 +------------------+        +----------------------+        +------------------+
 |Policies Registry | -----> | AuthorizationService | <----- |  Principal (JWT) |
 +------------------+        +----------------------+        +------------------+

   Observability: Serilog Console + Application Insights Telemetry
   Secrets/Keys: Azure Key Vault (optional asymmetric key) else symmetric key from config
```

## 3. Program Flow Diagram (Typical Customer Self-Service)

Scenario: Create account -> Login -> Authenticated GetMe -> UpdateMe

```
Client                Functions Runtime / Service                 Cosmos DB              TokenService
  | POST /customers          |                                           |                    |
  |------------------------->| CreateAccount()                           |                    |
  |                          |  Validate payload                        |                    |
  |                          |  _repo.GetByEmailAsync() --------------->| (query)            |
  |                          |  (none)                                  |<-------------------|
  |                          |  Hash password (BCrypt)                  |                    |
  |                          |  _repo.CreateAsync() ------------------->| (insert)           |
  |                          |<-----------------------------------------|                    |
  |<-------------------------| 201 + account subset                     |                    |
  |                                                                                          |
  | POST /customers/login    |                                           |                    |
  |------------------------->| Login()                                   |                    |
  |                          | _repo.GetByEmailAsync() ---------------->| (query)            |
  |                          |<-----------------------------------------| account            |
  |                          | Verify password (BCrypt)                 |                    |
  |                          | tokens = IssueTokens() -------------------------------------->|
  |                          |<----------------------------------------- access+refresh      |
  |<-------------------------| 200 {access_token,refresh_token}          |                    |
  |                                                                                          |
  | GET /customers/me        |                                           |                    |
  | Authorization: Bearer    | AuthorizationMiddleware:                  |                    |
  | access_token             |  - Extract token                          |                    |
  |------------------------->|  - Build principal                        |                    |
  |                          |  - ClaimsMapping.Enrich(principal)        |                    |
  |                          |  - LoadCustomerAccount:                   |                    |
  |                          |    _repo.GetByIdAsync() ---------------->| (point read)       |
  |                          |<-----------------------------------------| account            |
  |                          | GetMe() returns filtered projection       |                    |
  |<-------------------------| 200 customer profile                      |                    |
  |                                                                                          |
  | PATCH /customers/me      | AuthorizationMiddleware path (as above)   |                    |
  |------------------------->| UpdateMe()                                |                    |
  |                          | _repo.UpdateAsync() -------------------->| (upsert)           |
  |                          |<-----------------------------------------|                    |
  |<-------------------------| 200 updated projection                    |                    |
```

Admin Flow (GetCustomer) adds policy evaluation chain: JWT -> Enrich -> PolicyRegistry(AdminRead/AdminWrite) -> Repo.

## 4. Security Concerns & Coverage Diagram

Security Domains addressed:
- Authentication: JWT bearer tokens validated syntactically (no signature validation middleware implemented yet; improvement required). Access tokens include sub, email, tenant id, jti.
- Authorization: Custom attribute/middleware system with permission claims + policy registry + group-to-permission enrichment.
- Secrets Management: Optional integration with Azure Key Vault for asymmetric signing (currently falls back due to NotImplementedException placeholder). Symmetric key from configuration required (JWT_SIGNING_KEY).
- Data Protection: Passwords hashed with BCrypt; TOTP secret generation for MFA enrollment (not yet persisted/encrypted—placeholder fields exist for encrypted storage). Refresh tokens hashed field exists in model but not yet used in flow.
- Least Privilege & Separation: Admin endpoints behind explicit policies; internal vs public route separation under /admin.
- Auditing: AuditMetadata on CustomerAccount plus Serilog logging of account creation; Application Insights telemetry wired.
- Multi-Tenant Readiness: tenantId partition key in Cosmos queries prevents cross-tenant data leakage if enforced by token claim tid.

ASCII overlay focusing on security controls:

```
		   +-------------------- Security Boundary (API) --------------------+
		   |                                                                 |
External --> |  AuthorizationMiddleware (AuthN/AuthZ Gate)                     | --> Functions Methods
Clients      |   - Parse JWT (TODO: signature validation)                      |
 (Bearer)    |   - ClaimsMapping (AAD groups -> internal permission claims)    |
		   |   - Policy & Permission evaluation                              |
		   |   - Optional LoadCustomerAccount (ownership check via sub/tid)  |
		   +-----------------------------+-----------------------------------+
								   |
								   v
						    +------------------+
						    | Repositories     |  (Cosmos DB)
						    |  - CustomerAccount (partitioned by tenantId)
						    |  - RefreshTokenRecord (revocation & rotation potential)
						    +------------------+
								   ^
								   |
			   +----------------+       |        +-------------------------+
			   | TokenService   |--------------- | Key Vault (optional key)|
			   |  - Issues JWT  |  (signing)     +-------------------------+
			   |  - In-memory   |
			   |    refresh map | (TODO: move to durable repo w/ hashing)
			   +----------------+
								   |
								   v
						+---------------------------+
						| BCrypt Password Hashing   | (Account creation & login)
						+---------------------------+
```

Control Gaps / Recommendations:
1. JWT Signature & Lifetime Validation: Middleware currently only parses token (ReadJwtToken) without validating signature, issuer, audience, exp. Add full validation via TokenValidationParameters + JwtSecurityTokenHandler.ValidateToken.
2. Refresh Token Persistence: Wire TokenService to IRefreshTokenRepository with secure hashing (e.g., SHA-256) and rotation on use. Bind refresh tokens to user & tenant.
3. MFA Persistence: Store encrypted TOTP secret per user (using protected secret storage / Key Vault) and mark MfaEnabled after verification.
4. Key Vault Integration: Implement proper asymmetric signing path using CryptographyClient.SignData or load an exportable key to avoid NotImplementedException fallback.
5. Logging & PII: Scrub sensitive fields (email, IP) or apply structured logging with data classification. Add request correlation IDs.
6. Brute Force Protection: Track failed logins (FailedLoginAttempts) and enforce lockouts using Security.LockoutEndUtc.
7. Input Validation: Strengthen payload validation (password policy, email format, size limits, address length) to reduce risk of injection or data abuse.
8. Threat Modeling: Add checks for tenantId claim tampering—validate that tenantId in token is authorized for principal.
9. Secure Headers / API Gateway: Ensure APIM or front door adds TLS enforcement, rate limiting, DDoS protection.
10. Secrets Management: Ensure JWT_SIGNING_KEY not committed; rotate keys periodically; implement key rollover (kid header already placeholder-ready).

## 5. Summary
The service is a foundational customer identity/profile API with custom authorization and Cosmos persistence. Key security primitives exist (hashing, claims-based policies), while token validation robustness, refresh token durability, MFA storage, and full cryptographic key usage require further enhancement for production readiness.

---
Transcript continues below with any further user queries and assistant responses appended verbatim.
