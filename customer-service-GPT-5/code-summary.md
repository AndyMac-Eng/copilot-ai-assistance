# Code Summary

Generated: 2025-09-02

## High-Level Metrics

Total tracked lines (code + infra + docs): **2356**

| Extension | Files | Lines | Share |
|-----------|-------|-------|-------|
| .cs       | 15    | 1008  | 42.8% |
| .md       | 4     | 789   | 33.5% |
| .bicep    | 1     | 442   | 18.8% |
| .yml      | 1     | 87    | 3.7%  |
| .json     | 2     | 30    | 1.3%  |

> Excludes generated / build outputs (`bin`, `obj`, `.git`, `.vs`, `node_modules`).

## Top 10 Largest Files

| Rank | File | Lines | Notes |
|------|------|-------|-------|
| 1 | `infra/main.bicep` | 442 | Infra: dual APIM, VNet, Cosmos, Function App, Key Vault scaffold |
| 2 | `iteration-diagrams.md` |  (md) | Architecture & network diagrams (multiple Mermaid/ASCII) |
| 3 | `ai-asstance-chat.md` | (md) | Conversation / design log |
| 4 | `src/CustomerService/Services/AuthorizationAttributes.cs` | (cs) | Auth middleware + attributes + policies |
| 5 | `src/CustomerService/Functions/CustomerFunctions.cs` | (cs) | Public customer endpoints |
| 6 | `auth-flow.md` | (md) | Auth lifecycle & UpdateMe sequence diagram |
| 7 | `src/CustomerService/Services/TokenService.cs` | (cs) | JWT + refresh issuance (Key Vault placeholder) |
| 8 | `README.md` | (md) | Project overview |
| 9 | `.github/workflows/customer-service-ci.yml` | (yml) | CI pipeline |
|10 | `src/CustomerService/Services/Authorization.cs` | (cs) | Claims mapping + permission service |

_(Exact per-file line numbers for markdown not reprinted here to keep table concise. Full numeric output available via `./linecount.ps1`.)_

## Architecture Components (Code Perspective)

### Runtime
- Azure Functions .NET 8 isolated worker.
- Public customer APIs and internal admin APIs share process; access segregated via policies & (in infra) separate ingress.

### Domain Models
- `CustomerAccount` record (profile + security + preferences + audit) supporting immutable updates via `with`.
- `RefreshTokenRecord` for refresh token persistence (currently not fully wired for login refresh path).

### Authorization & Security
- Attribute-based authorization pipeline: `[RequireAuthentication]`, `[LoadCustomerAccount]`, `[AuthorizePolicy("AdminRead")]`, `[AuthorizePolicy("AdminWrite")]`.
- Middleware centralizes token parsing, claims enrichment (AAD group -> internal permission claims), permission/policy evaluation, and optional account loading.
- Policy registry allows extension without touching middleware core.

### Token Service
- Issues JWT access + refresh tokens; symmetric signing by default; scaffold for Key Vault RSA (currently fallback path). In-memory refresh token store (placeholder).

### Persistence
- Cosmos DB repositories for customers and refresh tokens (basic queries + upsert). Partition key = `tenantId` for multi-tenancy readiness.

### Infrastructure (Bicep)
- Defines: Function App, dual API Management (public + internal), VNet/subnet, Cosmos DB, Key Vault (scaffold), ACR (if present), diagnostic placeholders.

### Tests
- Authorization claim mapping & permission evaluation.
- Model immutability / preference updates.
- Token issuance basic structure.

### Documentation
- `iteration-diagrams.md` multi-layer diagrams (architecture, network, admin vs public).
- `auth-flow.md` generic & concrete authorization sequence diagrams.

## Current Strengths
- Clear separation of business logic from authorization concerns after middleware refactor.
- Immutable records encourage safe partial updates.
- Extensible policy registry avoids scattering conditional logic.
- Infrastructure template already parameterized for environment SKUs.

## Notable Gaps / Future Enhancements
| Area | Opportunity | Suggested Action |
|------|-------------|------------------|
| JWT Validation | Demo parsing only | Add signature/audience/issuer validation (APIM `<validate-jwt>` or Microsoft.IdentityModel validation in middleware). |
| Refresh Tokens | In-memory; not user-bound | Persist refresh tokens & bind to user/device; implement revoke path in logout. |
| Audit & Security Logs | Minimal | Emit structured events (auth successes/failures, admin data changes) to App Insights / Event Hub. |
| Secret Management | Symmetric key via config | Move to Key Vault RSA key + caching and kid header inclusion. |
| Admin Isolation | Same Function App | Consider separate Function App + private endpoint; restrict admin APIM to VNet. |
| Cosmos Networking | Public | Add private endpoint / firewall; restrict egress. |
| Testing | Limited integration coverage | Add end-to-end auth pipeline tests (function invocation with fake JWT). |
| Performance | Repeated JWT parsing | Consider caching parsed principal if multiple middleware stages added later. |

## Extension Guide
- Add a new permission: define constant + map AAD group in `Authorization:GroupMappings`; then decorate Functions with `[RequirePermissions]` or expand policy.
- Add policy: inject new lambda into `PermissionPolicyRegistry` (extension method) and use `[AuthorizePolicy("Name")]`.
- New self endpoint: Apply `[RequireAuthentication]` + any resource-specific attributes; avoid duplicating auth code.

## Tooling
- `linecount.ps1` reproducibly outputs the metrics above; rerun after substantive changes to track growth.

## Quick Metrics Commands
```powershell
pwsh ./linecount.ps1
```

---
_Auto-generated code summary; update after major structural or permission model changes._
