using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CustomerMicroservice
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = FunctionsApplication.CreateBuilder(args);
            builder.Services.AddSingleton<CustomerMicroservice.Services.CustomerCosmosDbService>();
            builder.ConfigureFunctionsWebApplication();
            builder.Services.AddApplicationInsightsTelemetryWorkerService();
            builder.Services.ConfigureFunctionsApplicationInsights();
            builder.Build().Run();
        }
    }
}
