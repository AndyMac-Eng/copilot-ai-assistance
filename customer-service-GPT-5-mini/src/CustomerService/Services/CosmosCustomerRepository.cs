using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using CustomerService.Models;
using System.Security.Cryptography;
using System.Text;

namespace CustomerService.Services
{
    public class CosmosCustomerRepository : ICustomerRepository
    {
        private readonly Container _container;

        public CosmosCustomerRepository(CosmosClient client)
        {
            var dbName = Environment.GetEnvironmentVariable("COSMOS_DATABASE") ?? "customerdb";
            var containerName = Environment.GetEnvironmentVariable("COSMOS_CONTAINER") ?? "customers";
            _container = client.GetContainer(dbName, containerName);
        }

        public async Task CreateAsync(Customer customer)
        {
            await _container.CreateItemAsync(customer, new PartitionKey(customer.Email));
        }

        public async Task<Customer?> GetByEmailAsync(string email)
        {
            var sql = "SELECT * FROM c WHERE c.Email = @email";
            var query = _container.GetItemQueryIterator<Customer>(new QueryDefinition(sql).WithParameter("@email", email));
            if (query.HasMoreResults)
            {
                var res = await query.ReadNextAsync();
                return res.Resource.FirstOrDefault();
            }
            return null;
        }

        public async Task<Customer?> GetByIdAsync(string id)
        {
            try
            {
                var response = await _container.ReadItemAsync<Customer>(id, new PartitionKey(id));
                return response.Resource;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public async Task UpdateAsync(Customer customer)
        {
            await _container.UpsertItemAsync(customer, new PartitionKey(customer.Email));
        }

        public async Task<bool> ValidateCredentialsAsync(string email, string password)
        {
            var user = await GetByEmailAsync(email);
            if (user == null) return false;

            var saltBytes = Convert.FromBase64String(user.PasswordSalt);
            using var deriveBytes = new Rfc2898DeriveBytes(password, saltBytes, 100_000, HashAlgorithmName.SHA256);
            var computed = Convert.ToBase64String(deriveBytes.GetBytes(32));
            return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(computed), Convert.FromBase64String(user.PasswordHash)) ;
        }

        public async Task UpdateLastLoginAsync(string id)
        {
            var user = await GetByIdAsync(id);
            if (user == null) return;
            user.LastLoginAt = DateTime.UtcNow;
            await UpdateAsync(user);
        }
    }
}
