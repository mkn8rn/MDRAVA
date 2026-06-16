using BusinessRuntimeHeaderFieldProjection = MDRAVA.BLL.Configuration.RuntimeHeaderFieldProjection;
using BusinessRuntimeHeaderPolicyProjection = MDRAVA.BLL.Configuration.RuntimeHeaderPolicyProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHeaderPolicyResponse
{
    public RuntimeHeaderPolicyResponse(
        IReadOnlyList<RuntimeHeaderFieldResponse> setRequestHeaders,
        IReadOnlyList<string> removeRequestHeaders,
        IReadOnlyList<RuntimeHeaderFieldResponse> setResponseHeaders,
        IReadOnlyList<string> removeResponseHeaders)
    {
        SetRequestHeaders = ApiResponseList.Copy(setRequestHeaders);
        RemoveRequestHeaders = ApiResponseList.Copy(removeRequestHeaders);
        SetResponseHeaders = ApiResponseList.Copy(setResponseHeaders);
        RemoveResponseHeaders = ApiResponseList.Copy(removeResponseHeaders);
    }

    public IReadOnlyList<RuntimeHeaderFieldResponse> SetRequestHeaders { get; }

    public IReadOnlyList<string> RemoveRequestHeaders { get; }

    public IReadOnlyList<RuntimeHeaderFieldResponse> SetResponseHeaders { get; }

    public IReadOnlyList<string> RemoveResponseHeaders { get; }

    public static RuntimeHeaderPolicyResponse FromProjection(BusinessRuntimeHeaderPolicyProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHeaderPolicyResponse(
            setRequestHeaders: RuntimeHeaderFieldResponse.FromFields(projection.SetRequestHeaders),
            removeRequestHeaders: projection.RemoveRequestHeaders,
            setResponseHeaders: RuntimeHeaderFieldResponse.FromFields(projection.SetResponseHeaders),
            removeResponseHeaders: projection.RemoveResponseHeaders);
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
