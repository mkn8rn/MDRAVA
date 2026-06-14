using BusinessRuntimeCanonicalHostPolicy = MDRAVA.BLL.Configuration.RuntimeCanonicalHostPolicy;
using BusinessRuntimeHttpsRedirectProjection = MDRAVA.BLL.Configuration.RuntimeHttpsRedirectProjection;
using BusinessRuntimeMaintenancePolicy = MDRAVA.BLL.Configuration.RuntimeMaintenancePolicy;
using BusinessRuntimePathRewritePolicy = MDRAVA.BLL.Configuration.RuntimePathRewritePolicy;
using BusinessRuntimeRedirectPolicy = MDRAVA.BLL.Configuration.RuntimeRedirectPolicy;
using BusinessRuntimeStaticResponse = MDRAVA.BLL.Configuration.RuntimeStaticResponse;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHttpsRedirectResponse(
    bool Enabled,
    int StatusCode,
    int? HttpsPort)
{
    public static RuntimeHttpsRedirectResponse FromProjection(BusinessRuntimeHttpsRedirectProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHttpsRedirectResponse(projection.Enabled, projection.StatusCode, projection.HttpsPort);
    }
}

public sealed record RuntimeCanonicalHostResponse(
    bool Enabled,
    string TargetHost,
    int StatusCode)
{
    public static RuntimeCanonicalHostResponse FromPolicy(BusinessRuntimeCanonicalHostPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeCanonicalHostResponse(policy.Enabled, policy.TargetHost, policy.StatusCode);
    }
}

public sealed record RuntimePathRewriteResponse(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement)
{
    public static RuntimePathRewriteResponse FromPolicy(BusinessRuntimePathRewritePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimePathRewriteResponse(policy.StripPrefix, policy.ReplacePrefix, policy.Replacement);
    }
}

public sealed record RuntimeRedirectResponse(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery)
{
    public static RuntimeRedirectResponse FromPolicy(BusinessRuntimeRedirectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeRedirectResponse(
            policy.StatusCode,
            policy.TargetUrl,
            policy.TargetPath,
            policy.PreserveQuery);
    }
}

public sealed record RuntimeStaticResponseResponse(
    int StatusCode,
    string ContentType,
    string Body)
{
    public static RuntimeStaticResponseResponse FromResponse(BusinessRuntimeStaticResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new RuntimeStaticResponseResponse(response.StatusCode, response.ContentType, response.Body);
    }
}

public sealed record RuntimeMaintenanceResponse(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body)
{
    public static RuntimeMaintenanceResponse FromPolicy(BusinessRuntimeMaintenancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeMaintenanceResponse(
            policy.Enabled,
            policy.RetryAfterSeconds,
            policy.ContentType,
            policy.Body);
    }
}
