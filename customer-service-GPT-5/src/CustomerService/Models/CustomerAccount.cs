namespace CustomerService.Models;

public record CustomerAccount
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = "default"; // multi-tenant readiness
    public string Email { get; init; } = string.Empty; // unique index
    public string NormalizedEmail => Email.ToLowerInvariant();
    public string PasswordHash { get; init; } = string.Empty; // BCrypt hashed
    public string? DisplayName { get; init; }
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLoginUtc { get; init; }
    public bool IsLocked { get; init; } = false;
    public string[] Roles { get; init; } = Array.Empty<string>();
    public CustomerAccountSecurity Security { get; init; } = new();
    public CustomerAccountPreferences Preferences { get; init; } = new();
    public AuditMetadata Audit { get; init; } = new();
}

public record CustomerAccountSecurity
{
    public string PasswordVersion { get; init; } = "bcrypt-v1";
    public int FailedLoginAttempts { get; init; }
    public DateTimeOffset? LockoutEndUtc { get; init; }
    public DateTimeOffset? PasswordChangedUtc { get; init; }
    public string? MfaType { get; init; }
    public bool MfaEnabled { get; init; }
    public string? TotpSecretEncrypted { get; init; } // base64 protected (sample)
    public string[] RecoveryCodesHashed { get; init; } = Array.Empty<string>();
}

public record CustomerAccountPreferences
{
    public string Locale { get; init; } = "en-US";
    public string TimeZone { get; init; } = "UTC";
    public bool MarketingOptIn { get; init; } = false;
}

public record AuditMetadata
{
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedUtc { get; init; }
    public string? UpdatedBy { get; init; }
}
