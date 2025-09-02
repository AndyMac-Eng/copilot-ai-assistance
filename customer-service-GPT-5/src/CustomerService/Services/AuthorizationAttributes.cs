using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using CustomerService.Models;
using CustomerService.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace CustomerService.Services;

// Attribute markers
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAuthenticationAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequirePermissionsAttribute : Attribute
{
    public string[] Permissions { get; }
    public RequirePermissionsAttribute(params string[] permissions) => Permissions = permissions;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireAnyPermissionsAttribute : Attribute
{
    public string[] Permissions { get; }
    public RequireAnyPermissionsAttribute(params string[] permissions) => Permissions = permissions;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class AuthorizePolicyAttribute : Attribute
{
    public string Policy { get; }
    public AuthorizePolicyAttribute(string policy) => Policy = policy;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class LoadCustomerAccountAttribute : Attribute { }

public interface IPermissionPolicyRegistry
{
    bool TryGet(string name, out Func<ClaimsPrincipal, bool> evaluator);
}

public class PermissionPolicyRegistry : IPermissionPolicyRegistry
{
    private readonly ConcurrentDictionary<string, Func<ClaimsPrincipal, bool>> _policies = new(StringComparer.OrdinalIgnoreCase);
    public PermissionPolicyRegistry()
    {
        // Default policies; can be extended via extension method at startup.
        _policies.TryAdd("AdminRead", p => p.HasAppPermission(PermissionConstants.AdminReadOnly) || p.HasAppPermission(PermissionConstants.AdminReadWrite));
        _policies.TryAdd("AdminWrite", p => p.HasAppPermission(PermissionConstants.AdminReadWrite));
    }
    public bool TryGet(string name, out Func<ClaimsPrincipal, bool> evaluator) => _policies.TryGetValue(name, out evaluator!);
    public void AddOrReplace(string name, Func<ClaimsPrincipal, bool> evaluator) => _policies[name] = evaluator;
}

// Invocation filter
public class AuthorizationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IClaimsMappingService _mapper;
    private readonly IAuthorizationService _authz;
    private readonly IPermissionPolicyRegistry _policies;
    private readonly ICustomerRepository _customers;

    private static readonly ConcurrentDictionary<string, MethodInfo?> MethodCache = new();

    public AuthorizationMiddleware(IClaimsMappingService mapper, IAuthorizationService authz, IPermissionPolicyRegistry policies, ICustomerRepository customers)
    {
        _mapper = mapper;
        _authz = authz;
        _policies = policies;
        _customers = customers;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var method = ResolveMethod(context.FunctionDefinition);
        if (method == null)
        {
            await next(context);
            return;
        }
        var attrs = method.GetCustomAttributes(inherit: false);
        if (attrs.Length == 0)
        {
            await next(context);
            return;
        }
        var requiresAuth = attrs.OfType<RequireAuthenticationAttribute>().Any() ||
                           attrs.OfType<RequirePermissionsAttribute>().Any() ||
                           attrs.OfType<RequireAnyPermissionsAttribute>().Any() ||
                           attrs.OfType<AuthorizePolicyAttribute>().Any() ||
                           attrs.OfType<LoadCustomerAccountAttribute>().Any();
        if (!requiresAuth)
        {
            await next(context);
            return;
        }
        var req = await context.GetHttpRequestDataAsync();
        if (req == null)
        {
            await next(context); // not HTTP
            return;
        }
        if (!req.Headers.TryGetValues("Authorization", out var authValues))
        {
            await ShortCircuit(context, req, HttpStatusCode.Unauthorized, "Missing token");
            return;
        }
        var tokenRaw = authValues.First().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries).Last();
        JwtSecurityToken jwt;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            jwt = handler.ReadJwtToken(tokenRaw);
        }
        catch
        {
            await ShortCircuit(context, req, HttpStatusCode.Unauthorized, "Invalid token");
            return;
        }
        var principal = new ClaimsPrincipal(new ClaimsIdentity(jwt.Claims.Select(c => new Claim(c.Type, c.Value)), "jwt"));
        principal = _mapper.Enrich(principal);
        context.Items["Principal"] = principal;
        context.Items["Jwt"] = jwt;

        foreach (var rp in attrs.OfType<RequirePermissionsAttribute>())
        {
            var missing = rp.Permissions.Where(p => !_authz.HasPermission(principal, p)).ToList();
            if (missing.Count > 0)
            {
                await ShortCircuit(context, req, HttpStatusCode.Forbidden, $"Missing permissions: {string.Join(',', missing)}");
                return;
            }
        }
        foreach (var any in attrs.OfType<RequireAnyPermissionsAttribute>())
        {
            if (!any.Permissions.Any(p => _authz.HasPermission(principal, p)))
            {
                await ShortCircuit(context, req, HttpStatusCode.Forbidden, $"Requires any of: {string.Join(',', any.Permissions)}");
                return;
            }
        }
        foreach (var policyAttr in attrs.OfType<AuthorizePolicyAttribute>())
        {
            if (!_policies.TryGet(policyAttr.Policy, out var evaluator) || !evaluator(principal))
            {
                await ShortCircuit(context, req, HttpStatusCode.Forbidden, $"Policy failed: {policyAttr.Policy}");
                return;
            }
        }
        if (attrs.OfType<LoadCustomerAccountAttribute>().Any())
        {
            var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
            var tenantId = principal.FindFirst("tid")?.Value ?? "default";
            if (string.IsNullOrEmpty(subject))
            {
                await ShortCircuit(context, req, HttpStatusCode.Unauthorized, "Missing subject claim");
                return;
            }
            var account = await _customers.GetByIdAsync(tenantId, subject);
            if (account == null)
            {
                await ShortCircuit(context, req, HttpStatusCode.NotFound, "Account not found");
                return;
            }
            context.Items["CustomerAccount"] = account;
        }
        await next(context);
    }

    private static MethodInfo? ResolveMethod(FunctionDefinition def) => MethodCache.GetOrAdd(def.EntryPoint, ep =>
    {
        var lastDot = ep.LastIndexOf('.');
        if (lastDot < 0) return null;
        var typeName = ep.Substring(0, lastDot);
        var methodName = ep.Substring(lastDot + 1);
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
        return type?.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    });

    private static async Task ShortCircuit(FunctionContext ctx, HttpRequestData req, HttpStatusCode status, string detail)
    {
        var resp = req.CreateResponse(status);
        await resp.WriteAsJsonAsync(new { error = detail });
        ctx.GetInvocationResult().Value = resp;
    }
}

public static class FunctionContextAuthExtensions
{
    public static ClaimsPrincipal? GetPrincipal(this FunctionContext ctx) => ctx.Items.TryGetValue("Principal", out var v) ? v as ClaimsPrincipal : null;
    public static CustomerAccount? GetCustomerAccount(this FunctionContext ctx) => ctx.Items.TryGetValue("CustomerAccount", out var v) ? v as CustomerAccount : null;
    public static JwtSecurityToken? GetJwt(this FunctionContext ctx) => ctx.Items.TryGetValue("Jwt", out var v) ? v as JwtSecurityToken : null;
}
