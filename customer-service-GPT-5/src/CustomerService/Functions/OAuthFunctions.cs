using System.Net;
using CustomerService.OAuth;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace CustomerService.Functions;

public class OAuthFunctions
{
    private readonly OAuthService _oauthService;
    public OAuthFunctions(OAuthService oauthService) { _oauthService = oauthService; }

    [Function("OAuthStart")]
    public async Task<HttpResponseData> Start([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "oauth/{provider}/start")] HttpRequestData req, string provider)
    {
        string? linkCustomerId = null;
        var queryPairs = req.Url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var kv in queryPairs)
        {
            var parts = kv.Split('=',2);
            if (parts.Length==2 && parts[0]=="linkCustomerId") linkCustomerId = Uri.UnescapeDataString(parts[1]);
        }
        try
        {
            var uri = await _oauthService.StartAsync(provider, linkCustomerId);
            var resp = req.CreateResponse(HttpStatusCode.Redirect);
            resp.Headers.Add("Location", uri.ToString());
            return resp;
        }
        catch (Exception ex)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = ex.Message });
            return bad;
        }
    }

    [Function("OAuthCallback")]
    public async Task<HttpResponseData> Callback([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "oauth/{provider}/callback")] HttpRequestData req, string provider)
    {
        string? code=null; string? state=null;
        var queryPairs = req.Url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        foreach (var kv in queryPairs)
        {
            var parts = kv.Split('=',2);
            if (parts.Length==2)
            {
                if (parts[0]=="code") code = Uri.UnescapeDataString(parts[1]);
                else if (parts[0]=="state") state = Uri.UnescapeDataString(parts[1]);
            }
        }
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = "Missing code/state" });
            return bad;
        }
        try
        {
            var (access, refresh) = await _oauthService.CompleteAsync(provider, state, code);
            var resp = req.CreateResponse(HttpStatusCode.OK);
            await resp.WriteAsJsonAsync(new { access_token = access, refresh_token = refresh, token_type = "Bearer" });
            return resp;
        }
        catch (Exception ex)
        {
            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
            await bad.WriteAsJsonAsync(new { error = ex.Message });
            return bad;
        }
    }
}