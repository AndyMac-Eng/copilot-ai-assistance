using CustomerService.Models;
using Microsoft.Extensions.Configuration;

namespace CustomerService.OAuth.Providers;

public class MicrosoftOAuthProvider : IOAuthProvider
{
    private readonly IConfiguration _config;
    public string Name => "microsoft";
    public MicrosoftOAuthProvider(IConfiguration config) { _config = config; }
    public Uri BuildAuthorizationUrl(string state, string nonce, string redirectBaseUrl, string? mode = null)
    {
        var clientId = _config["Microsoft:ClientId"] ?? string.Empty;
        var tenant = _config["Microsoft:Tenant"] ?? "common";
        var scopes = _config["Microsoft:Scopes"] ?? "openid profile email";
        var redirect = new Uri(new Uri(redirectBaseUrl.TrimEnd('/')), $"/api/oauth/{Name}/callback");
        var url = $"https://login.microsoftonline.com/{tenant}/oauth2/v2.0/authorize?client_id={Uri.EscapeDataString(clientId)}&response_type=code&redirect_uri={Uri.EscapeDataString(redirect.ToString())}&response_mode=query&scope={Uri.EscapeDataString(scopes)}&state={state}&nonce={nonce}";
        return new Uri(url);
    }
    public Task<NormalizedExternalProfile> ExchangeAsync(string code, string redirectBaseUrl, string nonce, CancellationToken ct = default)
    {
        return Task.FromResult(new NormalizedExternalProfile(Name, $"sub-{Guid.NewGuid()}", null, false, null, null, null, null, null));
    }
}