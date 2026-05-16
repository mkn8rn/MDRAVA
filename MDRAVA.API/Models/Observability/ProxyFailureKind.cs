namespace MDRAVA.API.Models.Observability;

public enum ProxyFailureKind
{
    None = 0,
    ClientMalformedRequest,
    ClientRequestHeadTimeout,
    ClientRequestBodyTimeout,
    ClientDisconnected,
    UpstreamUnavailable,
    UpstreamConnectTimeout,
    UpstreamConnectFailed,
    UpstreamResponseHeadTimeout,
    UpstreamResponseBodyTimeout,
    UpstreamPrematureDisconnect,
    UpstreamMalformedResponse,
    NoMatchingRoute,
    NoHealthyUpstream,
    TlsHandshakeFailed,
    TlsHandshakeTimeout,
    UpgradeValidationFailed,
    UpgradeRejected,
    RateLimited,
    UpgradeRateLimited,
    AdmissionRejected,
    RequestPayloadTooLarge,
    ParserLimitExceeded,
    Shutdown,
    TunnelIdleTimeout,
    TunnelRelayFailure,
    InternalError
}
