using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHeaderPolicy
{
    public RuntimeHeaderPolicy(
        IEnumerable<ProxyHeaderField> SetRequestHeaders,
        IEnumerable<string> RemoveRequestHeaders,
        IEnumerable<ProxyHeaderField> SetResponseHeaders,
        IEnumerable<string> RemoveResponseHeaders)
    {
        this.SetRequestHeaders = RuntimeList.Copy(SetRequestHeaders);
        this.RemoveRequestHeaders = RuntimeList.Copy(RemoveRequestHeaders);
        this.SetResponseHeaders = RuntimeList.Copy(SetResponseHeaders);
        this.RemoveResponseHeaders = RuntimeList.Copy(RemoveResponseHeaders);
    }

    public IReadOnlyList<ProxyHeaderField> SetRequestHeaders { get; }

    public IReadOnlyList<string> RemoveRequestHeaders { get; }

    public IReadOnlyList<ProxyHeaderField> SetResponseHeaders { get; }

    public IReadOnlyList<string> RemoveResponseHeaders { get; }

    public static RuntimeHeaderPolicy Empty { get; } = new([], [], [], []);
}
