using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHeaderPolicy(
    IReadOnlyList<ProxyHeaderField> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<ProxyHeaderField> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicy Empty { get; } = new([], [], [], []);
}
