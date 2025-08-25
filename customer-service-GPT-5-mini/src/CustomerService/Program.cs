using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using CustomerService.Services;

namespace CustomerService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = FunctionsApplication.CreateBuilder(args);

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Configuration;

            var useManagedIdentity = configuration.GetValue<bool>("Cosmos:UseManagedIdentity");
            if (useManagedIdentity)
            {
                builder.Services.AddSingleton(s => new CosmosClient(configuration["Cosmos:AccountEndpoint"], new DefaultAzureCredential()));
            }
            else
            {
                builder.Services.AddSingleton(s => new CosmosClient(configuration["Cosmos:ConnectionString"]));
            }

            builder.Services.AddSingleton<ICustomerRepository, CosmosCustomerRepository>();

            // Integrate with ASP.NET Core style functions web application
            builder.ConfigureFunctionsWebApplication();

            // Optional: Application Insights integration for worker service
            // builder.Services.AddApplicationInsightsTelemetryWorkerService();
            // builder.Services.ConfigureFunctionsApplicationInsights();

            builder.Build().Run();
        }
    }
}
