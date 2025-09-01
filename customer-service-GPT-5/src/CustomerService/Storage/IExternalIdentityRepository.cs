using CustomerService.Models;

namespace CustomerService.Storage;

public interface IExternalIdentityRepository
{
    Task<ExternalIdentity?> GetByProviderAsync(string tenantId, string provider, string providerSubject, CancellationToken ct = default);
    Task<IEnumerable<ExternalIdentity>> GetByCustomerAsync(string tenantId, string customerId, CancellationToken ct = default);
    Task<int> CountForCustomerAsync(string tenantId, string customerId, CancellationToken ct = default);
    Task CreateAsync(ExternalIdentity identity, CancellationToken ct = default);
    Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default);
}