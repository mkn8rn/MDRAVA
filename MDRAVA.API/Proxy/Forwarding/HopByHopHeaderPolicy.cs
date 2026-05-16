using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Forwarding;

public sealed class HopByHopHeaderPolicy
{
    private static readonly HashSet<string> StandardHopByHopHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade"
    };

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
