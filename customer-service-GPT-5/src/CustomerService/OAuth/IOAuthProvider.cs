using CustomerService.Models;

namespace CustomerService.OAuth;

public interface IOAuthProvider
{
    string Name { get; }
    Uri BuildAuthorizationUrl(string state, string nonce, string redirectBaseUrl, string? mode = null);
    Task<NormalizedExternalProfile> ExchangeAsync(string code, string redirectBaseUrl, string nonce, CancellationToken ct = default);
}