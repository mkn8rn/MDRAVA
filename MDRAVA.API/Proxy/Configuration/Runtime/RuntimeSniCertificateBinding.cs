namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeSniCertificateBinding(
    string HostName,
    string CertificateId);
