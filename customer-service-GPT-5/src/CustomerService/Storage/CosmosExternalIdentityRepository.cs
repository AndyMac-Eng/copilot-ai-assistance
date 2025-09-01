using CustomerService.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Storage;

public class CosmosExternalIdentityRepository : IExternalIdentityRepository
{
    private readonly Container _container;

    public CosmosExternalIdentityRepository(IConfiguration config)
    {
        var conn = config["COSMOS_CONNECTION_STRING"] ?? "";
        var dbName = config["COSMOS_DATABASE"] ?? "customersdb";
        var containerName = config["COSMOS_EXTERNAL_CONTAINER"] ?? "externalIdentities";
        if (string.IsNullOrWhiteSpace(conn))
            throw new InvalidOperationException("COSMOS_CONNECTION_STRING not configured.");
        var client = new CosmosClient(conn, new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway, SerializerOptions = new() { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase } });
        _container = client.GetDatabase(dbName).GetContainer(containerName);
    }

    public async Task<ExternalIdentity?> GetByProviderAsync(string tenantId, string provider, string providerSubject, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t AND c.provider = @p AND c.providerSubject = @s")
            .WithParameter("@t", tenantId).WithParameter("@p", provider).WithParameter("@s", providerSubject);
        using var feed = _container.GetItemQueryIterator<ExternalIdentity>(q, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 });
        while (feed.HasMoreResults)
        {
            foreach (var item in await feed.ReadNextAsync(ct)) return item;
        }
        return null;
    }

    public async Task<IEnumerable<ExternalIdentity>> GetByCustomerAsync(string tenantId, string customerId, CancellationToken ct = default)
    {
        var list = new List<ExternalIdentity>();
        var q = new QueryDefinition("SELECT * FROM c WHERE c.tenantId = @t AND c.customerId = @c")
            .WithParameter("@t", tenantId).WithParameter("@c", customerId);
        using var feed = _container.GetItemQueryIterator<ExternalIdentity>(q, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId) });
        while (feed.HasMoreResults)
        {
            list.AddRange(await feed.ReadNextAsync(ct));
        }
        return list;
    }

    public async Task<int> CountForCustomerAsync(string tenantId, string customerId, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT VALUE COUNT(1) FROM c WHERE c.tenantId = @t AND c.customerId = @c")
            .WithParameter("@t", tenantId).WithParameter("@c", customerId);
        using var feed = _container.GetItemQueryIterator<int>(q, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 });
        while (feed.HasMoreResults)
        {
            foreach (var val in await feed.ReadNextAsync(ct)) return val;
        }
        return 0;
    }

    public async Task CreateAsync(ExternalIdentity identity, CancellationToken ct = default) =>
        await _container.CreateItemAsync(identity, new PartitionKey(identity.TenantId), cancellationToken: ct);

    public async Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default) =>
        await _container.UpsertItemAsync(identity, new PartitionKey(identity.TenantId), cancellationToken: ct);
}