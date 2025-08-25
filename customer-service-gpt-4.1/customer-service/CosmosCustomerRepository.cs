using System.Threading.Tasks;
using CustomerService.Models;
using Microsoft.Azure.Cosmos;
using System.Linq;

namespace CustomerService
{
    public class CosmosCustomerRepository : ICustomerRepository
    {
        private readonly Container _container;

        public CosmosCustomerRepository(Container container)
        {
            _container = container;
        }

        public async Task CreateCustomerAsync(CustomerAccount account)
        {
            await _container.CreateItemAsync(account, new PartitionKey(account.Id));
        }

        public async Task<CustomerAccount?> GetCustomerByEmailAsync(string email)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Email = @email")
                .WithParameter("@email", email);
            var iterator = _container.GetItemQueryIterator<CustomerAccount>(query);
            var results = await iterator.ReadNextAsync();
            return results.FirstOrDefault();
        }

        public async Task<CustomerAccount?> GetCustomerByIdAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<CustomerAccount>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }
    }
}
