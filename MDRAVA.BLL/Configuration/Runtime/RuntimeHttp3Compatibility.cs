namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHttp3Compatibility(
    RuntimeListenerProtocols Protocols,
    bool ProtocolsValid,
    RuntimeHttp3Enablement EffectiveEnablement,
    bool EnablementValid,
    bool EnablementExplicitlyConfigured,
    bool ExplicitHttp3Requested)
{
    public static readonly IReadOnlyList<string> SupportedProtocolConfigValues =
    [
        "http1",
        "http2",
        "http1AndHttp2",
        "http3",
        "http1AndHttp3",
        "http2AndHttp3",
        "http1AndHttp2AndHttp3"
    ];

    public static readonly IReadOnlyList<string> SupportedEnablementConfigValues =
    [
        "default",
        "disabled"
    ];

    public static RuntimeHttp3Compatibility From(ListenerOptions listener)
    {
        var protocolsValid = TryParseProtocols(listener.Protocols, out var protocols);
        if (!protocolsValid)
        {
            protocols = RuntimeListenerProtocols.Http1;
        }

        var enablementValid = TryParseEnablement(
            listener.Http3Enablement,
            out var parsedEnablement,
            out var enablementExplicitlyConfigured);
        var effectiveEnablement = enablementValid && enablementExplicitlyConfigured
            ? parsedEnablement
            : RuntimeHttp3Enablement.Default;

        return new RuntimeHttp3Compatibility(
            protocols,
            protocolsValid,
            effectiveEnablement,
            enablementValid,
            enablementExplicitlyConfigured,
            protocols.HasHttp3());
    }

    public static bool TryParseProtocols(string? protocols, out RuntimeListenerProtocols parsed)
    {
        if (string.IsNullOrWhiteSpace(protocols))
        {
            parsed = RuntimeListenerProtocols.Http1;
            return true;
        }

        switch (protocols.Trim().ToLowerInvariant())
        {
            case "http1":
                parsed = RuntimeListenerProtocols.Http1;
                return true;
            case "http2":
                parsed = RuntimeListenerProtocols.Http2;
                return true;
            case "http1andhttp2":
                parsed = RuntimeListenerProtocols.Http1AndHttp2;
                return true;
            case "http3":
                parsed = RuntimeListenerProtocols.Http3;
                return true;
            case "http1andhttp3":
                parsed = RuntimeListenerProtocols.Http1AndHttp3;
                return true;
            case "http2andhttp3":
                parsed = RuntimeListenerProtocols.Http2AndHttp3;
                return true;
            case "http1andhttp2andhttp3":
                parsed = RuntimeListenerProtocols.Http1AndHttp2AndHttp3;
                return true;
            default:
                parsed = RuntimeListenerProtocols.None;
                return false;
        }
    }

    public static RuntimeListenerProtocols ParseProtocolsOrDefault(string? protocols)
    {
        return TryParseProtocols(protocols, out var parsed)
            ? parsed
            : RuntimeListenerProtocols.Http1;
    }

    public static bool TryParseEnablement(
        string? enablement,
        out RuntimeHttp3Enablement parsed,
        out bool explicitlyConfigured)
    {
        explicitlyConfigured = !string.IsNullOrWhiteSpace(enablement);
        if (!explicitlyConfigured)
        {
            parsed = RuntimeHttp3Enablement.Default;
            return true;
        }

        switch (enablement!.Trim().ToLowerInvariant())
        {
            case "default":
                parsed = RuntimeHttp3Enablement.Default;
                return true;
            case "disabled":
                parsed = RuntimeHttp3Enablement.Disabled;
                return true;
            default:
                parsed = RuntimeHttp3Enablement.Default;
                return false;
        }
    }

    public static RuntimeHttp3Enablement ResolveEffectiveEnablement(RuntimeHttp3Enablement configuredEnablement)
    {
        return configuredEnablement == RuntimeHttp3Enablement.Disabled
            ? RuntimeHttp3Enablement.Disabled
            : RuntimeHttp3Enablement.Default;
    }

    public static string MergeEnablementConfigText(string existing, string next)
    {
        if (!TryParseEnablement(existing, out var existingEnablement, out var existingExplicit)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.Trim();
        }

        if (!TryParseEnablement(next, out var nextEnablement, out var nextExplicit)
            && !string.IsNullOrWhiteSpace(next))
        {
            return next.Trim();
        }

        if (!existingExplicit && !nextExplicit)
        {
            return "";
        }

        return existingEnablement == RuntimeHttp3Enablement.Disabled || nextEnablement == RuntimeHttp3Enablement.Disabled
            ? RuntimeHttp3Enablement.Disabled.ToConfigText()
            : RuntimeHttp3Enablement.Default.ToConfigText();
    }
}
