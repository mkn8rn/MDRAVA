using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public interface IProxyRouteDiagnosticsConfigurationSource
{
    bool TryRead(out IProxyRouteDiagnosticsConfigurationSnapshot? snapshot);
}

public interface IProxyRouteDiagnosticsConfigurationSnapshot
{
    IReadOnlyList<IProxyRouteDiagnosticsListener> Listeners { get; }

    IReadOnlyList<IProxyRouteDiagnosticsRoute> Routes { get; }
}

public interface IProxyRouteDiagnosticsMatcher
{
    IProxyRouteDiagnosticsRoute? Match(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot,
        ProxyRouteDiagnosticsRequestHead requestHead);
}

public interface IProxyRouteDiagnosticsActionPolicy
{
    ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest);
}

public interface IProxyRouteDiagnosticsPathRewritePolicy
{
    string Apply(IProxyRouteDiagnosticsRoute route, string target, string path);
}

public interface IProxyRouteDiagnosticsMetricsSink
{
    void RouteMatchDryRun(string? failureReason);
}

public interface IProxyRouteDiagnosticsListener
{
    string Name { get; }

    string Transport { get; }

    string Address { get; }

    int Port { get; }

    bool Enabled { get; }

    RuntimeListenerProtocols Protocols { get; }

    bool Http3EnabledForTraffic { get; }
}

public interface IProxyRouteDiagnosticsRoute
{
    string SiteName { get; }

    string Name { get; }

    string Host { get; }

    string PathPrefix { get; }

    string Action { get; }

    bool MaintenanceEnabled { get; }

    long MaxRequestBodyBytes { get; }

    IReadOnlyList<IProxyRouteDiagnosticsUpstream> Upstreams { get; }

    bool CacheEnabled { get; }

    IReadOnlyList<string> CacheMethods { get; }

    bool RetryEnabled { get; }

    IReadOnlyList<string> RetryMethods { get; }
}

public interface IProxyRouteDiagnosticsUpstream
{
    string Name { get; }

    string Scheme { get; }

    string Protocol { get; }

    string Endpoint { get; }

    int Weight { get; }

    bool CircuitBreakerEnabled { get; }
}

public sealed record ProxyRouteDiagnosticsHeader(string Name, string Value);

public sealed class ProxyRouteDiagnosticsRequestHead
{
    public ProxyRouteDiagnosticsRequestHead(
        string method,
        string target,
        string path,
        string version,
        string host,
        ProxyRouteDiagnosticsRequestFraming framing,
        IReadOnlyList<ProxyRouteDiagnosticsHeader> headers)
    {
        Method = method;
        Target = target;
        Path = path;
        Version = version;
        Host = host;
        Framing = framing;
        Headers = headers;
    }

    public string Method { get; }

    public string Target { get; }

    public string Path { get; }

    public string Version { get; }

    public string Host { get; }

    public ProxyRouteDiagnosticsRequestFraming Framing { get; }

    public IReadOnlyList<ProxyRouteDiagnosticsHeader> Headers { get; }
}

public sealed record ProxyRouteDiagnosticsRequestFraming(
    ProxyRouteDiagnosticsBodyKind Kind,
    long? ContentLength)
{
    public static ProxyRouteDiagnosticsRequestFraming None { get; } = new(ProxyRouteDiagnosticsBodyKind.None, null);

    public static ProxyRouteDiagnosticsRequestFraming Chunked { get; } = new(ProxyRouteDiagnosticsBodyKind.Chunked, null);

    public static ProxyRouteDiagnosticsRequestFraming FromContentLength(long contentLength)
    {
        return contentLength == 0
            ? None
            : new ProxyRouteDiagnosticsRequestFraming(ProxyRouteDiagnosticsBodyKind.ContentLength, contentLength);
    }
}

public enum ProxyRouteDiagnosticsBodyKind
{
    None,
    ContentLength,
    Chunked,
    CloseDelimited
}

public sealed record ProxyRouteDiagnosticsActionDecision(
    bool ShouldProxy,
    int? GeneratedStatusCode);
