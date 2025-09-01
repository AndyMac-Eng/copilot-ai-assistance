namespace CustomerService.Models;

public record ExternalIdentity
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = "default";
    public string CustomerId { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ProviderSubject { get; init; } = string.Empty;
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? DisplayName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastLoginUtc { get; init; } = DateTimeOffset.UtcNow;
    public bool Revoked { get; init; }
    public string? ClaimsHash { get; init; }
    public Dictionary<string,string>? Metadata { get; init; }
    public string DocType => "externalIdentity";
}

public record NormalizedExternalProfile(
    string Provider,
    string ProviderSubject,
    string? Email,
    bool EmailVerified,
    string? DisplayName,
    string? FirstName,
    string? LastName,
    string? AvatarUrl,
    Dictionary<string,string>? RawClaims
);