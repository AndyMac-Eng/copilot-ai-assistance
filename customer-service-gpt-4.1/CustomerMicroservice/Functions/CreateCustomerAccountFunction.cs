using System.Net;
using CustomerMicroservice.Models;
using CustomerMicroservice.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CustomerMicroservice.Functions
{
    public class CreateCustomerAccountFunction
    {
        private readonly CustomerCosmosDbService _dbService;

        public CreateCustomerAccountFunction(CustomerCosmosDbService dbService)
        {
            _dbService = dbService;
        }

        [Function("CreateCustomerAccount")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("CreateCustomerAccount");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var customer = JsonSerializer.Deserialize<CustomerAccount>(requestBody);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email) || string.IsNullOrWhiteSpace(customer.PasswordHash))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Invalid customer data.");
                return badResponse;
            }
            var createdCustomer = await _dbService.CreateCustomerAsync(customer);
            logger.LogInformation($"Created customer account for {createdCustomer.Email}");
            var response = req.CreateResponse(HttpStatusCode.Created);
            await response.WriteStringAsync(JsonSerializer.Serialize(createdCustomer));
            return response;
        }
    }
}
