using System.Net.Security;

namespace MDRAVA.API.Models.Tls;

public sealed record TlsAuthenticationResult(
    SslStream Stream,
    SslApplicationProtocol NegotiatedProtocol);
