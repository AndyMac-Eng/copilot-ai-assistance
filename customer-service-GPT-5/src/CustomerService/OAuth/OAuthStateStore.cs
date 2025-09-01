namespace CustomerService.OAuth;

public record OAuthStateRecord(string State, string Nonce, string Provider, string? Mode, DateTimeOffset ExpiresUtc, string? LinkingCustomerId);

public interface IOAuthStateStore
{
    Task StoreAsync(OAuthStateRecord record, CancellationToken ct = default);
    Task<OAuthStateRecord?> TakeAsync(string state, CancellationToken ct = default);
}

public class InMemoryOAuthStateStore : IOAuthStateStore
{
    private readonly Dictionary<string, OAuthStateRecord> _records = new();
    private readonly object _lock = new();

    public Task StoreAsync(OAuthStateRecord record, CancellationToken ct = default)
    {
        lock (_lock)
        {
            _records[record.State] = record;
        }
        return Task.CompletedTask;
    }

    public Task<OAuthStateRecord?> TakeAsync(string state, CancellationToken ct = default)
    {
        OAuthStateRecord? rec = null;
        lock (_lock)
        {
            if (_records.TryGetValue(state, out var r))
            {
                if (r.ExpiresUtc > DateTimeOffset.UtcNow) rec = r;
                _records.Remove(state);
            }
        }
        return Task.FromResult(rec);
    }
}