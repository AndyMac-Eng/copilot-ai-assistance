using System;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using CustomerService.Models;

namespace CustomerService
{
    public class CustomerFunctions
    {
        private readonly ICustomerRepository _repo;
        private readonly ILogger _logger;

        public CustomerFunctions(ICustomerRepository repo, ILoggerFactory loggerFactory)
        {
            _repo = repo;
            _logger = loggerFactory.CreateLogger<CustomerFunctions>();
        }

        [Function("CreateCustomer")]
        public async Task<HttpResponseData> CreateCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers")] HttpRequestData req)
        {
            var customer = await req.ReadFromJsonAsync<CustomerAccount>();
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email) || string.IsNullOrWhiteSpace(customer.PasswordHash))
                return req.CreateResponse(HttpStatusCode.BadRequest);
            customer.Id = Guid.NewGuid().ToString();
            customer.CreatedAt = DateTime.UtcNow;
            customer.IsActive = true;
            await _repo.CreateCustomerAsync(customer);
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteAsJsonAsync(customer);
            return response;
        }

        [Function("LoginCustomer")]
        public async Task<HttpResponseData> LoginCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/login")] HttpRequestData req)
        {
            var login = await req.ReadFromJsonAsync<LoginRequest>();
            var account = await _repo.GetCustomerByEmailAsync(login.Email);
            if (account == null || !PasswordHasher.Verify(login.Password, account.PasswordHash))
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            // Generate JWT (handled by Azure AD in production)
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new { Token = "<jwt-token>" });
            return response;
        }

        [Function("LogoutCustomer")]
        public HttpResponseData LogoutCustomer(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customers/logout")] HttpRequestData req)
        {
            // Token invalidation handled by client/AAD
            var response = req.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        [Function("GetCustomerInfo")]
        public async Task<HttpResponseData> GetCustomerInfo(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customers/me")] HttpRequestData req)
        {
            // In production, parse JWT from Authorization header to get userId (sub claim)
            string? userId = null;
            if (req.Headers.TryGetValues("x-ms-client-principal-id", out var values))
            {
                userId = values.FirstOrDefault();
            }
            // TODO: Replace with real JWT parsing for sub claim
            if (string.IsNullOrEmpty(userId))
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            var account = await _repo.GetCustomerByIdAsync(userId);
            var response = req.CreateResponse(account != null ? HttpStatusCode.OK : HttpStatusCode.NotFound);
            if (account != null)
                await response.WriteAsJsonAsync(account);
            return response;
        }
    }

    public class LoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    public static class PasswordHasher
    {
        public static string Hash(string password) => password; // Replace with real hash
        public static bool Verify(string password, string hash) => password == hash; // Replace with real verification
    }
}
