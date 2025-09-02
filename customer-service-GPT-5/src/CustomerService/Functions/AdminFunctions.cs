using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CustomerService.Models;
using CustomerService.Services;
using CustomerService.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CustomerService.Functions;

// These endpoints are intended to be exposed ONLY via an internal ingress (e.g., separate Function App behind VPN / private endpoint)
public class AdminFunctions
{
    private readonly ICustomerRepository _repo;
    private readonly IClaimsMappingService _claimsMapper;
    private readonly IAuthorizationService _authz;

    public AdminFunctions(ICustomerRepository repo, IClaimsMappingService mapper, IAuthorizationService authz)
    {
        _repo = repo;
        _claimsMapper = mapper;
        _authz = authz;
    }

    [Function("AdminGetCustomer")] // GET /admin/customers/{id}
    public async Task<HttpResponseData> GetCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/customers/{id}")] HttpRequestData req, string id)
    {
        var principal = ExtractPrincipal(req);
        principal = _claimsMapper.Enrich(principal);
        if (!_authz.HasAnyPermission(principal, PermissionConstants.AdminReadOnly, PermissionConstants.AdminReadWrite))
            return await Problem(req, HttpStatusCode.Forbidden, "Missing permission");
        var account = await _repo.GetByIdAsync("default", id);
        if (account == null) return await Problem(req, HttpStatusCode.NotFound, "Not found");
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(account);
        return resp;
    }

    [Function("AdminUpdateCustomer")] // PATCH /admin/customers/{id}
    public async Task<HttpResponseData> UpdateCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "admin/customers/{id}")] HttpRequestData req, string id)
    {
        var principal = ExtractPrincipal(req);
        principal = _claimsMapper.Enrich(principal);
        if (!_authz.HasPermission(principal, PermissionConstants.AdminReadWrite))
            return await Problem(req, HttpStatusCode.Forbidden, "Missing write permission");
        var existing = await _repo.GetByIdAsync("default", id);
        if (existing == null) return await Problem(req, HttpStatusCode.NotFound, "Not found");
        var payload = await JsonSerializer.DeserializeAsync<AdminUpdateRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload == null) return await Problem(req, HttpStatusCode.BadRequest, "Invalid payload");
        var updated = existing with
        {
            DisplayName = payload.DisplayName ?? existing.DisplayName,
            DateOfBirth = payload.DateOfBirth ?? existing.DateOfBirth,
            ResidentialAddress = payload.ResidentialAddress ?? existing.ResidentialAddress,
            MobilePhone = payload.MobilePhone ?? existing.MobilePhone,
            Preferences = payload.ThemeMode == null ? existing.Preferences : existing.Preferences with { ThemeMode = payload.ThemeMode },
            Audit = existing.Audit with { UpdatedUtc = DateTimeOffset.UtcNow, UpdatedBy = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "admin" }
        };
        await _repo.UpdateAsync(updated);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(updated);
        return resp;
    }

    private static ClaimsPrincipal ExtractPrincipal(HttpRequestData req)
    {
        // In a production system you'd validate JWT signature/audience/issuer. Here we simply parse for demo.
        if (!req.Headers.TryGetValues("Authorization", out var authValues)) return new ClaimsPrincipal(new ClaimsIdentity());
        var token = authValues.First().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Last();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var claims = jwt.Claims.Select(c => new Claim(c.Type, c.Value));
        var id = new ClaimsIdentity(claims, "jwt");
        return new ClaimsPrincipal(id);
    }

    private static async Task<HttpResponseData> Problem(HttpRequestData req, HttpStatusCode status, string detail)
    {
        var resp = req.CreateResponse(status);
        await resp.WriteAsJsonAsync(new { error = detail });
        return resp;
    }

    private record AdminUpdateRequest(string? DisplayName, DateTime? DateOfBirth, string? ResidentialAddress, string? MobilePhone, string? ThemeMode);
}