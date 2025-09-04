User request:
can you analyse all the code in this workspace and do the following:
- tell me what the purpose of this software is
- draw an architural diagram of the tech stack
- draw a diagram of program flow
- draw a diagram showing parts of the stack that address key information security concerns

Assistant response:
Purpose:
This codebase implements an Azure Functions isolated-process backend providing customer account management (registration, authentication with JWT + refresh tokens, profile self-service update, MFA enrollment/verification scaffold) and admin customer management endpoints, backed by Azure Cosmos DB for persistence and (optionally) Azure Key Vault for JWT signing keys. It includes a custom attribute/middleware-based authorization framework mapping external identity provider group claims to internal granular permission claims.

High-level capabilities:
- Public customer endpoints: create account, login (JWT issuance), refresh token placeholder, MFA enrollment & verification (demo), logout (stateless), get/update own profile.
- Admin endpoints (intended for private/internal network): read & update arbitrary customer accounts with policy-based access control.
- Security services: token issuance (Key Vault / symmetric fallback), claims enrichment from group membership, permission + policy evaluation, customer account auto-loading middleware.
- Data access layer: Cosmos DB repositories for customer accounts and refresh tokens (refresh token logic partly stubbed in main flow; repository implemented for future wiring).

Tech stack architectural diagram:

ASCII Architecture:
+---------------------------+            +------------------------------+
|        Client Apps        |            |   Admin Internal Clients     |
|  (Web/Mobile, uses HTTPS) |            |  (VPN/PE, Ops tooling)       |
+-------------+-------------+            +---------------+--------------+
              |  HTTPS (REST)                           |
              v                                         v
        +------------------------------ Azure Functions App ------------------------------+
        | (Isolated .NET 8 Worker)                                                         |
        |  - Program.cs: DI, Serilog, AI, Middleware registration                          |
        |  - HTTP Function Endpoints (CustomerFunctions, AdminFunctions)                   |
        |  - AuthorizationMiddleware (JWT parsing, claims enrich, policy/perm checks)      |
        |  - TokenService (JWT + in-memory refresh tokens)                                 |
        |  - ClaimsMappingService (group -> permission claims)                             |
        |  - AuthorizationService / PolicyRegistry                                         |
        +----------------------+---------------------------+-------------------------------+
                               |                           |
                               | Cosmos DB SDK             | Key Vault (optional RSA key)
                               v                           v
                   +----------------------+      +---------------------------+
                   |   Cosmos DB Account  |      | Azure Key Vault (Keys)    |
                   |  Containers:         |      | (Asymmetric signing)      |
                   |   - customers        |      +---------------------------+
                   |   - refreshTokens    |                ^
                   +----------------------+                |
                               ^                           |
                               | telemetry                 | credentials (Managed Identity)
                               |                           |
                               +-----------+   +-----------+
                                           v   v
                                      +-----------+
                                      |App Insights|
                                      +-----------+

Program flow (key requests):
1. CreateAccount:
   Client -> POST /customers -> CustomerFunctions.CreateAccount
   - Deserialize body, validate uniqueness via ICustomerRepository.GetByEmailAsync
   - Hash password (BCrypt) -> store account (CreateAsync)
   - Return 201 with basic account data.
2. Login:
   - Validate credentials (GetByEmailAsync + BCrypt.Verify)
   - TokenService.IssueTokens -> access JWT + refresh token recorded in-memory
   - Return tokens.
3. Authenticated call (e.g., GET /customers/me):
   - AuthorizationMiddleware runs before function:
     a. Extract Authorization header, parse JWT (JwtSecurityTokenHandler)
     b. Map external groups -> internal permission claims
     c. If [LoadCustomerAccount], fetch via GetByIdAsync, attach to context
     d. Evaluate any [RequireAuthentication]/[AuthorizePolicy]/[RequirePermissions] attributes
   - Function executes using enriched context (ctx.GetCustomerAccount()) and returns profile.
4. AdminGetCustomer (private ingress):
   - Same middleware; requires token carrying group mapped to AdminRead or AdminReadWrite (policy AdminRead)
   - Repository fetch and return.
5. UpdateMe / AdminUpdateCustomer:
   - Middleware auth & (for admin) policy pass
   - Deserialize patch request, apply immutable record with modifications, set audit metadata, Upsert in Cosmos.
6. EnrollMfa / VerifyMfa:
   - Demonstration only; generates TOTP secret (OtpNet), returns provisioning URI; verification checks code window.

Program flow diagram (simplified):
Client Request
  -> Azure Functions Frontend
    -> AuthorizationMiddleware?
        - Parse JWT? (if auth required)
        - Enrich Claims (group->permission)
        - Evaluate Permissions/Policies
        - Load CustomerAccount (if attribute)
        - On failure -> ShortCircuit (error JSON)
    -> Target Function Method
        - Validate & Deserialize
        - Repository Ops (Cosmos)
        - TokenService (for auth endpoints)
        - Return HttpResponseData (JSON)

ASCII Flow (GET /customers/me example):
[Client] --Authorization: Bearer--> [HTTP Trigger]
   -> [AuthorizationMiddleware]
       -> Parse JWT
       -> Enrich claims
       -> Policy/permission checks
       -> Load account (Cosmos) if attribute
       -> Pass
   -> [CustomerFunctions.GetMe]
       -> Read account from context
       -> Shape response
       -> Return 200 JSON

Security-focused diagram (controls mapping):
+---------------------------------------------------------------------------------------+
| Threat / Concern                | Control Component(s)                                |
|---------------------------------------------------------------------------------------|
| AuthN of user requests          | JWT Access Tokens (TokenService), AuthorizationMiddleware|
| Access token integrity          | HMAC SHA256 signing (symmetric) or Key Vault key (planned) |
| Privilege enforcement           | ClaimsMappingService, PermissionPolicyRegistry, AuthorizationService |
| Least privilege (admin vs user) | Policy names (AdminRead/AdminWrite), role/permission claims |
| Data confidentiality at rest    | Cosmos DB (encrypted by platform), Key Vault for keys        |
| Password security               | BCrypt hashing (PasswordHash)                               |
| MFA support (account takeover)  | TOTP enrollment & verification endpoints (demo scaffold)     |
| Replay / token misuse           | Short-lived access token + refresh token rotation (planned)  |
| Refresh token revocation        | IRefreshTokenRepository (implemented) future wiring          |
| Auditing / change tracking      | AuditMetadata fields, Serilog + App Insights telemetry       |
| Input validation                | Manual null/whitespace checks, limited field constraints     |
| DoS / brute force mitigation    | (Gap) - no rate limiting (Polly imported but unused)         |
| Secret management               | KEYVAULT_URL + Managed Identity (optional)                   |
| Secure config                   | Environment/AppSettings (JWT_SIGNING_KEY required)           |
+---------------------------------------------------------------------------------------+

ASCII Security Architecture:
            +-------------------+
            |  Client (JWT)     |
            +---------+---------+
                      |
                      v
        +-------------------------------+
        | AuthorizationMiddleware       |
        | - Parse JWT                   |
        | - Enrich Claims               |
        | - Eval Policies/Perms         |
        | - Load Account                |
        +---------------+---------------+
                        |
         +--------------+--------------+
         | Customer/Admin Functions    |
         +--------------+--------------+
                        |
        +---------------+---------------+
        |   Cosmos DB (Accounts/Tokens) |
        +---------------+---------------+
                        |
        +---------------+---------------+
        |   Key Vault (JWT Key) (opt)   |
        +---------------+---------------+

Key observations & gaps:
- Refresh token persistence not yet integrated into login/refresh flows (in-memory only) though repository exists.
- No JWT validation (signature, expiry) performed in middleware; it only parses token without verifying cryptographic integrity (should use TokenValidationParameters & ValidateToken).
- MFA not bound to account state (secret returned but not persisted encrypted in TotpSecretEncrypted field).
- Lack of rate limiting, account lockout logic not applied though fields exist.
- Missing logging on security failures (silent catches in TokenService Key Vault retrieval and middleware).

Recommendations (next steps):
1. Implement proper JWT validation (TokenValidationParameters, issuer/audience, signing key / Key Vault). 
2. Store refresh tokens hashed in Cosmos; wire TokenService to IRefreshTokenRepository instead of in-memory dictionary, binding user & tenant.
3. Persist MFA secret (encrypted) and flip MfaEnabled when verified; enforce MFA claim requirement for privileged actions.
4. Add rate limiting / incremental backoff (Polly or Front Door / APIM policies) and account lockout after N failed logins.
5. Add structured security logging (failed auth, policy denials) to App Insights.
6. Introduce data validation & length constraints to mitigate injection / size abuse.
7. Add unit tests for middleware logic (permissions, policy denial paths) and token refresh flow once implemented.
8. Consider secret rotation (signing key rotation with multiple KIDs) & key rollover support.
9. Encrypt at-rest MFA and recovery codes using Key Vault or Azure Managed HSM or DPAPI + KEK.
10. Add OpenAPI documentation for function endpoints for client integration.

Simplified DSL-like Component List:
- Entry: Program.cs (DI + Middleware)
- Functions: CustomerFunctions, AdminFunctions
- Security: AuthorizationMiddleware, ClaimsMappingService, AuthorizationService, PermissionPolicyRegistry
- Identity: TokenService (JWT issuance), BCrypt for password hashing, TOTP (OtpNet)
- Data: CosmosCustomerRepository, CosmosRefreshTokenRepository
- Models: CustomerAccount (+ Security, Preferences, Audit), RefreshTokenRecord
- Tests: Authorization, TokenService basic, Model immutability

End of analysis.
