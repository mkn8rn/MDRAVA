using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateListeners(
        List<string> failures,
        ProxyOptions options,
        IProxyEndpointAddressPolicy endpointAddressPolicy)
    {
        if (options.Listeners.Count == 0)
        {
            failures.Add("Proxy:Listeners must contain at least one listener.");
        }

        HashSet<string> listenerNames = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> listenerBinds = new(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < options.Listeners.Count; index++)
        {
            var listener = options.Listeners[index];
            var prefix = $"Proxy:Listeners:{index}";

            if (string.IsNullOrWhiteSpace(listener.Name))
            {
                failures.Add($"{prefix}:Name is required.");
            }
            else if (!listenerNames.Add(listener.Name))
            {
                failures.Add($"{prefix}:Name '{listener.Name}' is duplicated.");
            }

            if (!endpointAddressPolicy.IsListenerAddress(listener.Address))
            {
                failures.Add($"{prefix}:Address must be an IP address for Phase 1.");
            }

            var isHttp = string.Equals(listener.Transport, "http", StringComparison.OrdinalIgnoreCase);
            var isHttps = string.Equals(listener.Transport, "https", StringComparison.OrdinalIgnoreCase);
            if (!isHttp && !isHttps)
            {
                failures.Add($"{prefix}:Transport must be 'http' or 'https'.");
            }

            var http3Compatibility = RuntimeHttp3Compatibility.From(listener);
            var listenerProtocols = http3Compatibility.Protocols;
            if (!http3Compatibility.ProtocolsValid)
            {
                failures.Add($"{prefix}:Protocols must be {SupportedListenerProtocolsText()}.");
            }
            else if (listenerProtocols.HasFlag(RuntimeListenerProtocols.Http2) && !isHttps)
            {
                failures.Add($"{prefix}:HTTP/2 requires an HTTPS listener with ALPN; h2c is not supported.");
            }

            var http3Enablement = http3Compatibility.EffectiveEnablement;
            var explicitHttp3Requested = http3Compatibility.ExplicitHttp3Requested;
            if (explicitHttp3Requested)
            {
                if (http3Enablement == RuntimeHttp3Enablement.Disabled)
                {
                    failures.Add($"{prefix}:HTTP/3 protocols cannot be combined with Http3Enablement 'disabled'.");
                }

                if (!isHttps)
                {
                    failures.Add($"{prefix}:HTTP/3 requires an HTTPS listener; QUIC TLS over plaintext is not supported.");
                }

                if (string.IsNullOrWhiteSpace(listener.DefaultCertificateId)
                    && listener.SniCertificates.Count == 0)
                {
                    failures.Add($"{prefix}:HTTP/3 requires DefaultCertificateId or SniCertificates so QUIC TLS can use the certificate registry.");
                }
            }

            if (!http3Compatibility.EnablementValid)
            {
                failures.Add($"{prefix}:Http3Enablement must be {SupportedHttp3EnablementsText()} when configured.");
            }

            if (listener.Http3AltSvcMaxAgeSeconds is < 0 or > 31536000)
            {
                failures.Add($"{prefix}:Http3AltSvcMaxAgeSeconds must be between 0 and 31536000.");
            }

            if (listener.Http3AltSvcEnabled
                && string.Equals(listener.Http3Enablement, "disabled", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{prefix}:Http3AltSvcEnabled cannot be true when Http3Enablement is 'disabled'.");
            }

            if (listener.Port is < 1 or > 65535)
            {
                failures.Add($"{prefix}:Port must be between 1 and 65535.");
            }

            if (listener.Backlog < 1)
            {
                failures.Add($"{prefix}:Backlog must be greater than zero.");
            }

            if (listener.MaxRequestHeadBytes is < 1024 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:MaxRequestHeadBytes must be between 1024 and 1048576.");
            }

            if (listener.ForwardingBufferBytes is < 4096 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:ForwardingBufferBytes must be between 4096 and 1048576.");
            }

            if (listener.MaxResponseHeadBytes is < 1024 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:MaxResponseHeadBytes must be between 1024 and 1048576.");
            }

            if (listener.MaxChunkLineBytes is < 64 or > 16 * 1024)
            {
                failures.Add($"{prefix}:MaxChunkLineBytes must be between 64 and 16384.");
            }

            if (listener.Http2MaxConcurrentStreams is < 1 or > 1000)
            {
                failures.Add($"{prefix}:Http2MaxConcurrentStreams must be between 1 and 1000.");
            }

            if (listener.Http2MaxHeaderListBytes is < 1024 or > 1024 * 1024)
            {
                failures.Add($"{prefix}:Http2MaxHeaderListBytes must be between 1024 and 1048576.");
            }

            if (listener.Http2MaxFrameSize is < 16 * 1024 or > 16 * 1024 * 1024 - 1)
            {
                failures.Add($"{prefix}:Http2MaxFrameSize must be between 16384 and 16777215.");
            }

            var bindKey = $"{listener.Address.Trim().ToLowerInvariant()}|{listener.Port}|{listener.Transport.Trim().ToLowerInvariant()}";
            if (listener.Enabled && !listenerBinds.Add(bindKey))
            {
                failures.Add($"{prefix}:Listener bind {listener.Address}:{listener.Port}/{listener.Transport} is duplicated.");
            }
        }

        if (options.Listeners.Count > 0 && !options.Listeners.Any(static listener => listener.Enabled))
        {
            failures.Add("Proxy:Listeners must contain at least one enabled listener.");
        }
    }
}
