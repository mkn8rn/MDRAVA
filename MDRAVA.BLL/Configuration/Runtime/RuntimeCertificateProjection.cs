namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCertificateProjection(
    string Id,
    string Path,
    string Format,
    string Source,
    IReadOnlyList<string> Domains,
    bool HasConfiguredPassword,
    string? Subject,
    string? Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter);
