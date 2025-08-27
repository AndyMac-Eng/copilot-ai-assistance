using CustomerService.Models;
using Microsoft.Azure.Cosmos;
using System.Net;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Storage;

public class CosmosCustomerRepository : ICustomerRepository
{
    private readonly Container _container;
    private const string PartitionKeyPath = "/tenantId";

    public CosmosCustomerRepository(IConfiguration config)
    {
        var conn = config["COSMOS_CONNECTION_STRING"] ?? "";
        var dbName = config["COSMOS_DATABASE"] ?? "customers-db";
        var containerName = config["COSMOS_CONTAINER"] ?? "customers";
        if (string.IsNullOrWhiteSpace(conn))
        {
            throw new InvalidOperationException("COSMOS_CONNECTION_STRING not configured.");
        }
        var client = new CosmosClient(conn, new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
            SerializerOptions = new() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
        });
        var db = client.GetDatabase(dbName);
        _container = db.GetContainer(containerName);
    }

    public async Task CreateAsync(CustomerAccount account, CancellationToken ct = default)
    {
        await _container.CreateItemAsync(account, new PartitionKey(account.TenantId), cancellationToken: ct);
    }

    public async Task<CustomerAccount?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t AND c.normalizedEmail = @e")
            .WithParameter("@t", tenantId)
            .WithParameter("@e", email.ToLowerInvariant());
        using var feed = _container.GetItemQueryIterator<CustomerAccount>(query, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 });
        while (feed.HasMoreResults)
        {
            foreach (var item in await feed.ReadNextAsync(ct))
                return item;
        }
        return null;
    }

    public async Task<CustomerAccount?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
    {
        try
        {
            var resp = await _container.ReadItemAsync<CustomerAccount>(id, new PartitionKey(tenantId), cancellationToken: ct);
            return resp.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task UpdateAsync(CustomerAccount account, CancellationToken ct = default)
    {
        await _container.UpsertItemAsync(account, new PartitionKey(account.TenantId), cancellationToken: ct);
    }
}
