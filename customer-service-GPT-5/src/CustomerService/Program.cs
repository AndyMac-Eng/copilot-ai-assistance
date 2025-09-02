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
    .ConfigureAppConfiguration(builder =>
    {
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.AddLogging(lb => lb.AddSerilog());
    services.AddSingleton<ICustomerRepository, CosmosCustomerRepository>();
        services.AddSingleton<IRefreshTokenRepository, CosmosRefreshTokenRepository>();
        services.AddSingleton<TokenService>();
    services.AddSingleton<CustomerService.Services.IClaimsMappingService, CustomerService.Services.ClaimsMappingService>();
    services.AddSingleton<CustomerService.Services.IAuthorizationService, CustomerService.Services.AuthorizationService>();
    services.AddSingleton<CustomerService.Services.IPermissionPolicyRegistry, CustomerService.Services.PermissionPolicyRegistry>();
    })
    .ConfigureFunctionsWorkerDefaults(builder =>
    {
        builder.UseMiddleware<CustomerService.Services.AuthorizationMiddleware>();
    })
    .Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

await host.RunAsync();
