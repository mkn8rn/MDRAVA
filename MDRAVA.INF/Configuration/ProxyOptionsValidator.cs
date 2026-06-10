using MDRAVA.BLL.Configuration;
using Microsoft.Extensions.Options;

namespace MDRAVA.INF.Configuration;

public sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        var failures = ProxyOptionsValidationRules.Validate(options);
        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
