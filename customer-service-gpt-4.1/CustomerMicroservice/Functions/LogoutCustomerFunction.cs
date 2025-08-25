using System.Net;
using CustomerMicroservice.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace CustomerMicroservice.Functions
{
    public class LogoutCustomerFunction
    {
        private readonly CustomerCosmosDbService _dbService;

        public LogoutCustomerFunction(CustomerCosmosDbService dbService)
        {
            _dbService = dbService;
        }

        [Function("LogoutCustomer")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer/logout")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var logger = executionContext.GetLogger("LogoutCustomer");
            // For demo: just log out (token/session invalidation would go here)
            logger.LogInformation("Customer logout attempt.");
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Logout successful.");
            return response;
        }
    }
}
