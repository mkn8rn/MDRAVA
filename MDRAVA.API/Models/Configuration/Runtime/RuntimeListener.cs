namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeListener(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    RuntimeListenerTransport Transport,
    string? DefaultCertificateId,
    IReadOnlyList<RuntimeSniCertificateBinding> SniCertificates,
    int Backlog,
    int MaxRequestHeadBytes,
    int MaxResponseHeadBytes,
    int MaxChunkLineBytes,
    int ForwardingBufferBytes)
{
    public RuntimeListenerIdentity Identity => RuntimeListenerIdentity.From(this);
}
