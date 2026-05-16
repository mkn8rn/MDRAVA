namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeCertificateProjection(
    string Id,
    string Path,
    string Format,
    bool HasConfiguredPassword,
    string? Subject,
    string? Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter);
