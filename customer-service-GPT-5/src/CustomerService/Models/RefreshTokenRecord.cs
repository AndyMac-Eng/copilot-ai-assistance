namespace CustomerService.Models;

public record RefreshTokenRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TokenHash { get; init; } = string.Empty; // hashed token
    public string UserId { get; init; } = string.Empty;
    public string TenantId { get; init; } = "default";
    public DateTimeOffset ExpiresUtc { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? CreatedByIp { get; init; }
    public bool Revoked { get; init; }
    public DateTimeOffset? RevokedUtc { get; init; }
    public string? ReplacedByTokenHash { get; init; }
}