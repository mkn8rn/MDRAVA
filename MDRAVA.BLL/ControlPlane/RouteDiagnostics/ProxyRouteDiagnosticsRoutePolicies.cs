namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsMaintenancePolicy(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body);

public sealed record ProxyRouteDiagnosticsHttpsRedirectPolicy(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);

public sealed record ProxyRouteDiagnosticsCanonicalHostPolicy(
    bool Enabled,
    string TargetHost,
    int StatusCode);

public sealed record ProxyRouteDiagnosticsRedirectPolicy(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery);

public sealed record ProxyRouteDiagnosticsStaticResponse(
    int StatusCode,
    string ContentType,
    string Body);

public sealed record ProxyRouteDiagnosticsPathRewrite(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement);
