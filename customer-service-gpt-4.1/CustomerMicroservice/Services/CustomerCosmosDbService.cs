using CustomerMicroservice.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CustomerMicroservice.Services
{
    public class CustomerCosmosDbService
    {
        private readonly Container _container;

        public CustomerCosmosDbService(IConfiguration configuration)
        {
            var connectionString = configuration["CosmosDbConnectionString"];
            var databaseName = configuration["CosmosDbDatabaseName"];
            var containerName = configuration["CosmosDbContainerName"];
            var client = new CosmosClient(connectionString);
            _container = client.GetContainer(databaseName, containerName);
        }

        public async Task<CustomerAccount> CreateCustomerAsync(CustomerAccount customer)
        {
            customer.Id = Guid.NewGuid().ToString();
            customer.CreatedAt = DateTime.UtcNow;
            await _container.CreateItemAsync(customer, new PartitionKey(customer.Id));
            return customer;
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

        public async Task<CustomerAccount?> GetCustomerByEmailAsync(string email)
        {
            var query = new QueryDefinition("SELECT * FROM c WHERE c.Email = @email").WithParameter("@email", email);
            var iterator = _container.GetItemQueryIterator<CustomerAccount>(query);
            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                var customer = response.Resource.FirstOrDefault();
                if (customer != null) return customer;
            }
            return null;
        }

        public async Task UpdateCustomerAsync(CustomerAccount customer)
        {
            await _container.UpsertItemAsync(customer, new PartitionKey(customer.Id));
        }
    }
}
