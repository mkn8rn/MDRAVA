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
        var protocolParsing = ParseProtocols(listener.Protocols);
        var protocolsValid = protocolParsing is RuntimeListenerProtocolParseResult.AcceptedResult;
        var protocols = protocolParsing is RuntimeListenerProtocolParseResult.AcceptedResult acceptedProtocols
            ? acceptedProtocols.Protocols
            : RuntimeListenerProtocols.Http1;

        var enablementParsing = ParseEnablement(listener.Http3Enablement);
        var enablementValid = enablementParsing is RuntimeHttp3EnablementParseResult.AcceptedResult;
        var parsedEnablement = enablementParsing is RuntimeHttp3EnablementParseResult.AcceptedResult acceptedEnablement
            ? acceptedEnablement.Enablement
            : RuntimeHttp3Enablement.Default;
        var enablementExplicitlyConfigured = enablementParsing switch
        {
            RuntimeHttp3EnablementParseResult.AcceptedResult accepted => accepted.ExplicitlyConfigured,
            RuntimeHttp3EnablementParseResult.RejectedResult rejected => rejected.ExplicitlyConfigured,
            _ => false
        };
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

    public static RuntimeListenerProtocolParseResult ParseProtocols(string? protocols)
    {
        if (string.IsNullOrWhiteSpace(protocols))
        {
            return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http1);
        }

        switch (protocols.Trim().ToLowerInvariant())
        {
            case "http1":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http1);
            case "http2":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http2);
            case "http1andhttp2":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http1AndHttp2);
            case "http3":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http3);
            case "http1andhttp3":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http1AndHttp3);
            case "http2andhttp3":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http2AndHttp3);
            case "http1andhttp2andhttp3":
                return RuntimeListenerProtocolParseResult.Accepted(RuntimeListenerProtocols.Http1AndHttp2AndHttp3);
            default:
                return RuntimeListenerProtocolParseResult.Rejected;
        }
    }

    public static RuntimeListenerProtocols ParseProtocolsOrDefault(string? protocols)
    {
        return ParseProtocols(protocols) is RuntimeListenerProtocolParseResult.AcceptedResult accepted
            ? accepted.Protocols
            : RuntimeListenerProtocols.Http1;
    }

    public static RuntimeHttp3EnablementParseResult ParseEnablement(string? enablement)
    {
        var explicitlyConfigured = !string.IsNullOrWhiteSpace(enablement);
        if (!explicitlyConfigured)
        {
            return RuntimeHttp3EnablementParseResult.Accepted(
                RuntimeHttp3Enablement.Default,
                explicitlyConfigured: false);
        }

        switch (enablement!.Trim().ToLowerInvariant())
        {
            case "default":
                return RuntimeHttp3EnablementParseResult.Accepted(
                    RuntimeHttp3Enablement.Default,
                    explicitlyConfigured: true);
            case "disabled":
                return RuntimeHttp3EnablementParseResult.Accepted(
                    RuntimeHttp3Enablement.Disabled,
                    explicitlyConfigured: true);
            default:
                return RuntimeHttp3EnablementParseResult.Rejected(explicitlyConfigured: true);
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
        var existingParsing = ParseEnablement(existing);
        if (existingParsing is RuntimeHttp3EnablementParseResult.RejectedResult
            && !string.IsNullOrWhiteSpace(existing))
        {
            return existing.Trim();
        }

        var nextParsing = ParseEnablement(next);
        if (nextParsing is RuntimeHttp3EnablementParseResult.RejectedResult
            && !string.IsNullOrWhiteSpace(next))
        {
            return next.Trim();
        }

        var existingAccepted = (RuntimeHttp3EnablementParseResult.AcceptedResult)existingParsing;
        var nextAccepted = (RuntimeHttp3EnablementParseResult.AcceptedResult)nextParsing;
        var existingEnablement = existingAccepted.Enablement;
        var nextEnablement = nextAccepted.Enablement;
        var existingExplicit = existingAccepted.ExplicitlyConfigured;
        var nextExplicit = nextAccepted.ExplicitlyConfigured;
        if (!existingExplicit && !nextExplicit)
        {
            return "";
        }

        return existingEnablement == RuntimeHttp3Enablement.Disabled || nextEnablement == RuntimeHttp3Enablement.Disabled
            ? RuntimeHttp3Enablement.Disabled.ToConfigText()
            : RuntimeHttp3Enablement.Default.ToConfigText();
    }
}

public abstract record RuntimeListenerProtocolParseResult
{
    private RuntimeListenerProtocolParseResult()
    {
    }

    public static RuntimeListenerProtocolParseResult Rejected { get; } = new RejectedResult();

    public static RuntimeListenerProtocolParseResult Accepted(RuntimeListenerProtocols protocols)
    {
        return new AcceptedResult(protocols);
    }

    public sealed record AcceptedResult(RuntimeListenerProtocols Protocols)
        : RuntimeListenerProtocolParseResult;

    private sealed record RejectedResult : RuntimeListenerProtocolParseResult;
}

public abstract record RuntimeHttp3EnablementParseResult
{
    private RuntimeHttp3EnablementParseResult()
    {
    }

    public static RuntimeHttp3EnablementParseResult Accepted(
        RuntimeHttp3Enablement enablement,
        bool explicitlyConfigured)
    {
        return new AcceptedResult(enablement, explicitlyConfigured);
    }

    public static RuntimeHttp3EnablementParseResult Rejected(bool explicitlyConfigured)
    {
        return new RejectedResult(explicitlyConfigured);
    }

    public sealed record AcceptedResult(
        RuntimeHttp3Enablement Enablement,
        bool ExplicitlyConfigured)
        : RuntimeHttp3EnablementParseResult;

    public sealed record RejectedResult(bool ExplicitlyConfigured)
        : RuntimeHttp3EnablementParseResult;
}
