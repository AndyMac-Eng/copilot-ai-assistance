using CustomerService.Models;
using Microsoft.Extensions.Configuration;

namespace CustomerService.OAuth.Providers;

public class LinkedInOAuthProvider : IOAuthProvider
{
    private readonly IConfiguration _config;
    public string Name => "linkedin";
    public LinkedInOAuthProvider(IConfiguration config) { _config = config; }
    public Uri BuildAuthorizationUrl(string state, string nonce, string redirectBaseUrl, string? mode = null)
    {
        var clientId = _config["LinkedIn:ClientId"] ?? string.Empty;
        var scopes = _config["LinkedIn:Scopes"] ?? "r_liteprofile r_emailaddress";
        var redirect = new Uri(new Uri(redirectBaseUrl.TrimEnd('/')), $"/api/oauth/{Name}/callback");
        var url = $"https://www.linkedin.com/oauth/v2/authorization?response_type=code&client_id={Uri.EscapeDataString(clientId)}&redirect_uri={Uri.EscapeDataString(redirect.ToString())}&state={state}&scope={Uri.EscapeDataString(scopes)}";
        return new Uri(url);
    }
    public Task<NormalizedExternalProfile> ExchangeAsync(string code, string redirectBaseUrl, string nonce, CancellationToken ct = default)
    {
        return Task.FromResult(new NormalizedExternalProfile(Name, $"sub-{Guid.NewGuid()}", null, false, null, null, null, null, null));
    }
}