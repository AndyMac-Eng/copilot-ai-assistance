using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using CustomerService.Models;
using CustomerService.Repositories.Interfaces;
using CustomerService.Configuration;
using Microsoft.Extensions.Options;

namespace CustomerService.Repositories
{
    /// <summary>
    /// Cosmos DB implementation of customer repository
    /// </summary>
    public class CosmosDbCustomerRepository : ICustomerRepository
    {
        private readonly Container _container;
        private readonly ILogger<CosmosDbCustomerRepository> _logger;

        public CosmosDbCustomerRepository(
            CosmosClient cosmosClient,
            IOptions<CosmosDbConfiguration> config,
            ILogger<CosmosDbCustomerRepository> logger)
        {
            _logger = logger;
            var database = cosmosClient.GetDatabase(config.Value.DatabaseName);
            _container = database.GetContainer(config.Value.ContainerName);
        }

        public async Task<Customer?> GetByIdAsync(string id)
        {
            try
            {
                _logger.LogInformation("Retrieving customer with ID: {CustomerId}", id);
                
                var response = await _container.ReadItemAsync<Customer>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Customer with ID {CustomerId} not found", id);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer with ID: {CustomerId}", id);
                throw;
            }
        }

        public async Task<Customer?> GetByEmailAsync(string email)
        {
            try
            {
                _logger.LogInformation("Retrieving customer with email: {Email}", email);
                
                var query = new QueryDefinition("SELECT * FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                using var iterator = _container.GetItemQueryIterator<Customer>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    var customer = response.FirstOrDefault();
                    if (customer != null)
                    {
                        return customer;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customer with email: {Email}", email);
                throw;
            }
        }

        public async Task<Customer> CreateAsync(Customer customer)
        {
            try
            {
                _logger.LogInformation("Creating new customer with email: {Email}", customer.Email);
                
                customer.CreatedAt = DateTime.UtcNow;
                customer.UpdatedAt = DateTime.UtcNow;
                
                var response = await _container.CreateItemAsync(customer, new PartitionKey(customer.PartitionKey));
                
                _logger.LogInformation("Successfully created customer with ID: {CustomerId}", customer.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer with email: {Email}", customer.Email);
                throw;
            }
        }

        public async Task<Customer> UpdateAsync(Customer customer)
        {
            try
            {
                _logger.LogInformation("Updating customer with ID: {CustomerId}", customer.Id);
                
                customer.UpdatedAt = DateTime.UtcNow;
                
                var response = await _container.ReplaceItemAsync(
                    customer, 
                    customer.Id, 
                    new PartitionKey(customer.PartitionKey));
                
                _logger.LogInformation("Successfully updated customer with ID: {CustomerId}", customer.Id);
                return response.Resource;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer with ID: {CustomerId}", customer.Id);
                throw;
            }
        }

        public async Task DeleteAsync(string id)
        {
            try
            {
                _logger.LogInformation("Deleting customer with ID: {CustomerId}", id);
                
                await _container.DeleteItemAsync<Customer>(id, new PartitionKey(id));
                
                _logger.LogInformation("Successfully deleted customer with ID: {CustomerId}", id);
            }
            catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Customer with ID {CustomerId} not found for deletion", id);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer with ID: {CustomerId}", id);
                throw;
            }
        }

        public async Task<bool> ExistsAsync(string email)
        {
            try
            {
                _logger.LogInformation("Checking if customer exists with email: {Email}", email);
                
                var query = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.email = @email")
                    .WithParameter("@email", email);

                using var iterator = _container.GetItemQueryIterator<int>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    return response.FirstOrDefault() > 0;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if customer exists with email: {Email}", email);
                throw;
            }
        }

        public async Task<IEnumerable<Customer>> GetAllAsync(int skip = 0, int take = 100)
        {
            try
            {
                _logger.LogInformation("Retrieving customers with skip: {Skip}, take: {Take}", skip, take);
                
                var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAt DESC OFFSET @skip LIMIT @take")
                    .WithParameter("@skip", skip)
                    .WithParameter("@take", take);

                var customers = new List<Customer>();
                using var iterator = _container.GetItemQueryIterator<Customer>(query);
                
                while (iterator.HasMoreResults)
                {
                    var response = await iterator.ReadNextAsync();
                    customers.AddRange(response);
                }
                
                return customers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers with skip: {Skip}, take: {Take}", skip, take);
                throw;
            }
        }
    }
}
