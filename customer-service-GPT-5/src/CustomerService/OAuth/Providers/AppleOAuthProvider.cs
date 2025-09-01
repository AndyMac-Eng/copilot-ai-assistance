using CustomerService.Models;
using Microsoft.Extensions.Configuration;

namespace CustomerService.OAuth.Providers;

public class AppleOAuthProvider : IOAuthProvider
{
    private readonly IConfiguration _config;
    public string Name => "apple";
    public AppleOAuthProvider(IConfiguration config) { _config = config; }
    public Uri BuildAuthorizationUrl(string state, string nonce, string redirectBaseUrl, string? mode = null)
    {
        var clientId = _config["Apple:ServiceId"] ?? string.Empty;
        var redirect = new Uri(new Uri(redirectBaseUrl.TrimEnd('/')), $"/api/oauth/{Name}/callback");
        var url = $"https://appleid.apple.com/auth/authorize?response_type=code%20id_token&response_mode=query&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirect.ToString())}&scope={Uri.EscapeDataString("name email")}&state={state}&nonce={nonce}";
        return new Uri(url);
    }
    public Task<NormalizedExternalProfile> ExchangeAsync(string code, string redirectBaseUrl, string nonce, CancellationToken ct = default)
    {
        return Task.FromResult(new NormalizedExternalProfile(Name, $"sub-{Guid.NewGuid()}", null, false, null, null, null, null, null));
    }
}