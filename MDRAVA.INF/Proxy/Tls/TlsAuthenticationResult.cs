using System.Net.Security;

namespace MDRAVA.INF.Proxy.Tls;

public sealed record TlsAuthenticationResult(
    SslStream Stream,
    SslApplicationProtocol NegotiatedProtocol);
