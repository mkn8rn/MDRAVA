using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeCertificate(
    string Id,
    string Path,
    string Format,
    bool HasConfiguredPassword,
    X509Certificate2 Certificate,
    string Source = "manualPfx",
    IReadOnlyList<string>? Domains = null);
