using System.Security.Cryptography;
using CustomerService.Models;
using CustomerService.Storage;
using Microsoft.Extensions.Configuration;

namespace CustomerService.OAuth;

public class OAuthService
{
    private readonly IEnumerable<IOAuthProvider> _providers;
    private readonly IOAuthStateStore _stateStore;
    private readonly IExternalIdentityRepository _extRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly TokenService _tokenService;
    private readonly IConfiguration _config;
    private readonly int _identityCap;

    public OAuthService(IEnumerable<IOAuthProvider> providers, IOAuthStateStore stateStore, IExternalIdentityRepository extRepo, ICustomerRepository customerRepo, TokenService tokenService, IConfiguration config)
    {
        _providers = providers;
        _stateStore = stateStore;
        _extRepo = extRepo;
        _customerRepo = customerRepo;
        _tokenService = tokenService;
        _config = config;
        _identityCap = int.TryParse(config["OAUTH_IDENTITY_CAP"], out var cap) ? cap : 10;
    }

    public IOAuthProvider? GetProvider(string name) => _providers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public async Task<Uri> StartAsync(string providerName, string? linkingCustomerId, CancellationToken ct = default)
    {
        var provider = GetProvider(providerName) ?? throw new InvalidOperationException("Unknown provider");
        var state = Base64Url(Guid.NewGuid().ToByteArray());
        var nonce = Base64Url(RandomNumberGenerator.GetBytes(16));
        var record = new OAuthStateRecord(state, nonce, provider.Name, linkingCustomerId != null ? "link" : "login", DateTimeOffset.UtcNow.AddMinutes(5), linkingCustomerId);
        await _stateStore.StoreAsync(record, ct);
        var baseUrl = _config["OAUTH_REDIRECT_BASE_URL"] ?? throw new InvalidOperationException("OAUTH_REDIRECT_BASE_URL not configured");
        return provider.BuildAuthorizationUrl(state, nonce, baseUrl, record.Mode);
    }

    public async Task<(string accessToken,string refreshToken)> CompleteAsync(string providerName, string state, string code, CancellationToken ct = default)
    {
        var record = await _stateStore.TakeAsync(state, ct) ?? throw new InvalidOperationException("Invalid or expired state");
        if (!record.Provider.Equals(providerName, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Provider mismatch");
        var provider = GetProvider(providerName)!;
        var baseUrl = _config["OAUTH_REDIRECT_BASE_URL"] ?? throw new InvalidOperationException("OAUTH_REDIRECT_BASE_URL not configured");
        var profile = await provider.ExchangeAsync(code, baseUrl, record.Nonce, ct);
        var tenantId = "default";
        var existingExt = await _extRepo.GetByProviderAsync(tenantId, provider.Name, profile.ProviderSubject, ct);
        CustomerAccount? customer = null;
        if (existingExt != null)
        {
            customer = await _customerRepo.GetByIdAsync(tenantId, existingExt.CustomerId, ct);
        }
        else if (record.LinkingCustomerId != null)
        {
            var count = await _extRepo.CountForCustomerAsync(tenantId, record.LinkingCustomerId, ct);
            if (count >= _identityCap) throw new InvalidOperationException("Identity cap reached");
            customer = await _customerRepo.GetByIdAsync(tenantId, record.LinkingCustomerId, ct) ?? throw new InvalidOperationException("Customer not found for linking");
            var newExt = new ExternalIdentity
            {
                CustomerId = customer.Id,
                TenantId = tenantId,
                Provider = provider.Name,
                ProviderSubject = profile.ProviderSubject,
                Email = profile.Email,
                EmailVerified = profile.EmailVerified,
                DisplayName = profile.DisplayName,
                FirstName = profile.FirstName,
                LastName = profile.LastName
            };
            await _extRepo.CreateAsync(newExt, ct);
        }
        else
        {
            var account = new CustomerAccount
            {
                Email = profile.Email ?? $"{provider.Name}-user-{Guid.NewGuid():N}@placeholder.local",
                PasswordHash = string.Empty,
                DisplayName = profile.DisplayName,
                Roles = new[] { "customer" }
            };
            await _customerRepo.CreateAsync(account, ct);
            var ext = new ExternalIdentity
            {
                CustomerId = account.Id,
                TenantId = tenantId,
                Provider = provider.Name,
                ProviderSubject = profile.ProviderSubject,
                Email = profile.Email,
                EmailVerified = profile.EmailVerified,
                DisplayName = profile.DisplayName,
                FirstName = profile.FirstName,
                LastName = profile.LastName
            };
            await _extRepo.CreateAsync(ext, ct);
            customer = account;
        }
        if (customer == null) throw new InvalidOperationException("Customer resolution failed");
        var tokens = _tokenService.IssueTokens(tenantId, customer.Id, customer.Email, customer.Roles);
        return (tokens.accessToken, tokens.refreshToken);
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+','-').Replace('/','_');
}