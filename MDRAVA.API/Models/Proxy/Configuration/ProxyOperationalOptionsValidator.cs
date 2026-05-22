using MDRAVA.BLL.Configuration;

namespace MDRAVA.API.Proxy.Configuration;

public static class ProxyOperationalOptionsValidator
{
    public static IReadOnlyList<string> Validate(ProxyOperationalOptions options)
    {
        return ProxyOperationalOptionsValidationRules.Validate(options, Environment.GetEnvironmentVariable);
    }
}
