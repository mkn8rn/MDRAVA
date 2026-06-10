using MDRAVA.BLL.Configuration;
using Microsoft.Extensions.Options;

namespace MDRAVA.INF.Configuration;

public sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    private readonly IProxyEndpointAddressPolicy _endpointAddressPolicy;

    public ProxyOptionsValidator(IProxyEndpointAddressPolicy endpointAddressPolicy)
    {
        _endpointAddressPolicy = endpointAddressPolicy;
    }

    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        var failures = ProxyOptionsValidationRules.Validate(options, _endpointAddressPolicy);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
