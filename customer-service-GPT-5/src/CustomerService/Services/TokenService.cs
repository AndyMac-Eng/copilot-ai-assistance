using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Storage;

public class TokenService
{
    private readonly IConfiguration _config;
    private readonly KeyClient? _keyClient;
    private readonly CryptographyClient? _cryptoClient;
    private readonly Lazy<SecurityKey> _fallbackSymmetricKey;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _refreshTokens = new();

    public TokenService(IConfiguration config)
    {
        _config = config;
        var keyVaultUrl = config["KEYVAULT_URL"];
        var keyName = config["JWT_KEY_NAME"]; // Optional Key Vault key for asymmetric signing
        if (!string.IsNullOrEmpty(keyVaultUrl) && !string.IsNullOrEmpty(keyName))
        {
            _keyClient = new KeyClient(new Uri(keyVaultUrl), new DefaultAzureCredential());
            try
            {
                var key = _keyClient.GetKey(keyName);
                _cryptoClient = new CryptographyClient(key.Value.Id, new DefaultAzureCredential());
            }
            catch
            {
                // fallback silently; logging can be added
            }
        }
        _fallbackSymmetricKey = new Lazy<SecurityKey>(() => new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["JWT_SIGNING_KEY"] ?? throw new InvalidOperationException("JWT_SIGNING_KEY not configured"))));
    }

    public (string accessToken, string refreshToken, DateTimeOffset accessExpires, DateTimeOffset refreshExpires) IssueTokens(string tenantId, string customerId, string email, IEnumerable<string> roles)
    {
        var issuer = _config["JWT_ISSUER"] ?? "customer-service";
        var audience = _config["JWT_AUDIENCE"] ?? "customer-clients";
        var jti = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;
        var accessExp = now.AddMinutes(int.TryParse(_config["JWT_ACCESS_MINUTES"], out var am) ? am : 15);
        var refreshExp = now.AddDays(int.TryParse(_config["JWT_REFRESH_DAYS"], out var rd) ? rd : 7);
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, customerId),
            new Claim("tid", tenantId),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Jti, jti)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        SigningCredentials creds;
        string? kid = null;
        if (_cryptoClient != null)
        {
            // Use Key Vault key material (assumes RSA key)
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = accessExp,
                Issuer = issuer,
                Audience = audience
            };
            // We create token manually then sign bytes; simpler path: fetch key and construct RsaSecurityKey if exportable. Here fallback to symmetric if not.
            try
            {
                // Fallback simplified: use symmetric route if cannot sign
                // (Production: implement custom ISecurityTokenValidator or CryptoProviderFactory using _cryptoClient.SignData)
                throw new NotImplementedException();
            }
            catch
            {
                creds = new SigningCredentials(_fallbackSymmetricKey.Value, SecurityAlgorithms.HmacSha256);
            }
        }
        else
        {
            creds = new SigningCredentials(_fallbackSymmetricKey.Value, SecurityAlgorithms.HmacSha256);
        }

        var token = new JwtSecurityToken(issuer, audience, claims, notBefore: now, expires: accessExp, signingCredentials: creds);
        if (kid != null)
        {
            token.Header[JwtHeaderParameterNames.Kid] = kid;
        }
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        _refreshTokens[refreshToken] = refreshExp;
        return (accessToken, refreshToken, accessExp, refreshExp);
    }

    public bool TryValidateRefreshToken(string refreshToken, bool consume, out DateTimeOffset expires)
    {
        expires = default;
        if (!_refreshTokens.TryGetValue(refreshToken, out var exp)) return false;
        if (exp < DateTimeOffset.UtcNow)
        {
            _refreshTokens.TryRemove(refreshToken, out _);
            return false;
        }
        if (consume)
        {
            _refreshTokens.TryRemove(refreshToken, out _);
        }
        expires = exp;
        return true;
    }

    public void RevokeAllForUser(string userId) {
        // Simplified: Clear all (production: store mapping user->tokens)
        foreach (var kv in _refreshTokens.Keys.ToList()) _refreshTokens.TryRemove(kv, out _);
    }
}
