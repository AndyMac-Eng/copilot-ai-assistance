Repository Analysis Conversation Log
===================================

User Input:
```
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

As you produce output into this chat, keep a word-for-word record of my inputs and your outputs in a file called repository-analysis.md in the root of the workspace.

Ignore existing .md files - don't read their content as part of your analysis
```

Assistant Output (Part 1 - Analysis Pending)
```
Pending generation of purpose, architecture, flow, and security diagrams.
```

Assistant Output (Part 2 - Analysis Results)
```
1. Purpose of the Software
This project implements a lightweight Customer Account Service exposed as Azure Functions (HTTP-triggered) providing:
 - Customer self-service lifecycle: create account, login, refresh token (stub), logout, profile retrieval and partial update.
 - Administrative operations (read/update arbitrary customer accounts) guarded by a custom authorization layer and policy-based permissions.
 - JWT access token issuance with optional Azure Key Vault integration (fallback symmetric signing) plus in-memory refresh token tracking (prototype / not production ready).
 - Storage abstraction backed by Azure Cosmos DB for customer accounts and refresh tokens (the latter not yet fully wired into token refresh endpoints).
 - Extensible attribute-driven authorization middleware that maps external identity provider (e.g., Azure AD) group claims into internal fine-grained permission claims, plus policy registry for higher-level permission groupings.

Overall Intent: Provide a demonstrable reference / prototype for secure, multi-tenant-ready customer identity & profile management microservice using serverless architecture (Azure Functions) with layered authorization and Cosmos DB persistence.

2. Architectural Diagram (Tech Stack)
Legend: [SRV]=Service / [FUNC]=Azure Function / [KV]=Key Vault / [COS]=Cosmos DB / [AI]=App Insights

											+-----------------------------+
											| External Clients (Web/App)  |
											+--------------+--------------+
																		 |
															HTTPS / JSON (REST)
																		 |
													 Azure Functions Host
													(Isolated Worker .NET 8)
																		 |
				+----------------------------+-----------------------------+
				|                            |                             |
 [FUNC] Public Customer APIs   [FUNC] Admin APIs (internal)   Authorization Middleware
	 CreateAccount                   AdminGetCustomer            - Parses JWT
	 Login                           AdminUpdateCustomer         - Maps groups -> permissions
	 Refresh (stub)                                              - Evaluates policies/attributes
	 EnrollMfa (demo)                                            - Loads CustomerAccount
	 VerifyMfa (demo)
	 GetMe / UpdateMe
				|                            |                             |
				+----------------------------+-----------------------------+
																		 |
													 Application Services
								+--------------+-------------------+
								| TokenService | AuthZ/Claims Map  |
								+------+-------+---------+---------+
											 |                 |
								 (Issue JWT)       (Group->Permission)
											 |                 |
								Optional Azure Key Vault (asymmetric key) [KV]
								Fallback symmetric key from config
																		 |
													 Persistence Layer (Repositories)
											 +----------------+----------------+
											 | CosmosCustomer | CosmosRefresh  |
											 | Repository     | TokenRepo      |
											 +--------+-------+--------+-------+
																|                |
													 Azure Cosmos DB (Accounts / Refresh Tokens) [COS]
																		 |
													 Observability & Logging
												Serilog Console + App Insights [AI]

3. Program Flow Diagram (High-Level Request Lifecycle)

Example: Authenticated customer profile retrieval (GET /customers/me)

Client -> HTTP Request with Authorization: Bearer <JWT>
	-> Azure Functions Host routes to CustomerFunctions.GetMe
		-> AuthorizationMiddleware executes BEFORE function:
				 1. Resolve target method & attributes
				 2. Extract Authorization header & parse JWT (no signature validation here: prototype risk)
				 3. Enrich principal via ClaimsMappingService (group -> permission claims)
				 4. Evaluate: RequireAuthenticationAttribute (implicit due to attributes)
				 5. Evaluate any permissions / policies (none for GetMe)
				 6. LoadCustomerAccountAttribute triggers repository fetch by (tenantId, sub)
				 7. Inject principal & CustomerAccount into FunctionContext
		-> Function executes using loaded account
		-> Build response JSON
	-> Response returned to client

Sequence (ASCII UML style):

Client -> FunctionsHost : HTTP GET /customers/me
FunctionsHost -> AuthorizationMiddleware : Invoke
AuthorizationMiddleware -> JWT Handler : Read token
AuthorizationMiddleware -> ClaimsMappingService : Enrich(principal)
AuthorizationMiddleware -> CosmosCustomerRepository : GetByIdAsync(tenantId, sub)
CosmosCustomerRepository --> AuthorizationMiddleware : CustomerAccount
AuthorizationMiddleware --> FunctionsHost : continue
FunctionsHost -> CustomerFunctions.GetMe : Invoke
CustomerFunctions.GetMe --> FunctionsHost : HttpResponseData
FunctionsHost --> Client : 200 OK JSON

4. Security Concern Coverage Diagram & Mapping

Key Security Domains vs Implementation Elements:

	 +----------------------+----------------------------------------------+
	 | Concern              | Implemented / Location                       |
	 +----------------------+----------------------------------------------+
	 | Authentication       | JWT (TokenService) - BUT no runtime          |
	 |                      | signature/expiry validation in middleware.   |
	 <!-- | Authorization        | Attribute-driven middleware + policy registry| -->
	 | Least Privilege      | Fine-grained permission claims (cs.permission)|
	 | Secrets Management   | Optional Azure Key Vault for signing key     |
	 | Data Protection      | Passwords hashed with BCrypt; MFA TOTP demo  |
	 | Token Revocation     | In-memory refresh token store (prototype)    |
	 | Auditing             | AuditMetadata fields on CustomerAccount      |
	 | Multi-Tenancy        | tenantId partitioning in Cosmos DB           |
	 | Input Validation     | Basic null/format checks in endpoints        |
	 | Encryption at Rest   | Delegated to Cosmos DB & Key Vault (platform)|
	 | Observability        | Serilog + App Insights instrumentation        |
	 | MFA Readiness        | TOTP enroll/verify demo (not persisted)      |

Security-Focused ASCII Diagram:

										+-----------------+           +-------------------+
Client JWT/Req ---> | Authorization   |--perm-->  | Policy Registry    |
										| Middleware      |           +-------------------+
										|  - Parse JWT    |                 ^
										|  - Map groups   |                 |
										|  - Enforce attr |                 |
										+--------+--------+                 |
														 | Load Account             |
														 v                          |
												+---------+                     |
												| Cosmos  |<--partition (tenant)-+
												+----+----+
														 ^
					Password Hash (BCrypt) |  Refresh Tokens (prototype memory)
														 |
	+------------------+  Issue JWT (sign)   +-------------------+
	| TokenService     +-------------------->| Key Vault (opt)   |
	| (fallback HMAC)  |                     +-------------------+
	+------------------+

5. Notable Gaps / Recommendations
 - JWT Validation: Middleware only decodes token; add signature, issuer, audience, exp/nbf validation using TokenValidationParameters.
 - Refresh Flow: Current /customers/refresh endpoint returns NotImplemented and in-memory store loses state on restart. Integrate CosmosRefreshTokenRepository and bind refresh tokens to user & tenant.
 - MFA Persistence: Enrolled TOTP secret not stored; implement encryption (e.g., Azure Key Vault / local DP API) and bind to CustomerAccount.Security.TotpSecretEncrypted.
 - Rate Limiting & Brute Force: Add throttling for login & MFA verify endpoints (e.g., Azure API Management policies or middleware w/ distributed counter).
 - Password Policies & Rotation: Introduce complexity/version upgrades and rehash when PasswordVersion differs.
 - Logging Security: Add structured logging & exclude sensitive data (never log passwords / secrets). Presently safe but minimal.
 - Data Validation: Add stronger schema validation (e.g., FluentValidation) and PII normalization.
 - Secret Configuration: Ensure JWT_SIGNING_KEY length >= 256 bits for HMAC; enforce via startup validation.
 - Key Vault Integration: Complete RSA signing path (replace NotImplementedException branch) or remove until fully supported.
 - Account Lockout: Implement based on FailedLoginAttempts & LockoutEndUtc fields.

6. Summary
The repository is a prototype customer identity/profile microservice leveraging Azure Functions, Cosmos DB, and a custom authorization layer. It demonstrates secure password handling and extensible permission mapping but requires production hardening around JWT validation, refresh token persistence, MFA storage, rate limiting, and audit completeness.
```

User Input:
```
can you also do an analysis of current test coverage and put your findings in the same markdown file?
```

Assistant Output (Part 3 - Test Coverage Analysis)
```
Scope of Existing Tests
Test Project: tests/CustomerService.Tests
Frameworks: xUnit + FluentAssertions (.NET 8)
Test Files:
 - AuthorizationTests.cs (3 tests)
 - CustomerAccountModelTests.cs (2 tests)
 - TokenServiceTests.cs (1 test)

What Is Covered (Logical Areas)
1. Permission Mapping (ClaimsMappingService)
	- Positive case: group maps to read/write permission.
	- Negative case: absence of mapping yields no permission claims.
2. AuthorizationService logic
	- HasPermission true/false and HasAnyPermission scenario.
3. Domain Model Immutability & Defaults (CustomerAccount)
	- Default theme mode value.
	- with-expression immutability behavior.
4. TokenService basic issuance
	- Generates structurally valid JWT and refresh token.

What Is Not Covered (Significant Gaps)
Authentication & Authorization Pipeline
 - AuthorizationMiddleware end-to-end behavior (attribute detection, missing token, invalid token, policy evaluation, LoadCustomerAccount) is untested.
 - Policy registry (PermissionPolicyRegistry) default policies not explicitly tested.

CustomerFunctions / AdminFunctions
 - No HTTP-trigger function tests (CreateAccount, Login, GetMe, UpdateMe, Admin* endpoints, MFA flows). Input validation branches, error paths, and JSON shaping unverified.

Security-Critical Behavior
 - Password hashing & verification round-trip (BCrypt.Verify) not asserted (only implicit via login flow which is untested).
 - Refresh token validation & consumption (TokenService.TryValidateRefreshToken) not tested (including consume=true path and expiration removal).
 - Revocation logic (RevokeAllForUser) not tested.
 - MFA TOTP enrollment and verification (Totp code window acceptance) lacks tests.
 - Cosmos repositories (CosmosCustomerRepository, CosmosRefreshTokenRepository) have no tests (could be covered with integration tests or mocked container interfaces).

Data Integrity & Edge Cases
 - Multi-tenant partition key logic not tested (tenantId variations).
 - Update flows for partial null vs non-null fields (UpdateMe / AdminUpdate) not validated.
 - Error responses (Problem helper) formatting consistency untested.

Resilience & Configuration
 - Behavior when Key Vault integration partially configured / retrieval fails (fallback path) untested.
 - Missing or invalid configuration keys (e.g., JWT_SIGNING_KEY absent) not tested.

Observability
 - No assertions around logging (e.g., account creation log message) or correlation.

Qualitative Coverage Estimate (High-Level)
 - Core business logic lines (TokenService, AuthorizationMiddleware, Functions, Cosmos repos) mostly untested.
 - Existing tests focus on small, deterministic logic units (~3 low-complexity classes).
 - Estimated line coverage likely < 15% (no instrumentation run, qualitative only) because most executable code resides in functions and middleware.

Recommended Next Test Additions (Prioritized)
1. AuthorizationMiddleware Unit Tests
	- Missing Authorization header -> 401
	- Invalid JWT format -> 401
	- Policy failure -> 403
	- LoadCustomerAccount success vs account missing -> 404
2. TokenService Expanded Tests
	- TryValidateRefreshToken true/false, consume flag removes token.
	- Expired refresh token removal.
	- RevokeAllForUser clears dictionary.
3. CustomerFunctions Tests (use FunctionContext mocks or Azure Functions test host)
	- CreateAccount success & duplicate email conflict.
	- Login success & invalid password.
	- UpdateMe: themeMode validation error, partial update.
4. AdminFunctions Tests
	- AdminGetCustomer requires permission; missing permission returns 403.
	- AdminUpdateCustomer updates audit metadata.
5. MFA Tests
	- VerifyMfa accepts valid current code and rejects incorrect / outside window.
6. Repository Tests (Option A: Mock Cosmos Container)
	- GetByEmailAsync returns null vs match.
	- UpdateAsync upserts; error handling for not found on GetByIdAsync returns null.
7. Configuration Edge Cases
	- TokenService constructor throws when JWT_SIGNING_KEY missing.
	- Fallback path when Key Vault key retrieval fails (simulate by providing KV env values but no actual vault).

Suggested Tooling Enhancements
 - Add Coverlet for code coverage: <PackageReference Include="coverlet.collector" Version="*" /> and run `dotnet test /p:CollectCoverage=true`.
 - Consider using FluentAssertions extensions for JWT claim presence once signature validation logic added.
 - Introduce test doubles for Cosmos (e.g., custom in-memory repository implementing interfaces) to isolate middleware and function tests.

Test Debt Risk Assessment
 - Authorization & security-critical logic unverified: Medium-to-High risk of regressions or unnoticed vulnerabilities.
 - Absence of integration tests means contract (HTTP shape) can drift without detection.

Incremental Coverage Plan
Phase 1 (Foundational Security): Middleware, TokenService refresh logic, CreateAccount/Login.
Phase 2 (Admin & Update flows): Admin functions, UpdateMe edge cases, policy registry.
Phase 3 (Persistence & MFA): Repository abstraction tests, TOTP verify, negative windows.
Phase 4 (Resilience): Key Vault fallback, configuration error handling.

Minimal First Additions (Concrete Examples)
 - Test: Missing Authorization header returns 401 for GetMe (using simulated context + middleware).
 - Test: TokenService.TryValidateRefreshToken removes token when consume=true.
 - Test: CreateAccount duplicate email returns 409 (mock repo returning existing account).

Summary
Current tests validate only a narrow subset (permissions mapping, simple model defaults, basic token issuance). Significant security and functional paths lack coverage. Adding structured tests around middleware, token lifecycle, and function endpoints will substantially raise assurance with modest effort.
```


