using System.Threading.Tasks;
using CustomerService.Models;

namespace CustomerService.Services
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(string id);
        Task<Customer?> GetByEmailAsync(string email);
        Task CreateAsync(Customer customer);
        Task UpdateAsync(Customer customer);
        Task<bool> ValidateCredentialsAsync(string email, string password);
        Task UpdateLastLoginAsync(string id);
    }
}
