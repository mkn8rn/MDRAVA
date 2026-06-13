using System.Globalization;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Routing;

public static class GeneratedRouteResponseHeaderPolicy
{
    public static IReadOnlyList<ProxyHeaderField> BuildFramedResponseHeaders(
        GeneratedRouteResponse response,
        string requestId,
        int bodyByteLength)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentOutOfRangeException.ThrowIfNegative(bodyByteLength);

        List<ProxyHeaderField> headers = [];
        if (!string.IsNullOrWhiteSpace(response.ContentType))
        {
            headers.Add(new ProxyHeaderField("content-type", response.ContentType));
        }

        headers.Add(new ProxyHeaderField("x-request-id", requestId));
        headers.AddRange(response.Headers.Where(static header => !HopByHopHeaderPolicy.IsHopByHopHeader(header.Name)));
        headers.Add(new ProxyHeaderField("content-length", bodyByteLength.ToString(CultureInfo.InvariantCulture)));
        return headers;
    }
}
