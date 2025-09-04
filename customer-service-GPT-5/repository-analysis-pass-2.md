User Input:
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns
- determine what test coverage currently looks like

Assistant Output:
(analysis will append below)
\n+## Purpose of the Software
This repository implements an Azure Functions isolated-process backend providing customer account management, authentication (JWT access + refresh tokens), basic profile update operations, administrative read/write operations guarded by custom authorization attributes, and integration with Azure Cosmos DB for persistence. It aims to be a multi-tenant ready (simple tenant id field) customer identity/profile microservice with pluggable permission mapping (e.g., from Azure AD groups) and a security middleware pipeline handling authentication/authorization at the function level.

Key capabilities:
- Customer registration with hashed passwords (BCrypt).
- Customer login issuing access & refresh tokens (refresh persistence incomplete / partially in-memory).
- Token service supporting symmetric signing (with future Key Vault asymmetric signing placeholder).
- MFA enrollment & verification (TOTP) demo endpoints (not fully bound to persisted account yet).
- Customer self-service retrieval & partial update of profile data.
- Admin endpoints with policy-based authorization (read/write) via custom attributes.
- Cosmos DB repositories for customer accounts and refresh tokens (refresh tokens not yet fully wired into login/refresh flow; in-memory structure currently used).
- Extensible authorization via group-to-permission claim enrichment.

## Architectural Diagram (Tech Stack)
```
┌────────────────────────────────────────────────────────────────────┐
│                        Azure Environment                           │
│                                                                    │
│  ┌──────────────┐        ┌────────────────────────┐                │
│  │  Clients     │ HTTPS  │ Azure Functions (App)  │                │
│  │ (Web/Mobile) ├───────▶│  Isolated .NET 8 Host  │                │
│  └──────────────┘        │  Program.cs            │                │
│                          │  • Serilog Logging     │                │
│                          │  • App Insights        │                │
│                          │  • DI Container        │                │
│                          └──────────┬────────────┘                │
│                                      │ Middleware (Authorization)  │
│                                      ▼                            │
│                          ┌────────────────────────┐                │
│                          │  Function Endpoints    │                │
│                          │  • CustomerFunctions   │                │
│                          │  • AdminFunctions      │                │
│                          └──────────┬────────────┘                │
│                                      │ Calls                      │
│                   ┌──────────────────┼──────────────────┐         │
│                   │                  │                  │         │
│                   ▼                  ▼                  ▼         │
│        ┌─────────────────┐  ┌─────────────────┐  ┌────────────────────┐
│        │ TokenService    │  │ Authorization*  │  │ Cosmos Repositories │
│        │ (JWT, refresh)  │  │ (ClaimsMapping, │  │  • Customers        │
│        │                 │  │  Policies)      │  │  • RefreshTokens    │
│        └────────┬────────┘  └────────┬────────┘  └───────────┬────────┘
│                  │  Config + KeyVault │                     Config (Conn)│
│                  ▼                   ▼                                  │
│          ┌───────────────┐   ┌────────────────┐                        │
│          │ AppSettings & │   │ Azure Key Vault│ (optional JWT key)     │
│          │ Env Variables │   └────────────────┘                        │
│          └───────────────┘                                            │
└────────────────────────────────────────────────────────────────────┘
*Authorization includes custom attributes + middleware that evaluates JWT, maps group claims to internal permission claims, enforces policies, and optionally loads CustomerAccount into context.
```

## Program Flow Diagram (Selected Scenarios)
### 1. Create Account
```
Client -> POST /customers
	Body: { email, password, displayName }
		↳ CustomerFunctions.CreateAccount
				↳ ICustomerRepository.GetByEmailAsync
				↳ Hash password (BCrypt) -> new CustomerAccount
				↳ ICustomerRepository.CreateAsync
		<- 201 Created (id, email, displayName)
```
### 2. Login
```
Client -> POST /customers/login { email, password }
	↳ GetByEmailAsync
	↳ Verify BCrypt password
	↳ TokenService.IssueTokens
		↳ (Generate JWT, store refresh token in in-memory dictionary)
	<- 200 OK { access_token, refresh_token, expires_in }
```
### 3. Authenticated GetMe
```
Client -> GET /customers/me with Authorization: Bearer <JWT>
	↳ AuthorizationMiddleware
			Parse JWT -> ClaimsPrincipal
			ClaimsMappingService.Enrich (group->permission claims)
			LoadCustomerAccountAttribute triggers repository lookup by sub/tid
			Policy/permission checks (none specific besides RequireAuthentication, LoadCustomerAccount)
	↳ CustomerFunctions.GetMe (account from context)
	<- 200 OK (profile data)
```
### 4. Admin Update Customer
```
Admin Client -> PATCH /admin/customers/{id} (Bearer token)
	↳ AuthorizationMiddleware
			JWT parse + enrich
			AuthorizePolicy("AdminWrite") -> policy registry evaluates permission claims
	↳ AdminFunctions.UpdateCustomer
			GetByIdAsync -> modify record -> UpdateAsync
	<- 200 OK (updated account)
```
### 5. Refresh Token (CURRENT STATE)
```
Client -> POST /customers/refresh { refreshToken }
	↳ TokenService.TryValidateRefreshToken (in-memory dictionary)
	↳ Returns NotImplemented (does not yet issue new access token)
```

## Security Concerns & Mitigations Diagram
```
										[ Threats / Controls Mapping ]
┌──────────────────────────────────────────────────────────────┐
│ Authentication: JWT (HMAC-SHA256)                            │
│  • Threat: Token forgery  -> HMAC signing key / Key Vault    │
│  • Threat: Token replay   -> Short access token lifetime     │
├──────────────────────────────────────────────────────────────┤
│ Authorization Middleware                                     │
│  • Threat: Privilege escalation -> Least privilege policies  │
│  • Threat: Inconsistent checks  -> Centralized middleware    │
├──────────────────────────────────────────────────────────────┤
│ Claims Mapping (Group->Permission)                           │
│  • Threat: Improper role grant -> Explicit config mapping    │
├──────────────────────────────────────────────────────────────┤
│ Password Handling                                            │
│  • Threat: Credential theft -> BCrypt hashing                │
│  • Threat: Weak storage -> No plaintext storage              │
├──────────────────────────────────────────────────────────────┤
│ Data Persistence (Cosmos DB)                                 │
│  • Threat: Tenant data mix -> PartitionKey (tenantId)        │
│  • Threat: Injection -> Parameterized queries                │
├──────────────────────────────────────────────────────────────┤
│ TokenService Refresh Tokens                                  │
│  • Current Weakness: In-memory store (no persistence, no binding to user)   │
│  • Mitigation Needed: Use IRefreshTokenRepository + token hashing + rotation│
├──────────────────────────────────────────────────────────────┤
│ MFA (TOTP) (Demo Only)                                       │
│  • Threat: Shared secret leakage -> Should encrypt secret    │
│  • Current Gap: Secret not stored/linked to account          │
├──────────────────────────────────────────────────────────────┤
│ Observability                                                │
│  • Serilog + App Insights -> Audit, anomaly detection        │
└──────────────────────────────────────────────────────────────┘

### Security-Focused Component Diagram
```
Clients
	│  (HTTPS)
	▼
Azure Functions Host --(AuthorizationMiddleware)--> Enriched ClaimsPrincipal
	│                                         │
	│                                         ├─ ClaimsMappingService (group->permission)
	│                                         ├─ PermissionPolicyRegistry (AdminRead/AdminWrite)
	│                                         └─ ICustomerRepository (LoadCustomerAccount)
	│
	├─ CustomerFunctions / AdminFunctions
	│       └─ TokenService (JWT, refresh)
	│              ├─ (future) KeyVault CryptographyClient
	│              └─ Symmetric fallback key (app secret)
	│
	├─ CosmosCustomerRepository (account data)
	└─ CosmosRefreshTokenRepository (NOT fully utilized yet)
```

## Test Coverage Assessment
Executed tests: 3 test classes (CustomerAccountModelTests, AuthorizationTests, TokenServiceTests). All pass.

Observed coverage scope (qualitative):
- Models: CustomerAccount immutability & default property covered (partial model coverage).
- Authorization: Claims mapping + permission evaluation logic covered.
- TokenService: Basic token issuance path covered (symmetric signing path). No coverage of refresh validation, revocation, Key Vault path, or error handling branches.
- Functions (HTTP endpoints): No direct tests (CreateAccount, Login, Refresh, MFA endpoints, GetMe, UpdateMe, Admin endpoints) => zero coverage of request/response logic, validation errors, middleware integration, repository usage.
- Repositories (Cosmos): No tests; all data-access code untested (queries, error paths).
- AuthorizationMiddleware: No direct tests for attribute enforcement, policy evaluation, short-circuiting, loading accounts, error cases.

Estimated line coverage (rough qualitative estimate, since coverage file not produced): Likely < 25% of executable lines. Core business flows (endpoints + middleware + repositories) are untested.

Reasons coverage file missing: Collecting with `--collect:"XPlat Code Coverage"` requires the coverlet collector (added) plus running tests; however no `coverage.cobertura.xml` surfaced—environment or output path may require explicit run settings. Future action: add `Directory.Build.props` with `<CollectCoverage>true</CollectCoverage>` and `<CoverletOutputFormat>cobertura</CoverletOutputFormat>` or inspect TestResults subfolders (hidden). For now, proceed with qualitative assessment.

## Gaps & Recommendations
1. Implement full refresh token flow using `IRefreshTokenRepository`; hash refresh tokens (e.g., SHA256) before storing; bind to user & device; rotate on refresh.
2. Add tests for AuthorizationMiddleware covering:
	 - Missing token -> 401
	 - Invalid token -> 401
	 - Missing permissions/policy -> 403
	 - LoadCustomerAccount success & not found -> 404
3. Add endpoint integration tests (use Functions Worker test host or Azure Functions isolated testing harness) for CreateAccount/Login/UpdateMe/Admin scenarios.
4. Add repository tests using Cosmos DB emulator or abstract repository allowing in-memory stub to test logic branches.
5. Add negative tests for TokenService refresh validation & revoke logic.
6. Complete Key Vault signing path or remove placeholder NotImplementedException logic branch; add tests for fallback behavior.
7. Secure MFA: persist encrypted TOTP secret; integrate verify flow with actual user state; add tests for valid/invalid codes & timing windows.
8. Introduce static analysis / security scanning (e.g., GitHub Dependabot, CodeQL) and secrets scanning.
9. Observability: log structured security events (login success/failure, admin updates) with correlation IDs.
10. Add rate limiting / lockout enforcement for repeated failed logins to mitigate brute force attacks.

## Summary
The project is a customer identity/profile microservice on Azure Functions with custom authorization pipeline and Cosmos DB persistence. Security foundations (hashed passwords, JWT, permission claims) are present but refresh token & MFA flows are incomplete. Test coverage currently exercises only peripheral logic (models, basic token issuance, claim mapping) leaving core HTTP endpoints, middleware, and data access untested.

---
End of analysis.
