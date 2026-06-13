using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    public static IReadOnlyList<string> Validate(
        ProxyOptions options,
        IProxyEndpointAddressPolicy endpointAddressPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
        List<string> failures = [];

        ValidateListeners(failures, options, endpointAddressPolicy);
        ValidateRoutes(failures, options, endpointAddressPolicy, urlSyntaxPolicy);

        return failures;
    }
}
