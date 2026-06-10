using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHeaderPolicy(
    IReadOnlyList<Http1HeaderField> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<Http1HeaderField> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicy Empty { get; } = new([], [], [], []);
}
