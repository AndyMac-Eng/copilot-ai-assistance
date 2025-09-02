using System.Net;
using System.Security.Claims;
using System.Text.Json;
using CustomerService.Models;
using CustomerService.Services; // for auth attributes
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
    [RequireAuthentication]
    [AuthorizePolicy("AdminRead")]
    public async Task<HttpResponseData> GetCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "admin/customers/{id}")] HttpRequestData req, string id, FunctionContext ctx)
    {
        var account = await _repo.GetByIdAsync("default", id);
        if (account == null) return await Problem(req, HttpStatusCode.NotFound, "Not found");
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(account);
        return resp;
    }

    [Function("AdminUpdateCustomer")] // PATCH /admin/customers/{id}
    [RequireAuthentication]
    [AuthorizePolicy("AdminWrite")]
    public async Task<HttpResponseData> UpdateCustomer([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "admin/customers/{id}")] HttpRequestData req, string id, FunctionContext ctx)
    {
        var principal = ctx.GetPrincipal();
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
            Audit = existing.Audit with { UpdatedUtc = DateTimeOffset.UtcNow, UpdatedBy = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "admin" }
        };
        await _repo.UpdateAsync(updated);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(updated);
        return resp;
    }

    // Principal extraction now handled by AuthorizationInvocationFilter

    private static async Task<HttpResponseData> Problem(HttpRequestData req, HttpStatusCode status, string detail)
    {
        var resp = req.CreateResponse(status);
        await resp.WriteAsJsonAsync(new { error = detail });
        return resp;
    }

    private record AdminUpdateRequest(string? DisplayName, DateTime? DateOfBirth, string? ResidentialAddress, string? MobilePhone, string? ThemeMode);
}