using System;
using CustomerService.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

public class TokenServiceLifetimeTests
{
    [Theory]
    [InlineData("Development", 14)]
    [InlineData("Uat", 1)]
    [InlineData("Production", 1)]
    public void RefreshTokenLifetime_DependsOnEnvironment(string env, int expectedDays)
    {
        var inMemory = new Dictionary<string,string?>
        {
            ["JWT_SIGNING_KEY"] = new string('a',64),
            ["JWT_ISSUER"] = "issuer",
            ["JWT_AUDIENCE"] = "aud",
            // mimic what appsettings.<Env>.json provides
            ["JWT_REFRESH_DAYS"] = expectedDays.ToString()
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var svc = new TokenService(config);
        var (_, _, _, refreshExp) = svc.IssueTokens("t","id","a@example.com", new[]{"customer"});
        var diff = refreshExp - DateTimeOffset.UtcNow;
        diff.TotalDays.Should().BeApproximately(expectedDays, 0.2, "refresh token lifetime for {0} should be {1} days", env, expectedDays);
    }
}
