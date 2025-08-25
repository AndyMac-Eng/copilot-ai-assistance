using System.Threading.Tasks;
using CustomerService.Models;

namespace CustomerService
{
    public interface ICustomerRepository
    {
        Task CreateCustomerAsync(CustomerAccount account);
        Task<CustomerAccount?> GetCustomerByEmailAsync(string email);
        Task<CustomerAccount?> GetCustomerByIdAsync(string id);
    }
}
