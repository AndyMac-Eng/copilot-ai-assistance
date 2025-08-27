using CustomerService.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Storage;

public class CosmosRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly Container _container;
    public CosmosRefreshTokenRepository(IConfiguration cfg)
    {
        var conn = cfg["COSMOS_CONNECTION_STRING"] ?? throw new InvalidOperationException("COSMOS_CONNECTION_STRING not configured");
        var dbName = cfg["COSMOS_DATABASE"] ?? "customersdb";
        var containerName = cfg["COSMOS_REFRESH_CONTAINER"] ?? "refreshTokens";
        var client = new CosmosClient(conn);
        _container = client.GetDatabase(dbName).GetContainer(containerName);
    }

    public Task StoreAsync(RefreshTokenRecord record, CancellationToken ct = default)
        => _container.CreateItemAsync(record, new PartitionKey(record.TenantId), cancellationToken: ct);

    public async Task<RefreshTokenRecord?> GetValidAsync(string tenantId, string tokenHash, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c WHERE c.tenantId=@t AND c.tokenHash=@h AND c.revoked=false AND c.expiresUtc > @now")
            .WithParameter("@t", tenantId)
            .WithParameter("@h", tokenHash)
            .WithParameter("@now", DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        using var it = _container.GetItemQueryIterator<RefreshTokenRecord>(q, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(tenantId), MaxItemCount = 1 });
        while (it.HasMoreResults)
        {
            foreach (var rec in await it.ReadNextAsync(ct)) return rec;
        }
        return null;
    }

    public async Task RevokeAsync(string tenantId, string tokenHash, string? replacedByHash = null, CancellationToken ct = default)
    {
        var rec = await GetValidAsync(tenantId, tokenHash, ct);
        if (rec == null) return;
        var updated = rec with { Revoked = true, RevokedUtc = DateTimeOffset.UtcNow, ReplacedByTokenHash = replacedByHash };
        await _container.UpsertItemAsync(updated, new PartitionKey(tenantId), cancellationToken: ct);
    }

    public async Task RevokeAllForUserAsync(string tenantId, string userId, CancellationToken ct = default)
    {
        var q = new QueryDefinition("SELECT * FROM c WHERE c.tenantId=@t AND c.userId=@u AND c.revoked=false")
            .WithParameter("@t", tenantId).WithParameter("@u", userId);
        using var it = _container.GetItemQueryIterator<RefreshTokenRecord>(q, requestOptions: new QueryRequestOptions{ PartitionKey = new PartitionKey(tenantId)});
        while (it.HasMoreResults)
        {
            foreach (var rec in await it.ReadNextAsync(ct))
            {
                var updated = rec with { Revoked = true, RevokedUtc = DateTimeOffset.UtcNow };
                await _container.UpsertItemAsync(updated, new PartitionKey(tenantId), cancellationToken: ct);
            }
        }
    }
}