namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeSniCertificateBinding(
    string HostName,
    string CertificateId);
