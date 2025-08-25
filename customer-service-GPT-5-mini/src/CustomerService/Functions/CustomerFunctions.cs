using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using CustomerService.Models;
using CustomerService.Services;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Functions
{
    public class CustomerFunctions
    {
        private readonly ICustomerRepository _repo;
        private readonly IConfiguration _config;

        public CustomerFunctions(ICustomerRepository repo, IConfiguration config)
        {
            _repo = repo;
            _config = config;
        }

        [Function("Register")]
        public async Task<HttpResponseData> Register([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/register")] HttpRequestData req)
        {
            var body = await JsonSerializer.DeserializeAsync<Customer>(req.Body);
            if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.PasswordHash))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid payload");
                return bad;
            }

            // generate salt and hash from provided PasswordHash field (we expect plain password here)
            var password = body.PasswordHash;
            var salt = RandomNumberGenerator.GetBytes(32);
            using var derive = new Rfc2898DeriveBytes(password, salt, 100_000, HashAlgorithmName.SHA256);
            var hash = derive.GetBytes(32);

            body.PasswordSalt = Convert.ToBase64String(salt);
            body.PasswordHash = Convert.ToBase64String(hash);

            await _repo.CreateAsync(body);

            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteStringAsync("Created");
            return res;
        }

        [Function("Login")]
        public async Task<HttpResponseData> Login([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/login")] HttpRequestData req)
        {
            var doc = await JsonSerializer.DeserializeAsync<JsonElement>(req.Body);
            if (!doc.TryGetProperty("email", out var em) || !doc.TryGetProperty("password", out var pw))
            {
                var bad = req.CreateResponse(HttpStatusCode.BadRequest);
                await bad.WriteStringAsync("Invalid payload");
                return bad;
            }
            var email = em.GetString()!;
            var password = pw.GetString()!;

            var valid = await _repo.ValidateCredentialsAsync(email, password);
            if (!valid)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid credentials");
                return unauthorized;
            }

            var user = await _repo.GetByEmailAsync(email);
            if (user == null)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid credentials");
                return unauthorized;
            }

            // create JWT token (symmetric signing for prototype; recommend Azure AD or Managed Identity in production)
            var token = JwtHelpers.GenerateToken(user.Id, _config["Jwt:Key"] ?? "dev-key-please-change", _config["Jwt:Issuer"] ?? "customer-service", 60);

            await _repo.UpdateLastLoginAsync(user.Id);

            var res = req.CreateResponse(HttpStatusCode.OK);
            res.Headers.Add("Content-Type", "application/json");
            await res.WriteStringAsync(JsonSerializer.Serialize(new { token }));
            return res;
        }

        [Function("Logout")]
        public async Task<HttpResponseData> Logout([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/logout")] HttpRequestData req)
        {
            // For JWT stateless tokens, logout typically done client-side or by token revocation list. Prototype: accept token and respond OK.
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteStringAsync("Logged out");
            return res;
        }

        [Function("GetProfile")]
        public async Task<HttpResponseData> GetProfile([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/me")] HttpRequestData req)
        {
            // Expect Authorization: Bearer <token>
            if (!req.Headers.TryGetValues("Authorization", out var vals))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Missing Authorization");
                return unauthorized;
            }
            var auth = System.Linq.Enumerable.FirstOrDefault(vals);
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer "))
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid Authorization header");
                return unauthorized;
            }

            var token = auth.Substring("Bearer ".Length);
            var principal = JwtHelpers.ValidateToken(token, _config["Jwt:Key"] ?? "dev-key-please-change", _config["Jwt:Issuer"] ?? "customer-service");
            if (!principal.Valid)
            {
                var unauthorized = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorized.WriteStringAsync("Invalid token");
                return unauthorized;
            }

            var userId = principal.UserId;
            var user = await _repo.GetByIdAsync(userId);
            if (user == null)
            {
                var notfound = req.CreateResponse(HttpStatusCode.NotFound);
                await notfound.WriteStringAsync("User not found");
                return notfound;
            }

            var res = req.CreateResponse(HttpStatusCode.OK);
            res.Headers.Add("Content-Type", "application/json");
            await res.WriteStringAsync(JsonSerializer.Serialize(new { user.Id, user.Email, user.FullName, user.CreatedAt, user.LastLoginAt }));
            return res;
        }
    }
}
