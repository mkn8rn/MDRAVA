namespace MDRAVA.BLL.Configuration;

public static partial class SiteOptionsAggregator
{
    private static string GetListenerKey(ListenerOptions listener)
    {
        return $"{listener.Name}|{listener.Address}|{listener.Port}|{listener.Transport}";
    }

    private static ListenerOptions MergeListeners(ListenerOptions existing, ListenerOptions next)
    {
        var sniCertificates = CopySniCertificates(existing.SniCertificates.Concat(next.SniCertificates));

        return new ListenerOptions
        {
            Name = existing.Name,
            Address = existing.Address,
            Port = existing.Port,
            Enabled = existing.Enabled || next.Enabled,
            Transport = existing.Transport,
            Protocols = MergeListenerProtocols(existing.Protocols, next.Protocols),
            Http3Enablement = MergeHttp3Enablement(existing.Http3Enablement, next.Http3Enablement),
            Http3AltSvcEnabled = existing.Http3AltSvcEnabled || next.Http3AltSvcEnabled,
            Http3AltSvcMaxAgeSeconds = existing.Http3AltSvcMaxAgeSeconds,
            DefaultCertificateId = !string.IsNullOrWhiteSpace(existing.DefaultCertificateId)
                ? existing.DefaultCertificateId
                : next.DefaultCertificateId,
            SniCertificates = sniCertificates,
            Backlog = existing.Backlog,
            MaxRequestHeadBytes = existing.MaxRequestHeadBytes,
            MaxResponseHeadBytes = existing.MaxResponseHeadBytes,
            MaxChunkLineBytes = existing.MaxChunkLineBytes,
            ForwardingBufferBytes = existing.ForwardingBufferBytes,
            Http2MaxConcurrentStreams = existing.Http2MaxConcurrentStreams,
            Http2MaxHeaderListBytes = existing.Http2MaxHeaderListBytes,
            Http2MaxFrameSize = existing.Http2MaxFrameSize
        };
    }

    private static string MergeHttp3Enablement(string existing, string next)
    {
        return RuntimeHttp3Compatibility.MergeEnablementConfigText(existing, next);
    }

    private static string MergeListenerProtocols(string existing, string next)
    {
        var existingParsing = RuntimeListenerProtocolExtensions.ParseConfigText(existing);
        if (existingParsing is not RuntimeListenerProtocolParseResult.AcceptedResult existingProtocols)
        {
            return existing;
        }

        var nextParsing = RuntimeListenerProtocolExtensions.ParseConfigText(next);
        if (nextParsing is not RuntimeListenerProtocolParseResult.AcceptedResult nextProtocols)
        {
            return next;
        }

        var merged = existingProtocols.Protocols | nextProtocols.Protocols;
        return merged.ToConfigText();
    }
}
