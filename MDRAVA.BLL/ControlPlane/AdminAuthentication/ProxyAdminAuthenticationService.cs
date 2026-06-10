using System.Net;
using MDRAVA.BLL.ControlPlane.AdminAudit;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.AdminAuthentication;

public sealed class ProxyAdminAuthenticationService
{
    public const int UnauthorizedStatusCode = 401;
    public const int ForbiddenStatusCode = 403;
    public const int InternalServerErrorStatusCode = 500;
    public const string FailureResponseBody = "Admin authentication failed.";

    private readonly IProxyAdminSecurityOptionsReader _securityReader;
    private readonly IProxyAdminAuditRecorder _auditRecorder;
    private readonly ProxyMetrics _metrics;
    private readonly IProxyAdminAuthenticationEventSink _events;

    public ProxyAdminAuthenticationService(
        IProxyAdminSecurityOptionsReader securityReader,
        IProxyAdminAuditRecorder auditRecorder,
        ProxyMetrics metrics,
        IProxyAdminAuthenticationEventSink events)
    {
        _securityReader = securityReader;
        _auditRecorder = auditRecorder;
        _metrics = metrics;
        _events = events;
    }

    public ProxyAdminAuthenticationOutcome Authenticate(ProxyAdminRequestAuthenticationInput input)
    {
        var securityResult = _securityReader.Read();
        if (!securityResult.HasActiveConfiguration)
        {
            _events.ActiveConfigurationMissing();
        }

        var security = securityResult.Security;
        var decision = ProxyAdminAuthenticationPolicy.Authenticate(new ProxyAdminAuthenticationInput(
            security.RequireAuthentication,
            security.Token,
            input.AuthorizationHeaders,
            input.ApiKeyHeaders));
        if (decision.AuthenticationRequired && !decision.Allowed)
        {
            _metrics.AdminAuthFailed();
            var deniedStatusCode = decision.ShouldChallenge ? UnauthorizedStatusCode : ForbiddenStatusCode;
            RecordAudit(input, decision.Result, deniedStatusCode, succeeded: false, security.RecentAuditCapacity);
            return new ProxyAdminAuthenticationOutcome(
                false,
                decision.Result,
                security.RecentAuditCapacity,
                decision.ShouldChallenge,
                deniedStatusCode);
        }

        if (decision.AuthenticationRequired)
        {
            _metrics.AdminAuthSucceeded();
        }

        return new ProxyAdminAuthenticationOutcome(
            true,
            decision.Result,
            security.RecentAuditCapacity,
            ShouldChallenge: false,
            DeniedStatusCode: null);
    }

    public void RecordCompleted(
        ProxyAdminRequestAuthenticationInput input,
        ProxyAdminAuthenticationOutcome outcome,
        int statusCode)
    {
        RecordAudit(
            input,
            outcome.AuthResult,
            statusCode,
            succeeded: outcome.Allowed && statusCode < InternalServerErrorStatusCode,
            outcome.RecentAuditCapacity);
    }

    public void RecordFailed(
        ProxyAdminRequestAuthenticationInput input,
        ProxyAdminAuthenticationOutcome outcome)
    {
        RecordAudit(
            input,
            outcome.AuthResult,
            InternalServerErrorStatusCode,
            succeeded: false,
            outcome.RecentAuditCapacity);
    }

    private void RecordAudit(
        ProxyAdminRequestAuthenticationInput input,
        string authResult,
        int statusCode,
        bool succeeded,
        int capacity)
    {
        _auditRecorder.Add(
            new ProxyAdminAuditEvent(
                DateTimeOffset.UtcNow,
                input.Method,
                string.IsNullOrEmpty(input.Path) ? "/" : input.Path,
                ProxyClientAddressPolicy.NormalizeClientIp(input.RemoteIpAddress),
                authResult,
                statusCode,
                succeeded),
            capacity);
    }
}

public sealed record ProxyAdminRequestAuthenticationInput(
    string Method,
    string Path,
    IPAddress? RemoteIpAddress,
    IReadOnlyList<string?> AuthorizationHeaders,
    IReadOnlyList<string?> ApiKeyHeaders);

public sealed record ProxyAdminAuthenticationOutcome(
    bool Allowed,
    string AuthResult,
    int RecentAuditCapacity,
    bool ShouldChallenge,
    int? DeniedStatusCode);
