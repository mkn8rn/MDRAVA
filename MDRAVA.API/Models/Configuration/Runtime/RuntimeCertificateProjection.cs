namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeCertificateProjection(
    string Id,
    string Path,
    string Format,
    bool HasConfiguredPassword,
    string? Subject,
    string? Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter);
