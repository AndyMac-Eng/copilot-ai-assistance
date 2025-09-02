using System.Security.Claims;
using Microsoft.Extensions.Configuration;

namespace CustomerService.Services;

public static class PermissionConstants
{
    public const string ClaimType = "cs.permission"; // internal claim type used by app
    public const string AdminReadWrite = "customer.admin.rw"; // read & write customer data
    public const string AdminReadOnly = "customer.admin.r";  // read-only customer data
}

public interface IClaimsMappingService
{
    /// <summary>
    /// Adds internal permission claims to the principal based on external (AAD) group memberships.
    /// </summary>
    ClaimsPrincipal Enrich(ClaimsPrincipal principal);
}

public class ClaimsMappingService : IClaimsMappingService
{
    private readonly IDictionary<string,string> _groupMappings; // groupId -> permission value

    public ClaimsMappingService(IConfiguration config)
    {
        _groupMappings = config.GetSection("Authorization:GroupMappings")
            .GetChildren()
            .ToDictionary(c => c.Key, c => c.Value ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    public ClaimsPrincipal Enrich(ClaimsPrincipal principal)
    {
        if (!_groupMappings.Any()) return principal; // nothing configured

        var groupIds = principal.FindAll("groups").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newClaims = new List<Claim>();
        foreach (var kvp in _groupMappings)
        {
            if (groupIds.Contains(kvp.Key))
            {
                newClaims.Add(new Claim(PermissionConstants.ClaimType, kvp.Value));
            }
        }
        if (newClaims.Count == 0) return principal;
        var id = new ClaimsIdentity(newClaims, principal.Identity?.AuthenticationType ?? "aad");
        var enriched = new ClaimsPrincipal(principal.Identities.Concat(new[] { id }));
        return enriched;
    }
}

public interface IAuthorizationService
{
    bool HasPermission(ClaimsPrincipal principal, string permission);
    bool HasAnyPermission(ClaimsPrincipal principal, params string[] permissions);
}

public class AuthorizationService : IAuthorizationService
{
    public bool HasPermission(ClaimsPrincipal principal, string permission) =>
        principal.Claims.Any(c => c.Type == PermissionConstants.ClaimType && c.Value == permission);

    public bool HasAnyPermission(ClaimsPrincipal principal, params string[] permissions)
    {
        var set = permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return principal.Claims.Any(c => c.Type == PermissionConstants.ClaimType && set.Contains(c.Value));
    }
}

public static class ClaimsPrincipalExtensions
{
    public static bool HasAppPermission(this ClaimsPrincipal p, string permission) =>
        p.Claims.Any(c => c.Type == PermissionConstants.ClaimType && c.Value == permission);
}