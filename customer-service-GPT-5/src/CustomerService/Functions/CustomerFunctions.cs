using System.Net;
using System.Text.Json;
using BCrypt.Net;
using CustomerService.Models;
using CustomerService.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IdentityModel.Tokens.Jwt;
using OtpNet;
using System.Security.Cryptography;

namespace CustomerService.Functions;

public class CustomerFunctions
{
    private readonly ICustomerRepository _repo;
    private readonly TokenService _tokens;

    public CustomerFunctions(ICustomerRepository repo, TokenService tokens)
    {
        _repo = repo;
        _tokens = tokens;
    }

    [Function("CreateAccount")] // POST /api/customers
    public async Task<HttpResponseData> CreateAccount([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req, FunctionContext ctx)
    {
        var logger = ctx.GetLogger("CreateAccount");
        var body = await JsonSerializer.DeserializeAsync<CreateAccountRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            return await Problem(req, HttpStatusCode.BadRequest, "Invalid payload");
        }
        var existing = await _repo.GetByEmailAsync("default", body.Email);
        if (existing != null)
        {
            return await Problem(req, HttpStatusCode.Conflict, "Account already exists");
        }
        var account = new CustomerAccount
        {
            Email = body.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(body.Password),
            DisplayName = body.DisplayName,
            Roles = new[] { "customer" }
        };
        await _repo.CreateAsync(account);
        logger.LogInformation("Created customer {Email}", body.Email);
        var resp = req.CreateResponse(HttpStatusCode.Created);
        await resp.WriteAsJsonAsync(new { account.Id, account.Email, account.DisplayName });
        return resp;
    }

    [Function("Login")] // POST /api/customers/login
    public async Task<HttpResponseData> Login([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/login")] HttpRequestData req)
    {
        var body = await JsonSerializer.DeserializeAsync<LoginRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            return await Problem(req, HttpStatusCode.BadRequest, "Invalid payload");
        var account = await _repo.GetByEmailAsync("default", body.Email);
        if (account == null || !BCrypt.Net.BCrypt.Verify(body.Password, account.PasswordHash))
            return await Problem(req, HttpStatusCode.Unauthorized, "Invalid credentials");
        var tokens = _tokens.IssueTokens(account.TenantId, account.Id, account.Email, account.Roles);
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { access_token = tokens.accessToken, refresh_token = tokens.refreshToken, token_type = "Bearer", expires_in = (int)(tokens.accessExpires - DateTimeOffset.UtcNow).TotalSeconds });
        return resp;
    }

    [Function("RefreshToken")] // POST /api/customers/refresh
    public async Task<HttpResponseData> Refresh([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/refresh")] HttpRequestData req)
    {
        var payload = await JsonSerializer.DeserializeAsync<RefreshRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (payload?.RefreshToken is null) return await Problem(req, HttpStatusCode.BadRequest, "Missing refreshToken");
        if (!_tokens.TryValidateRefreshToken(payload.RefreshToken, consume: true, out _))
            return await Problem(req, HttpStatusCode.Unauthorized, "Invalid refresh token");
        // In production we would bind refresh token to user; here omitted.
        return await Problem(req, HttpStatusCode.NotImplemented, "Bind refresh tokens to user in persistent store");
    }

    [Function("EnrollMfa")] // POST /api/customers/mfa/enroll
    public async Task<HttpResponseData> EnrollMfa([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/mfa/enroll")] HttpRequestData req)
    {
        // For brevity, we don't authenticate user; production: require valid JWT
        var secret = KeyGeneration.GenerateRandomKey(20);
        var base32 = Base32Encoding.ToString(secret);
        var issuer = "CustomerService";
        var email = "user@example.com"; // placeholder
    var totp = new Totp(secret);
    var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(email)}?secret={base32}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { secret = base32, otpauth_uri = uri });
        return resp;
    }

    [Function("VerifyMfa")] // POST /api/customers/mfa/verify
    public async Task<HttpResponseData> VerifyMfa([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/mfa/verify")] HttpRequestData req)
    {
        var body = await JsonSerializer.DeserializeAsync<MfaVerifyRequest>(req.Body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (body is null || string.IsNullOrWhiteSpace(body.Secret) || string.IsNullOrWhiteSpace(body.Code))
            return await Problem(req, HttpStatusCode.BadRequest, "Invalid payload");
        var secretBytes = Base32Encoding.ToBytes(body.Secret);
        var totp = new Totp(secretBytes);
        if (!totp.VerifyTotp(body.Code, out _, new VerificationWindow(previous: 1, future: 1)))
            return await Problem(req, HttpStatusCode.Unauthorized, "Invalid code");
        var resp = req.CreateResponse(HttpStatusCode.NoContent);
        return resp;
    }

    [Function("Logout")] // POST /api/customers/logout
    public async Task<HttpResponseData> Logout([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/logout")] HttpRequestData req)
    {
        // For demo simply succeed (stateless JWT). Real impl: revoke refresh token(s) for user.
        var resp = req.CreateResponse(HttpStatusCode.NoContent);
        return await Task.FromResult(resp);
    }

    [Function("GetMe")] // GET /api/customers/me
    public async Task<HttpResponseData> GetMe([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/me")] HttpRequestData req)
    {
        // In production, validate JWT (API Mgmt or Function middleware). Here we parse Authorization header for brevity.
        if (!req.Headers.TryGetValues("Authorization", out var authHeaders))
            return await Problem(req, HttpStatusCode.Unauthorized, "Missing token");
        var token = authHeaders.First().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Last();
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var sub = jwt.Subject;
        var tenantId = jwt.Claims.FirstOrDefault(c => c.Type == "tid")?.Value ?? "default";
        var account = await _repo.GetByIdAsync(tenantId, sub);
        if (account == null) return await Problem(req, HttpStatusCode.NotFound, "Not found");
        var resp = req.CreateResponse(HttpStatusCode.OK);
        await resp.WriteAsJsonAsync(new { account.Id, account.Email, account.DisplayName, account.CreatedUtc, account.LastLoginUtc });
        return resp;
    }

    private static async Task<HttpResponseData> Problem(HttpRequestData req, HttpStatusCode status, string detail)
    {
        var resp = req.CreateResponse(status);
        await resp.WriteAsJsonAsync(new { error = detail });
        return resp;
    }

    private record CreateAccountRequest(string Email, string Password, string? DisplayName);
    private record LoginRequest(string Email, string Password);
    private record RefreshRequest(string RefreshToken);
    private record MfaVerifyRequest(string Secret, string Code);
}
