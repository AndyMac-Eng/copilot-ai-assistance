using System.Net;
using CustomerMicroservice.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace CustomerMicroservice.Functions
{
    public class GetCustomerAccountInfoFunction
    {
        private readonly CustomerCosmosDbService _dbService;

        public GetCustomerAccountInfoFunction(CustomerCosmosDbService dbService)
        {
            _dbService = dbService;
        }

        [Function("GetCustomerAccountInfo")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "customer/account")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("GetCustomerAccountInfo");
            // For demo: get customerId from query string
            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var customerId = query["id"];
            if (string.IsNullOrWhiteSpace(customerId))
            {
                var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badResponse.WriteStringAsync("Missing customer id.");
                return badResponse;
            }
            var customer = await _dbService.GetCustomerByIdAsync(customerId);
            if (customer == null)
            {
                var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                await notFoundResponse.WriteStringAsync("Customer not found.");
                return notFoundResponse;
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync(JsonSerializer.Serialize(customer));
            return response;
        }
    }
}
