using BusinessProxyHeaderField = MDRAVA.BLL.Http.ProxyHeaderField;
using BusinessRuntimeHeaderPolicy = MDRAVA.BLL.Configuration.RuntimeHeaderPolicy;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeHeaderPolicyResponse(
    IReadOnlyList<RuntimeHeaderFieldResponse> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<RuntimeHeaderFieldResponse> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicyResponse FromPolicy(BusinessRuntimeHeaderPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeHeaderPolicyResponse(
            RuntimeHeaderFieldResponse.FromFields(policy.SetRequestHeaders),
            policy.RemoveRequestHeaders.ToArray(),
            RuntimeHeaderFieldResponse.FromFields(policy.SetResponseHeaders),
            policy.RemoveResponseHeaders.ToArray());
    }
}

public sealed record RuntimeHeaderFieldResponse(string Name, string Value)
{
    public static IReadOnlyList<RuntimeHeaderFieldResponse> FromFields(
        IReadOnlyList<BusinessProxyHeaderField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.Select(FromField).ToArray();
    }

    private static RuntimeHeaderFieldResponse FromField(BusinessProxyHeaderField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return new RuntimeHeaderFieldResponse(field.Name, field.Value);
    }
}
