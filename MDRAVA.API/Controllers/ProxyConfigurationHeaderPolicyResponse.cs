using BusinessRuntimeHeaderFieldProjection = MDRAVA.BLL.Configuration.RuntimeHeaderFieldProjection;
using BusinessRuntimeHeaderPolicyProjection = MDRAVA.BLL.Configuration.RuntimeHeaderPolicyProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHeaderPolicyResponse(
    IReadOnlyList<RuntimeHeaderFieldResponse> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<RuntimeHeaderFieldResponse> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicyResponse FromProjection(BusinessRuntimeHeaderPolicyProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHeaderPolicyResponse(
            RuntimeHeaderFieldResponse.FromFields(projection.SetRequestHeaders),
            ApiResponseList.Copy(projection.RemoveRequestHeaders),
            RuntimeHeaderFieldResponse.FromFields(projection.SetResponseHeaders),
            ApiResponseList.Copy(projection.RemoveResponseHeaders));
    }
}

public sealed record RuntimeHeaderFieldResponse(string Name, string Value)
{
    public static IReadOnlyList<RuntimeHeaderFieldResponse> FromFields(
        IReadOnlyList<BusinessRuntimeHeaderFieldProjection> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return ApiResponseList.Copy(fields.Select(FromField));
    }

    private static RuntimeHeaderFieldResponse FromField(BusinessRuntimeHeaderFieldProjection field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return new RuntimeHeaderFieldResponse(field.Name, field.Value);
    }
}
