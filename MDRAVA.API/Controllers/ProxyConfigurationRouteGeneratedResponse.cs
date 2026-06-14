using BusinessRuntimeCanonicalHostProjection = MDRAVA.BLL.Configuration.RuntimeCanonicalHostProjection;
using BusinessRuntimeHttpsRedirectProjection = MDRAVA.BLL.Configuration.RuntimeHttpsRedirectProjection;
using BusinessRuntimeMaintenancePolicy = MDRAVA.BLL.Configuration.RuntimeMaintenancePolicy;
using BusinessRuntimePathRewriteProjection = MDRAVA.BLL.Configuration.RuntimePathRewriteProjection;
using BusinessRuntimeRedirectProjection = MDRAVA.BLL.Configuration.RuntimeRedirectProjection;
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
    public static RuntimeCanonicalHostResponse FromProjection(BusinessRuntimeCanonicalHostProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeCanonicalHostResponse(projection.Enabled, projection.TargetHost, projection.StatusCode);
    }
}

public sealed record RuntimePathRewriteResponse(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement)
{
    public static RuntimePathRewriteResponse FromProjection(BusinessRuntimePathRewriteProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimePathRewriteResponse(
            projection.StripPrefix,
            projection.ReplacePrefix,
            projection.Replacement);
    }
}

public sealed record RuntimeRedirectResponse(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery)
{
    public static RuntimeRedirectResponse FromProjection(BusinessRuntimeRedirectProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeRedirectResponse(
            projection.StatusCode,
            projection.TargetUrl,
            projection.TargetPath,
            projection.PreserveQuery);
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
