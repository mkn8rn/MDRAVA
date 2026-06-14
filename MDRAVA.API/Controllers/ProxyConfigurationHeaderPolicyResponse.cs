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
            projection.RemoveRequestHeaders.ToArray(),
            RuntimeHeaderFieldResponse.FromFields(projection.SetResponseHeaders),
            projection.RemoveResponseHeaders.ToArray());
    }
}

public sealed record RuntimeHeaderFieldResponse(string Name, string Value)
{
    public static IReadOnlyList<RuntimeHeaderFieldResponse> FromFields(
        IReadOnlyList<BusinessRuntimeHeaderFieldProjection> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.Select(FromField).ToArray();
    }

    private static RuntimeHeaderFieldResponse FromField(BusinessRuntimeHeaderFieldProjection field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return new RuntimeHeaderFieldResponse(field.Name, field.Value);
    }
}
