namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeQuicListenerIdentityProjection(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled,
    string Key,
    string BindKey);
