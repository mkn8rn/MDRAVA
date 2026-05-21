namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHttp3Compatibility(
    RuntimeListenerProtocols Protocols,
    bool ProtocolsValid,
    bool StableProtocolAliasUsed,
    bool LegacyProtocolAliasUsed,
    RuntimeHttp3Enablement EffectiveEnablement,
    bool EnablementValid,
    bool EnablementExplicitlyConfigured,
    bool LegacyEnablementAliasUsed,
    bool ExplicitHttp3Requested,
    bool LegacyBufferedRequestBodyLimitConfigured)
{
    public bool LegacyAliasUsed => LegacyProtocolAliasUsed || LegacyEnablementAliasUsed;

    public static readonly IReadOnlyList<string> SupportedProtocolConfigValues =
    [
        "http1",
        "http2",
        "http1AndHttp2",
        "http3",
        "http1AndHttp3",
        "http2AndHttp3",
        "http1AndHttp2AndHttp3",
        "http3Preview",
        "http1AndHttp3Preview",
        "http2AndHttp3Preview",
        "http1AndHttp2AndHttp3Preview"
    ];

    public static readonly IReadOnlyList<string> SupportedEnablementConfigValues =
    [
        "default",
        "disabled",
        "preview",
        "beta"
    ];

    public static RuntimeHttp3Compatibility From(ListenerOptions listener)
    {
        var protocolsValid = TryParseProtocols(
            listener.Protocols,
            out var protocols,
            out var stableProtocolAliasUsed,
            out var legacyProtocolAliasUsed);
        if (!protocolsValid)
        {
            protocols = RuntimeListenerProtocols.Http1;
        }

        var enablementValid = TryParseEnablement(
            listener.Http3Enablement,
            out var parsedEnablement,
            out var enablementExplicitlyConfigured,
            out var legacyEnablementAliasUsed);
        var effectiveEnablement = enablementValid && enablementExplicitlyConfigured
            ? parsedEnablement
            : ResolveImplicitEnablement(protocols, listener.ExperimentalHttp3);

        return new RuntimeHttp3Compatibility(
            protocols,
            protocolsValid,
            stableProtocolAliasUsed,
            legacyProtocolAliasUsed,
            effectiveEnablement,
            enablementValid,
            enablementExplicitlyConfigured,
            legacyEnablementAliasUsed,
            IsExplicitHttp3Requested(protocols, effectiveEnablement),
            listener.Http3MaxBufferedRequestBodyBytes > 0);
    }

    public static bool TryParseProtocols(
        string? protocols,
        out RuntimeListenerProtocols parsed,
        out bool stableHttp3AliasUsed,
        out bool legacyHttp3AliasUsed)
    {
        stableHttp3AliasUsed = false;
        legacyHttp3AliasUsed = false;
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
                stableHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http3Preview;
                return true;
            case "http1andhttp3":
                stableHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http1AndHttp3Preview;
                return true;
            case "http2andhttp3":
                stableHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http2AndHttp3Preview;
                return true;
            case "http1andhttp2andhttp3":
                stableHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview;
                return true;
            case "http3preview":
                legacyHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http3Preview;
                return true;
            case "http1andhttp3preview":
                legacyHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http1AndHttp3Preview;
                return true;
            case "http2andhttp3preview":
                legacyHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http2AndHttp3Preview;
                return true;
            case "http1andhttp2andhttp3preview":
                legacyHttp3AliasUsed = true;
                parsed = RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview;
                return true;
            default:
                parsed = RuntimeListenerProtocols.None;
                return false;
        }
    }

    public static bool TryParseProtocols(string? protocols, out RuntimeListenerProtocols parsed)
    {
        return TryParseProtocols(protocols, out parsed, out _, out _);
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
        out bool explicitlyConfigured,
        out bool legacyAliasUsed)
    {
        explicitlyConfigured = !string.IsNullOrWhiteSpace(enablement);
        legacyAliasUsed = false;
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
            case "preview":
                legacyAliasUsed = true;
                parsed = RuntimeHttp3Enablement.Preview;
                return true;
            case "beta":
                legacyAliasUsed = true;
                parsed = RuntimeHttp3Enablement.Beta;
                return true;
            default:
                parsed = RuntimeHttp3Enablement.Default;
                return false;
        }
    }

    public static RuntimeHttp3Enablement ResolveEffectiveEnablement(
        RuntimeListenerProtocols protocols,
        bool experimentalHttp3,
        RuntimeHttp3Enablement configuredEnablement)
    {
        return configuredEnablement switch
        {
            RuntimeHttp3Enablement.Disabled => RuntimeHttp3Enablement.Disabled,
            RuntimeHttp3Enablement.Preview or RuntimeHttp3Enablement.Beta => configuredEnablement,
            _ => ResolveImplicitEnablement(protocols, experimentalHttp3)
        };
    }

    public static bool IsExplicitHttp3Requested(
        RuntimeListenerProtocols protocols,
        RuntimeHttp3Enablement enablement)
    {
        return protocols.HasHttp3()
            || enablement is RuntimeHttp3Enablement.Preview or RuntimeHttp3Enablement.Beta;
    }

    public static string MergeEnablementConfigText(string existing, string next)
    {
        if (!TryParseEnablement(existing, out var existingEnablement, out var existingExplicit, out _)
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.Trim();
        }

        if (!TryParseEnablement(next, out var nextEnablement, out var nextExplicit, out _)
            && !string.IsNullOrWhiteSpace(next))
        {
            return next.Trim();
        }

        if (!existingExplicit && !nextExplicit)
        {
            return "";
        }

        var merged = MergeEnablement(existingExplicit ? existingEnablement : RuntimeHttp3Enablement.Default, nextExplicit ? nextEnablement : RuntimeHttp3Enablement.Default);
        return merged.ToConfigText();
    }

    private static RuntimeHttp3Enablement ResolveImplicitEnablement(
        RuntimeListenerProtocols protocols,
        bool experimentalHttp3)
    {
        return protocols.HasHttp3() && experimentalHttp3
            ? RuntimeHttp3Enablement.Preview
            : RuntimeHttp3Enablement.Default;
    }

    private static RuntimeHttp3Enablement MergeEnablement(
        RuntimeHttp3Enablement existing,
        RuntimeHttp3Enablement next)
    {
        if (existing == RuntimeHttp3Enablement.Disabled || next == RuntimeHttp3Enablement.Disabled)
        {
            return RuntimeHttp3Enablement.Disabled;
        }

        if (existing == RuntimeHttp3Enablement.Beta || next == RuntimeHttp3Enablement.Beta)
        {
            return RuntimeHttp3Enablement.Beta;
        }

        if (existing == RuntimeHttp3Enablement.Preview || next == RuntimeHttp3Enablement.Preview)
        {
            return RuntimeHttp3Enablement.Preview;
        }

        return RuntimeHttp3Enablement.Default;
    }
}
