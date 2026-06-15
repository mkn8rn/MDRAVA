using BusinessProxyConfigurationDiscovery = MDRAVA.BLL.Configuration.ProxyConfigurationDiscovery;
using BusinessProxyConfigurationFileDiscovery = MDRAVA.BLL.Configuration.ProxyConfigurationFileDiscovery;
using BusinessProxyConfigurationFileError = MDRAVA.BLL.Configuration.ProxyConfigurationFileError;
using BusinessProxyFilesystemLayout = MDRAVA.BLL.Configuration.ProxyFilesystemLayout;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationDiscoveryResponse(
    ProxyFilesystemLayoutResponse Layout,
    IReadOnlyList<ProxyConfigurationFileDiscoveryResponse> Files,
    IReadOnlyList<string> CreatedPaths,
    IReadOnlyList<string> ExistingPaths)
{
    public static ProxyConfigurationDiscoveryResponse FromDiscovery(BusinessProxyConfigurationDiscovery discovery)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        return new ProxyConfigurationDiscoveryResponse(
            ProxyFilesystemLayoutResponse.FromLayout(discovery.Layout),
            ProxyConfigurationFileDiscoveryResponse.FromFiles(discovery.Files),
            ApiResponseList.Copy(discovery.CreatedPaths),
            ApiResponseList.Copy(discovery.ExistingPaths));
    }
}

public sealed record ProxyFilesystemLayoutResponse(
    string DataDirectory,
    string ConfigDirectory,
    string SitesDirectory,
    string LogsDirectory,
    string CertificatesDirectory,
    string StateDirectory,
    string ProxyConfigPath)
{
    public static ProxyFilesystemLayoutResponse FromLayout(BusinessProxyFilesystemLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        return new ProxyFilesystemLayoutResponse(
            layout.DataDirectory,
            layout.ConfigDirectory,
            layout.SitesDirectory,
            layout.LogsDirectory,
            layout.CertificatesDirectory,
            layout.StateDirectory,
            layout.ProxyConfigPath);
    }
}

public sealed record ProxyConfigurationFileDiscoveryResponse(
    string Path,
    string Format,
    string Status,
    string? Reason)
{
    public static IReadOnlyList<ProxyConfigurationFileDiscoveryResponse> FromFiles(
        IReadOnlyList<BusinessProxyConfigurationFileDiscovery> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        return ApiResponseList.Copy(files.Select(FromFile));
    }

    private static ProxyConfigurationFileDiscoveryResponse FromFile(
        BusinessProxyConfigurationFileDiscovery file)
    {
        ArgumentNullException.ThrowIfNull(file);

        return new ProxyConfigurationFileDiscoveryResponse(
            file.Path,
            file.Format,
            file.Status,
            file.Reason);
    }
}

public sealed record ProxyConfigurationFileErrorResponse(
    string? Path,
    string Message)
{
    public static IReadOnlyList<ProxyConfigurationFileErrorResponse> FromErrors(
        IReadOnlyList<BusinessProxyConfigurationFileError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return ApiResponseList.Copy(errors.Select(FromError));
    }

    private static ProxyConfigurationFileErrorResponse FromError(BusinessProxyConfigurationFileError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ProxyConfigurationFileErrorResponse(error.Path, error.Message);
    }
}
