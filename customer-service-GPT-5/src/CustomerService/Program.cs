using CustomerService.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

var host = new HostBuilder()
    .ConfigureAppConfiguration((ctx, builder) =>
    {
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        // Load environment specific overrides: Development, Uat, Production
        var envName = ctx.HostingEnvironment.EnvironmentName;
        builder.AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: true);
        builder.AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddLogging(lb => lb.AddSerilog());
        services.AddSingleton<ICustomerRepository, CosmosCustomerRepository>();
        services.AddSingleton<IRefreshTokenRepository, CosmosRefreshTokenRepository>();
        services.AddSingleton<TokenService>();
    })
    .ConfigureFunctionsWorkerDefaults()
    .Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

await host.RunAsync();
