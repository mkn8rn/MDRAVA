using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeCertificate(
    string Id,
    string Path,
    string Format,
    bool HasConfiguredPassword,
    X509Certificate2 Certificate,
    string Source,
    IReadOnlyList<string> Domains);
