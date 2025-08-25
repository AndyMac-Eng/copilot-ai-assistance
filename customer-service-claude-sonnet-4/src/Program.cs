using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CustomerService.Configuration;
using CustomerService.Repositories;
using CustomerService.Repositories.Interfaces;
using CustomerService.Services;
using CustomerService.Services.Interfaces;

namespace CustomerService
{
    /// <summary>
    /// Program entry point and dependency injection configuration
    /// </summary>
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureServices(ConfigureServices)
                .Build();

            host.Run();
        }

        private static void ConfigureServices(HostBuilderContext context, IServiceCollection services)
        {
            // Add Application Insights
            services.AddApplicationInsightsTelemetryWorkerService();
            services.ConfigureFunctionsApplicationInsights();

            // Configuration
            var configuration = context.Configuration;
            
            services.Configure<CosmosDbConfiguration>(
                configuration.GetSection(CosmosDbConfiguration.SectionName));
            services.Configure<JwtConfiguration>(
                configuration.GetSection(JwtConfiguration.SectionName));
            services.Configure<AzureAdB2CConfiguration>(
                configuration.GetSection(AzureAdB2CConfiguration.SectionName));
            services.Configure<ApplicationInsightsConfiguration>(
                configuration.GetSection(ApplicationInsightsConfiguration.SectionName));
            services.Configure<KeyVaultConfiguration>(
                configuration.GetSection(KeyVaultConfiguration.SectionName));

            // Cosmos DB Client
            services.AddSingleton<CosmosClient>(serviceProvider =>
            {
                var cosmosConfig = configuration.GetSection(CosmosDbConfiguration.SectionName).Get<CosmosDbConfiguration>();
                if (cosmosConfig == null || string.IsNullOrEmpty(cosmosConfig.ConnectionString))
                {
                    throw new InvalidOperationException("Cosmos DB connection string is not configured");
                }
                
                return new CosmosClient(cosmosConfig.ConnectionString, new CosmosClientOptions
                {
                    SerializerOptions = new CosmosSerializationOptions
                    {
                        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                    }
                });
            });

            // Repository Services
            services.AddScoped<ICustomerRepository, CosmosDbCustomerRepository>();

            // Business Services
            services.AddScoped<IAuthenticationService, AuthenticationService>();
            services.AddScoped<ICustomerService, Services.CustomerService>();

            // Logging
            services.AddLogging(builder =>
            {
                builder.AddApplicationInsights();
                builder.AddConsole();
            });
        }
    }
}
