namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeListenerIdentityProjection(
    string Name,
    string Address,
    int Port,
    RuntimeListenerTransport Transport,
    bool TlsEnabled,
    string Key,
    string BindKey);
