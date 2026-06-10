using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Headers;

public sealed class HopByHopHeaderPolicy
{
    private static readonly HashSet<string> StandardHopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Connection",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

    public static bool IsHopByHopHeader(string name)
    {
        return StandardHopByHopHeaders.Contains(name);
    }

    public static bool HasConnectionToken(IReadOnlyList<Http1HeaderField> headers, string token)
    {
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(value, token, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IReadOnlyList<Http1HeaderField> FilterForForwarding(
        IReadOnlyList<Http1HeaderField> headers,
        bool preserveTransferEncoding,
        bool preserveTrailer)
    {
        HashSet<string> nominated = new(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "Connection", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var token in header.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                nominated.Add(token);
            }
        }

        List<Http1HeaderField> filtered = [];
        foreach (var header in headers)
        {
            if (nominated.Contains(header.Name))
            {
                continue;
            }

            if (StandardHopByHopHeaders.Contains(header.Name))
            {
                if (preserveTransferEncoding && string.Equals(header.Name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (preserveTrailer && string.Equals(header.Name, "Trailer", StringComparison.OrdinalIgnoreCase))
                {
                    filtered.Add(header);
                }

                continue;
            }

            filtered.Add(header);
        }

        return filtered;
    }
}
