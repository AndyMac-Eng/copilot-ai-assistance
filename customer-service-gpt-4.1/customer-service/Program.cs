using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Cosmos;
using System;

namespace CustomerService
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices((context, services) =>
                {
                    var config = context.Configuration;
                    var cosmosEndpoint = config["CosmosDb:AccountEndpoint"] ?? Environment.GetEnvironmentVariable("COSMOS_DB_ENDPOINT");
                    var cosmosKey = config["CosmosDb:AccountKey"] ?? Environment.GetEnvironmentVariable("COSMOS_DB_KEY");
                    var dbName = config["CosmosDb:DatabaseName"] ?? Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE") ?? "customerdb";
                    var client = new CosmosClient(cosmosEndpoint, cosmosKey);
                    var container = client.GetContainer(dbName, "CustomerAccounts");
                    services.AddSingleton<ICustomerRepository>(new CosmosCustomerRepository(container));
                })
                .Build();
            host.Run();
        }
    }
}
