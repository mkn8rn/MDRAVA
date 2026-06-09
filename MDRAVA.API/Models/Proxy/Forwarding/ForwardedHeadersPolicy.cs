using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Forwarding;

public sealed class ForwardedHeadersPolicy
{
    private static readonly string[] ForwardedHeaderNames =
    [
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Forwarded-Port",
        "Forwarded"
    ];

    public ForwardedHeadersContext Build(
        Http1RequestHead requestHead,
        RuntimeListener listener,
        RuntimeForwardedHeadersOptions options,
        IPEndPoint? remoteEndPoint)
    {
        var remoteAddress = remoteEndPoint?.Address;
        var normalizedRemoteAddress = NormalizeAddress(remoteAddress);
        if (!options.Enabled)
        {
            return new ForwardedHeadersContext(
                normalizedRemoteAddress,
                normalizedRemoteAddress?.ToString() ?? remoteEndPoint?.ToString(),
                []);
        }

        var immediatePeerTrusted = normalizedRemoteAddress is not null
            && options.TrustedProxies.Any(proxy => proxy.Contains(normalizedRemoteAddress));

        var incomingFor = immediatePeerTrusted
            ? SplitHeaderValues(requestHead.Headers, "X-Forwarded-For")
            : [];
        var resolvedClientIp = TryParseForwardedFor(incomingFor, out var parsedForwardedFor)
            ? parsedForwardedFor
            : normalizedRemoteAddress;

        List<string> forwardedFor = [];
        if (immediatePeerTrusted)
        {
            forwardedFor.AddRange(incomingFor);
        }

        if (normalizedRemoteAddress is not null)
        {
            forwardedFor.Add(normalizedRemoteAddress.ToString());
        }

        var forwardedHost = immediatePeerTrusted
            ? FirstHeaderValue(requestHead.Headers, "X-Forwarded-Host") ?? requestHead.Host
            : requestHead.Host;
        var forwardedProto = immediatePeerTrusted
            ? FirstHeaderValue(requestHead.Headers, "X-Forwarded-Proto") ?? Scheme(listener)
            : Scheme(listener);
        var forwardedPort = immediatePeerTrusted
            ? FirstHeaderValue(requestHead.Headers, "X-Forwarded-Port") ?? listener.Port.ToString(CultureInfo.InvariantCulture)
            : listener.Port.ToString(CultureInfo.InvariantCulture);

        var headers = new List<Http1HeaderField>
        {
            new("X-Forwarded-For", string.Join(", ", forwardedFor)),
            new("X-Forwarded-Host", forwardedHost),
            new("X-Forwarded-Proto", forwardedProto),
            new("X-Forwarded-Port", forwardedPort)
        };

        var standardFor = resolvedClientIp?.ToString() ?? "unknown";
        headers.Add(new Http1HeaderField(
            "Forwarded",
            $"for={QuoteForwardedValue(FormatForwardedFor(standardFor))};proto={QuoteForwardedValue(forwardedProto)};host={QuoteForwardedValue(forwardedHost)}"));

        return new ForwardedHeadersContext(
            resolvedClientIp,
            resolvedClientIp?.ToString() ?? remoteEndPoint?.ToString(),
            headers);
    }

    public static bool IsForwardedHeader(string headerName)
    {
        return ForwardedHeaderNames.Any(name => string.Equals(name, headerName, StringComparison.OrdinalIgnoreCase));
    }

    private static IPAddress? NormalizeAddress(IPAddress? address)
    {
        return address?.IsIPv4MappedToIPv6 == true ? address.MapToIPv4() : address;
    }

    private static bool TryParseForwardedFor(IReadOnlyList<string> forwardedFor, [NotNullWhen(true)] out IPAddress? address)
    {
        address = null;
        foreach (var entry in forwardedFor)
        {
            var value = entry.Trim().Trim('"');
            if (value.Length == 0)
            {
                continue;
            }

            if (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal))
            {
                value = value[1..^1];
            }

            if (IPAddress.TryParse(value, out var parsed))
            {
                address = NormalizeAddress(parsed)!;
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> SplitHeaderValues(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        List<string> values = [];
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (!ContainsControl(value))
                {
                    values.Add(value);
                }
            }
        }

        return values;
    }

    private static string? FirstHeaderValue(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(header.Value)
                && !ContainsControl(header.Value))
            {
                return header.Value.Trim();
            }
        }

        return null;
    }

    private static bool ContainsControl(string value)
    {
        return value.Any(char.IsControl);
    }

    private static string Scheme(RuntimeListener listener)
    {
        return listener.Transport == RuntimeListenerTransport.Https ? "https" : "http";
    }

    private static string FormatForwardedFor(string value)
    {
        return value.Contains(":", StringComparison.Ordinal) && !value.StartsWith("[", StringComparison.Ordinal)
            ? $"[{value}]"
            : value;
    }

    private static string QuoteForwardedValue(string value)
    {
        var sanitized = value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

        return $"\"{sanitized}\"";
    }
}
