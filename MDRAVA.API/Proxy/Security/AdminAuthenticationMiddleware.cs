using MDRAVA.BLL.ControlPlane.AdminAuthentication;
namespace MDRAVA.API.Proxy.Security;

public sealed class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ProxyAdminAuthenticationService _authentication;

    public AdminAuthenticationMiddleware(
        RequestDelegate next,
        ProxyAdminAuthenticationService authentication)
    {
        _next = next;
        _authentication = authentication;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var input = ProxyAdminRequestAuthenticationInputMapper.FromRawRequestFacts(
            context.Request.Method,
            context.Request.Path.Value,
            context.Connection.RemoteIpAddress,
            context.Request.Headers.Authorization.ToArray(),
            context.Request.Headers[ProxyAdminAuthenticationPolicy.AdminApiKeyHeaderName].ToArray());
        var outcome = _authentication.Authenticate(input);
        if (!outcome.Allowed)
        {
            context.Response.StatusCode = outcome.DeniedStatusCode!.Value;
            if (outcome.ShouldChallenge)
            {
                context.Response.Headers.WWWAuthenticate = "Bearer";
            }

            await context.Response.WriteAsync(
                ProxyAdminAuthenticationService.FailureResponseBody,
                context.RequestAborted);
            return;
        }

        try
        {
            await _next(context);
            _authentication.RecordCompleted(input, outcome, context.Response.StatusCode);
        }
        catch
        {
            _authentication.RecordFailed(input, outcome);
            throw;
        }
    }
}
