using CustomerService.Models;

namespace CustomerService.Storage;

public interface IRefreshTokenRepository
{
    Task StoreAsync(RefreshTokenRecord record, CancellationToken ct = default);
    Task<RefreshTokenRecord?> GetValidAsync(string tenantId, string tokenHash, CancellationToken ct = default);
    Task RevokeAsync(string tenantId, string tokenHash, string? replacedByHash = null, CancellationToken ct = default);
    Task RevokeAllForUserAsync(string tenantId, string userId, CancellationToken ct = default);
}