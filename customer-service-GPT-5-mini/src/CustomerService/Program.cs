using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Azure.Functions.Worker.Configuration;
using Microsoft.Extensions.Configuration;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using CustomerService.Services;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureAppConfiguration((ctx, cfg) =>
    {
        cfg.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
           .AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        // Configure Cosmos client: prefer Managed Identity (DefaultAzureCredential) when UseManagedIdentity=true
        var useManagedIdentity = configuration.GetValue<bool>("Cosmos:UseManagedIdentity");
        if (useManagedIdentity)
        {
            services.AddSingleton(s => new CosmosClient(configuration["Cosmos:AccountEndpoint"], new DefaultAzureCredential()));
        }
        else
        {
            services.AddSingleton(s => new CosmosClient(configuration["Cosmos:ConnectionString"]));
        }

        services.AddSingleton<ICustomerRepository, CosmosCustomerRepository>();
    })
    .Build();

host.Run();
