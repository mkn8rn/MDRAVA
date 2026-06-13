using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.Tests;

internal static class ProxyConfigurationLoadResultAssertions
{
    public static ProxyConfigurationLoadResult.LoadedResult AssertLoaded(
        ProxyConfigurationLoadResult result)
    {
        AssertEx.True(result is ProxyConfigurationLoadResult.LoadedResult, string.Join("; ", result.Errors));
        return (ProxyConfigurationLoadResult.LoadedResult)result;
    }

    public static ProxyConfigurationLoadResult.ValidatedResult AssertValidated(
        ProxyConfigurationLoadResult result)
    {
        AssertEx.True(result is ProxyConfigurationLoadResult.ValidatedResult, string.Join("; ", result.Errors));
        return (ProxyConfigurationLoadResult.ValidatedResult)result;
    }

    public static ProxyConfigurationLoadResult.FailedResult AssertFailed(
        ProxyConfigurationLoadResult result)
    {
        AssertEx.True(result is ProxyConfigurationLoadResult.FailedResult);
        return (ProxyConfigurationLoadResult.FailedResult)result;
    }

    public static ProxyConfigurationSnapshot AssertLoadedSnapshot(
        ProxyConfigurationLoadResult result)
    {
        return AssertLoaded(result).Snapshot;
    }
}
