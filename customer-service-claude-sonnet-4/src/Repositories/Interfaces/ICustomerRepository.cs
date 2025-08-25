using CustomerService.Models;

namespace CustomerService.Repositories.Interfaces
{
    /// <summary>
    /// Repository interface for customer data operations
    /// </summary>
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(string id);
        Task<Customer?> GetByEmailAsync(string email);
        Task<Customer> CreateAsync(Customer customer);
        Task<Customer> UpdateAsync(Customer customer);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string email);
        Task<IEnumerable<Customer>> GetAllAsync(int skip = 0, int take = 100);
    }
}
