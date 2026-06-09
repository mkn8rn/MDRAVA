using System.Net;
using System.Security.Cryptography;
using System.Text;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.INF.Observability;
using Microsoft.Extensions.Primitives;

namespace MDRAVA.API.Proxy.Security;

public sealed class AdminAuthenticationMiddleware
{
    public const string AdminApiKeyHeaderName = "X-MDRAVA-Admin-Key";

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
        var authResult = "not-required";
        var requestAllowed = true;

        if (security.RequireAuthentication)
        {
            requestAllowed = TryAuthenticate(context.Request, security, out authResult);
            if (!requestAllowed)
            {
                context.Response.StatusCode = authResult == "missing" ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
                if (context.Response.StatusCode == StatusCodes.Status401Unauthorized)
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

    private static bool TryAuthenticate(
        HttpRequest request,
        RuntimeAdminSecurityOptions security,
        out string authResult)
    {
        if (string.IsNullOrEmpty(security.Token))
        {
            authResult = "not-configured";
            return false;
        }

        var presentedToken = ReadBearerToken(request.Headers.Authorization)
            ?? ReadApiKey(request.Headers[AdminApiKeyHeaderName]);

        if (presentedToken is null)
        {
            authResult = "missing";
            return false;
        }

        if (!FixedTimeEquals(security.Token, presentedToken))
        {
            authResult = "invalid";
            return false;
        }

        authResult = "valid";
        return true;
    }

    private static string? ReadBearerToken(StringValues authorizationValues)
    {
        foreach (var value in authorizationValues)
        {
            if (value is null)
            {
                continue;
            }

            const string prefix = "Bearer ";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[prefix.Length..].Trim();
                return token.Length == 0 ? null : token;
            }
        }

        return null;
    }

    private static string? ReadApiKey(StringValues values)
    {
        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private void AddAudit(
        HttpContext context,
        string authResult,
        int statusCode,
        bool succeeded,
        int capacity)
    {
        var clientIp = NormalizeClientIp(context.Connection.RemoteIpAddress);
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

    private static string? NormalizeClientIp(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4().ToString() : address.ToString();
    }
}
