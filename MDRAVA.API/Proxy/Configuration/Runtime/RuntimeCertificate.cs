using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeCertificate(
    string Id,
    string Path,
    string Format,
    bool HasConfiguredPassword,
    X509Certificate2 Certificate);
