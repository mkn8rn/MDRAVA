using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateRetryPolicy(
        List<string> failures,
        string routePrefix,
        ProxyRetryPolicyOptions retry,
        string routeAction)
    {
        if (retry.MaxAttempts is < 1 or > 5)
        {
            failures.Add($"{routePrefix}:Retry:MaxAttempts must be between 1 and 5.");
        }

        if (retry.PerAttemptTimeoutMs is < 0 or > 10 * 60 * 1000)
        {
            failures.Add($"{routePrefix}:Retry:PerAttemptTimeoutMs must be between 0 and 600000 milliseconds when configured.");
        }

        if (retry.RetryBackoffMilliseconds is < 0 or > 60_000)
        {
            failures.Add($"{routePrefix}:Retry:RetryBackoffMilliseconds must be between 0 and 60000.");
        }

        if (!retry.Enabled)
        {
            return;
        }

        if (!IsProxyAction(routeAction))
        {
            failures.Add($"{routePrefix}:Retry can only be enabled for proxy routes.");
        }

        if (retry.MaxAttempts < 2)
        {
            failures.Add($"{routePrefix}:Retry:MaxAttempts must be at least 2 when retry is enabled.");
        }

        if (retry.RetryMethods.Count == 0)
        {
            failures.Add($"{routePrefix}:Retry:RetryMethods must contain GET, HEAD, or both.");
        }

        for (var index = 0; index < retry.RetryMethods.Count; index++)
        {
            var method = retry.RetryMethods[index];
            if (!ProxyRequestMethodPolicy.IsSafeReadMethod(method))
            {
                failures.Add($"{routePrefix}:Retry:RetryMethods:{index} must be GET or HEAD.");
            }
        }

        for (var index = 0; index < retry.RetryOnStatusCodes.Count; index++)
        {
            var statusCode = retry.RetryOnStatusCodes[index];
            if (statusCode is < 500 or > 599)
            {
                failures.Add($"{routePrefix}:Retry:RetryOnStatusCodes:{index} must be a 5xx HTTP response status code.");
            }
        }
    }
}
