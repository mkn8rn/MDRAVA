namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsMaintenancePolicy
{
    public ProxyRouteDiagnosticsMaintenancePolicy(
        bool Enabled,
        int? RetryAfterSeconds,
        string ContentType,
        string Body)
    {
        ArgumentNullException.ThrowIfNull(ContentType);
        ArgumentNullException.ThrowIfNull(Body);

        this.Enabled = Enabled;
        this.RetryAfterSeconds = RetryAfterSeconds;
        this.ContentType = ContentType;
        this.Body = Body;
    }

    public bool Enabled { get; }

    public int? RetryAfterSeconds { get; }

    public string ContentType { get; }

    public string Body { get; }
}

public sealed record ProxyRouteDiagnosticsHttpsRedirectPolicy(
    bool Enabled,
    int StatusCode,
    int? HttpsPort);

public sealed record ProxyRouteDiagnosticsCanonicalHostPolicy
{
    public ProxyRouteDiagnosticsCanonicalHostPolicy(
        bool Enabled,
        string TargetHost,
        int StatusCode)
    {
        ArgumentNullException.ThrowIfNull(TargetHost);

        this.Enabled = Enabled;
        this.TargetHost = TargetHost;
        this.StatusCode = StatusCode;
    }

    public bool Enabled { get; }

    public string TargetHost { get; }

    public int StatusCode { get; }
}

public sealed record ProxyRouteDiagnosticsRedirectPolicy
{
    public ProxyRouteDiagnosticsRedirectPolicy(
        int StatusCode,
        string TargetUrl,
        string TargetPath,
        bool PreserveQuery)
    {
        ArgumentNullException.ThrowIfNull(TargetUrl);
        ArgumentNullException.ThrowIfNull(TargetPath);

        this.StatusCode = StatusCode;
        this.TargetUrl = TargetUrl;
        this.TargetPath = TargetPath;
        this.PreserveQuery = PreserveQuery;
    }

    public int StatusCode { get; }

    public string TargetUrl { get; }

    public string TargetPath { get; }

    public bool PreserveQuery { get; }
}

public sealed record ProxyRouteDiagnosticsStaticResponse
{
    public ProxyRouteDiagnosticsStaticResponse(
        int StatusCode,
        string ContentType,
        string Body)
    {
        ArgumentNullException.ThrowIfNull(ContentType);
        ArgumentNullException.ThrowIfNull(Body);

        this.StatusCode = StatusCode;
        this.ContentType = ContentType;
        this.Body = Body;
    }

    public int StatusCode { get; }

    public string ContentType { get; }

    public string Body { get; }
}

public sealed record ProxyRouteDiagnosticsPathRewrite
{
    public ProxyRouteDiagnosticsPathRewrite(
        string StripPrefix,
        string ReplacePrefix,
        string Replacement)
    {
        ArgumentNullException.ThrowIfNull(StripPrefix);
        ArgumentNullException.ThrowIfNull(ReplacePrefix);
        ArgumentNullException.ThrowIfNull(Replacement);

        this.StripPrefix = StripPrefix;
        this.ReplacePrefix = ReplacePrefix;
        this.Replacement = Replacement;
    }

    public string StripPrefix { get; }

    public string ReplacePrefix { get; }

    public string Replacement { get; }
}
