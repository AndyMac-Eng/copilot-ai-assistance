using CustomerService.Storage;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Xunit;

public class TokenServiceTests
{
    [Fact]
    public void IssueToken_ReturnsJwt()
    {
        var inMemory = new Dictionary<string,string?>
        {
            ["JWT_SIGNING_KEY"] = new string('a',64),
            ["JWT_ISSUER"] = "issuer",
            ["JWT_AUDIENCE"] = "aud"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var svc = new TokenService(config);
    var result = svc.IssueTokens("t","id","a@example.com", new[]{"customer"});
    result.accessToken.Should().NotBeNullOrWhiteSpace();
    result.accessToken.Split('.').Length.Should().Be(3);
    result.refreshToken.Should().NotBeNullOrWhiteSpace();
    }
}
