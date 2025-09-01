using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CustomerService.OAuth;
using CustomerService.Storage;
using CustomerService.Models;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

public class OAuthServiceTests
{
    private class StubProvider : IOAuthProvider
    {
        public string Name => "stub";
        public Uri BuildAuthorizationUrl(string state, string nonce, string redirectBaseUrl, string? mode = null)
            => new Uri($"{redirectBaseUrl}/auth?state={state}&nonce={nonce}");
        public Task<NormalizedExternalProfile> ExchangeAsync(string code, string redirectBaseUrl, string nonce, CancellationToken ct = default)
            => Task.FromResult(new NormalizedExternalProfile(Name, "sub-123", "user@example.com", true, "User", "User", "Example", null, null));
    }

    private class InMemoryCustomerRepo : ICustomerRepository
    {
        private readonly Dictionary<string, CustomerAccount> _store = new();
        public Task CreateAsync(CustomerAccount account, CancellationToken ct = default)
        { _store[account.Id] = account; return Task.CompletedTask; }
        public Task<CustomerAccount?> GetByEmailAsync(string tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(_store.Values.FirstOrDefault(a => a.Email == email));
        public Task<CustomerAccount?> GetByIdAsync(string tenantId, string id, CancellationToken ct = default)
            => Task.FromResult(_store.TryGetValue(id, out var acc) ? acc : null);
        public Task UpdateAsync(CustomerAccount account, CancellationToken ct = default)
        { _store[account.Id] = account; return Task.CompletedTask; }
    }

    private class InMemoryExtRepo : IExternalIdentityRepository
    {
        private readonly List<ExternalIdentity> _list = new();
        public Task<int> CountForCustomerAsync(string tenantId, string customerId, CancellationToken ct = default) => Task.FromResult(_list.Count(e => e.CustomerId == customerId));
        public Task CreateAsync(ExternalIdentity identity, CancellationToken ct = default) { _list.Add(identity); return Task.CompletedTask; }
        public Task<IEnumerable<ExternalIdentity>> GetByCustomerAsync(string tenantId, string customerId, CancellationToken ct = default) => Task.FromResult<IEnumerable<ExternalIdentity>>(_list.Where(e => e.CustomerId == customerId));
        public Task<ExternalIdentity?> GetByProviderAsync(string tenantId, string provider, string providerSubject, CancellationToken ct = default) => Task.FromResult(_list.FirstOrDefault(e => e.Provider == provider && e.ProviderSubject == providerSubject));
        public Task UpdateAsync(ExternalIdentity identity, CancellationToken ct = default) { return Task.CompletedTask; }
    }

    [Fact]
    public async Task StartAsync_ReturnsAuthUrl_WithState()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["JWT_SIGNING_KEY"] = new string('a',64),
            ["OAUTH_REDIRECT_BASE_URL"] = "https://example.com",
            ["OAUTH_IDENTITY_CAP"] = "10"
        }).Build();
        var tokenSvc = new TokenService(cfg);
        var service = new OAuthService(new IOAuthProvider[]{ new StubProvider() }, new InMemoryOAuthStateStore(), new InMemoryExtRepo(), new InMemoryCustomerRepo(), tokenSvc, cfg);
        var url = await service.StartAsync("stub", null);
        url.ToString().Should().Contain("state=");
    }

    [Fact]
    public async Task CompleteAsync_CreatesAccountAndIssuesTokens()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["JWT_SIGNING_KEY"] = new string('a',64),
            ["OAUTH_REDIRECT_BASE_URL"] = "https://example.com",
            ["OAUTH_IDENTITY_CAP"] = "10"
        }).Build();
        var tokenSvc = new TokenService(cfg);
        var stateStore = new InMemoryOAuthStateStore();
        var service = new OAuthService(new IOAuthProvider[]{ new StubProvider() }, stateStore, new InMemoryExtRepo(), new InMemoryCustomerRepo(), tokenSvc, cfg);
        var startUrl = await service.StartAsync("stub", null);
        var state = System.Web.HttpUtility.ParseQueryString(startUrl.Query)["state"]!;
        var (access, refresh) = await service.CompleteAsync("stub", state, "code123");
        access.Should().NotBeNullOrWhiteSpace();
        refresh.Should().NotBeNullOrWhiteSpace();
    }
}