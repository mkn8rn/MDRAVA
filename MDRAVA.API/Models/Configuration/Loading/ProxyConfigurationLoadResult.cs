using MDRAVA.API.Proxy.Configuration.Runtime;

namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationLoadResult(
    bool Succeeded,
    string SourceDirectory,
    ProxyConfigurationSnapshot? Snapshot,
    IReadOnlyList<string> Errors)
{
    public static ProxyConfigurationLoadResult Success(string sourceDirectory, ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigurationLoadResult(true, sourceDirectory, snapshot, []);
    }

    public static ProxyConfigurationLoadResult Failure(string sourceDirectory, IReadOnlyList<string> errors)
    {
        return new ProxyConfigurationLoadResult(false, sourceDirectory, null, errors);
    }
}
