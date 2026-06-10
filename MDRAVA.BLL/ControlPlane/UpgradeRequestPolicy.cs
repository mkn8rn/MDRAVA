using MDRAVA.BLL.ControlPlane.Http1;
namespace MDRAVA.BLL.ControlPlane;

public sealed class UpgradeRequestPolicy
{
    public bool IsUpgradeRequest(Http1RequestHead requestHead)
    {
        return HopByHopHeaderPolicy.HasConnectionToken(requestHead.Headers, "upgrade")
            || HasHeader(requestHead.Headers, "Upgrade");
    }

    public bool TryValidate(
        Http1RequestHead requestHead,
        out UpgradeRequestInfo? upgrade,
        out string rejectionReason)
    {
        upgrade = null;
        rejectionReason = "";

        if (!string.Equals(requestHead.Version, "HTTP/1.1", StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "HTTP Upgrade requires HTTP/1.1.";
            return false;
        }

        if (!HopByHopHeaderPolicy.HasConnectionToken(requestHead.Headers, "upgrade"))
        {
            rejectionReason = "HTTP Upgrade requires Connection: Upgrade.";
            return false;
        }

        var protocol = GetHeaderValue(requestHead.Headers, "Upgrade");
        if (string.IsNullOrWhiteSpace(protocol))
        {
            rejectionReason = "HTTP Upgrade requires an Upgrade header.";
            return false;
        }

        if (requestHead.Framing.Kind != Http1BodyKind.None)
        {
            rejectionReason = "HTTP Upgrade request bodies are not supported in Phase 7.";
            return false;
        }

        var isWebSocket = string.Equals(protocol, "websocket", StringComparison.OrdinalIgnoreCase);
        string? webSocketKey = null;
        if (isWebSocket)
        {
            if (!string.Equals(requestHead.Method, "GET", StringComparison.Ordinal))
            {
                rejectionReason = "WebSocket Upgrade requires GET.";
                return false;
            }

            webSocketKey = GetHeaderValue(requestHead.Headers, "Sec-WebSocket-Key");
            if (string.IsNullOrWhiteSpace(webSocketKey))
            {
                rejectionReason = "WebSocket Upgrade requires Sec-WebSocket-Key.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(GetHeaderValue(requestHead.Headers, "Sec-WebSocket-Version")))
            {
                rejectionReason = "WebSocket Upgrade requires Sec-WebSocket-Version.";
                return false;
            }
        }

        upgrade = new UpgradeRequestInfo(protocol, isWebSocket, webSocketKey);
        return true;
    }

    public static string? GetHeaderValue(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return header.Value;
            }
        }

        return null;
    }

    public static bool IsManagedUpgradeHeader(string headerName)
    {
        return string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Upgrade", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsUnsafeSwitchingProtocolsResponseHeader(string headerName)
    {
        return string.Equals(headerName, "Keep-Alive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Authenticate", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "TE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Trailer", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasHeader(IReadOnlyList<Http1HeaderField> headers, string name)
    {
        return headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
