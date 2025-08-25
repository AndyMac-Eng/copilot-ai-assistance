using System.Net;
using CustomerMicroservice.Models;
using CustomerMicroservice.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CustomerMicroservice.Functions
{
    public class LoginCustomerFunction
    {
        private readonly CustomerCosmosDbService _dbService;

        public LoginCustomerFunction(CustomerCosmosDbService dbService)
        {
            _dbService = dbService;
        }

        [Function("LoginCustomer")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/login")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("LoginCustomer");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var loginData = JsonSerializer.Deserialize<CustomerAccount>(requestBody);
            if (loginData == null || string.IsNullOrWhiteSpace(loginData.Email) || string.IsNullOrWhiteSpace(loginData.PasswordHash))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid login data.");
                return badResponse;
            }
            var customer = await _dbService.GetCustomerByEmailAsync(loginData.Email);
            if (customer == null || customer.PasswordHash != loginData.PasswordHash)
            {
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid email or password.");
                return unauthorizedResponse;
            }
            customer.LastLogin = DateTime.UtcNow;
            await _dbService.UpdateCustomerAsync(customer);
            logger.LogInformation($"Customer {customer.Email} logged in.");
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Login successful.");
            return response;
        }
    }
}
