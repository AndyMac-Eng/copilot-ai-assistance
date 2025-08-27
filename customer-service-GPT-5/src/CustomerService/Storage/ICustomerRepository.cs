using CustomerService.Models;

namespace CustomerService.Storage;

public interface ICustomerRepository
{
    Task<CustomerAccount?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default);
    Task<CustomerAccount?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default);
    Task CreateAsync(CustomerAccount account, CancellationToken ct = default);
    Task UpdateAsync(CustomerAccount account, CancellationToken ct = default);
}
