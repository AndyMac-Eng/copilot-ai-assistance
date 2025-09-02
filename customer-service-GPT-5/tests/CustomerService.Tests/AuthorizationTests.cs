using System.Security.Claims;
using CustomerService.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

public class AuthorizationTests
{
    private static ClaimsPrincipal MakePrincipal(params string[] groups)
    {
        var claims = groups.Select(g => new Claim("groups", g));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "aad"));
    }

    [Fact]
    public void Mapping_Adds_ReadWrite_Permission()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Authorization:GroupMappings:grp-rw"] = PermissionConstants.AdminReadWrite
            }).Build();
        var mapper = new ClaimsMappingService(cfg);
        var principal = MakePrincipal("grp-rw");
        var enriched = mapper.Enrich(principal);
        enriched.Claims.Any(c=>c.Type==PermissionConstants.ClaimType && c.Value==PermissionConstants.AdminReadWrite).Should().BeTrue();
    }

    [Fact]
    public void Mapping_Skips_Absent_Group()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string,string?>
            {
                ["Authorization:GroupMappings:grp-ro"] = PermissionConstants.AdminReadOnly
            }).Build();
        var mapper = new ClaimsMappingService(cfg);
        var principal = MakePrincipal("some-other");
        var enriched = mapper.Enrich(principal);
        enriched.Claims.Any(c=>c.Type==PermissionConstants.ClaimType).Should().BeFalse();
    }

    [Fact]
    public void AuthorizationService_Evaluates_Permissions()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new []{ new Claim(PermissionConstants.ClaimType, PermissionConstants.AdminReadOnly) }, "test"));
        var authz = new AuthorizationService();
        authz.HasPermission(principal, PermissionConstants.AdminReadOnly).Should().BeTrue();
        authz.HasPermission(principal, PermissionConstants.AdminReadWrite).Should().BeFalse();
        authz.HasAnyPermission(principal, PermissionConstants.AdminReadWrite, PermissionConstants.AdminReadOnly).Should().BeTrue();
    }
}
