namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateOverrides(
        List<string> failures,
        string routePrefix,
        ProxyRouteOverrideOptions overrides)
    {
        if (overrides.MaxRequestBodyBytes is < 0 or > 1L * 1024 * 1024 * 1024 * 1024)
        {
            failures.Add($"{routePrefix}:Overrides:MaxRequestBodyBytes must be between 0 and 1099511627776.");
        }

        if (overrides.ClientRequestHeadTimeoutMs.HasValue)
        {
            ValidateOverrideTimeout(failures, $"{routePrefix}:Overrides:ClientRequestHeadTimeoutMs", overrides.ClientRequestHeadTimeoutMs.Value);
        }

        if (overrides.UpstreamResponseHeadTimeoutMs.HasValue)
        {
            ValidateOverrideTimeout(failures, $"{routePrefix}:Overrides:UpstreamResponseHeadTimeoutMs", overrides.UpstreamResponseHeadTimeoutMs.Value);
        }
    }

    private static void ValidateOverrideTimeout(List<string> failures, string name, int value)
    {
        if (value is < 100 or > 10 * 60 * 1000)
        {
            failures.Add($"{name} must be between 100 and 600000 milliseconds.");
        }
    }
}
