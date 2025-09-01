using CustomerService.Storage;
using CustomerService.OAuth;
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
    services.AddSingleton<IExternalIdentityRepository, CosmosExternalIdentityRepository>();
    services.AddSingleton<TokenService>();
    // OAuth integration
    services.AddSingleton<IOAuthStateStore, InMemoryOAuthStateStore>();
    services.AddSingleton<IOAuthProvider, CustomerService.OAuth.Providers.AppleOAuthProvider>();
    services.AddSingleton<IOAuthProvider, CustomerService.OAuth.Providers.MicrosoftOAuthProvider>();
    services.AddSingleton<IOAuthProvider, CustomerService.OAuth.Providers.LinkedInOAuthProvider>();
    services.AddSingleton<OAuthService>();
    })
    .ConfigureFunctionsWorkerDefaults()
    .Build();

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

await host.RunAsync();
