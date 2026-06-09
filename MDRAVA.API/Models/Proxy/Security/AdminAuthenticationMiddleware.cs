using MDRAVA.API.Proxy.Metrics;
using MDRAVA.INF.Observability;

namespace MDRAVA.API.Proxy.Security;

public sealed class AdminAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly AdminAuditStore _auditStore;
    private readonly ProxyMetrics? _metrics;
    private readonly ILogger<AdminAuthenticationMiddleware> _logger;

    public AdminAuthenticationMiddleware(
        RequestDelegate next,
        IProxyConfigurationStore configurationStore,
        AdminAuditStore auditStore,
        ProxyMetrics? metrics,
        ILogger<AdminAuthenticationMiddleware> logger)
    {
        _next = next;
        _configurationStore = configurationStore;
        _auditStore = auditStore;
        _metrics = metrics;
        _logger = logger;
    }

    public AdminAuthenticationMiddleware(
        RequestDelegate next,
        IProxyConfigurationStore configurationStore,
        AdminAuditStore auditStore,
        ILogger<AdminAuthenticationMiddleware> logger)
        : this(next, configurationStore, auditStore, null, logger)
    {
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var security = ResolveSecurityOptions();
        var authDecision = ProxyAdminAuthenticationPolicy.Authenticate(new ProxyAdminAuthenticationInput(
            security.RequireAuthentication,
            security.Token,
            context.Request.Headers.Authorization.ToArray(),
            context.Request.Headers[ProxyAdminAuthenticationPolicy.AdminApiKeyHeaderName].ToArray()));
        var authResult = authDecision.Result;
        var requestAllowed = true;

        if (authDecision.AuthenticationRequired)
        {
            requestAllowed = authDecision.Allowed;
            if (!requestAllowed)
            {
                context.Response.StatusCode = authDecision.ShouldChallenge ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
                if (authDecision.ShouldChallenge)
                {
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                }

                await context.Response.WriteAsync("Admin authentication failed.", context.RequestAborted);
                _metrics?.AdminAuthFailed();
                AddAudit(context, authResult, context.Response.StatusCode, succeeded: false, security.RecentAuditCapacity);
                return;
            }

            _metrics?.AdminAuthSucceeded();
        }

        try
        {
            await _next(context);
            AddAudit(
                context,
                authResult,
                context.Response.StatusCode,
                succeeded: requestAllowed && context.Response.StatusCode < 500,
                security.RecentAuditCapacity);
        }
        catch
        {
            AddAudit(context, authResult, StatusCodes.Status500InternalServerError, succeeded: false, security.RecentAuditCapacity);
            throw;
        }
    }

    private RuntimeAdminSecurityOptions ResolveSecurityOptions()
    {
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
        {
            return snapshot.AdminSecurity;
        }

        _logger.LogWarning("Admin request arrived before an active proxy configuration snapshot was available.");
        var adminOptions = new ProxyAdminOptions();
        return ProxyConfigurationRuntimeMapper.ToRuntimeAdminSecurityOptions(
            adminOptions,
            ProxyAdminSecurityTokenPolicy.Resolve(adminOptions, Environment.GetEnvironmentVariable));
    }

    private void AddAudit(
        HttpContext context,
        string authResult,
        int statusCode,
        bool succeeded,
        int capacity)
    {
        var clientIp = ProxyClientAddressPolicy.NormalizeClientIp(context.Connection.RemoteIpAddress);
        var path = context.Request.Path.HasValue
            ? context.Request.Path.Value!
            : "/";

        _auditStore.Add(
            new ProxyAdminAuditEvent(
                DateTimeOffset.UtcNow,
                context.Request.Method,
                path,
                clientIp,
                authResult,
                statusCode,
                succeeded),
            capacity);
    }

}
