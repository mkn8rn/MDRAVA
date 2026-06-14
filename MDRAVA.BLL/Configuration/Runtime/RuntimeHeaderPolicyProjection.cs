namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHeaderPolicyProjection
{
    public RuntimeHeaderPolicyProjection(
        IReadOnlyList<RuntimeHeaderFieldProjection> SetRequestHeaders,
        IReadOnlyList<string> RemoveRequestHeaders,
        IReadOnlyList<RuntimeHeaderFieldProjection> SetResponseHeaders,
        IReadOnlyList<string> RemoveResponseHeaders)
    {
        this.SetRequestHeaders = RuntimeList.Copy(SetRequestHeaders);
        this.RemoveRequestHeaders = RuntimeList.Copy(RemoveRequestHeaders);
        this.SetResponseHeaders = RuntimeList.Copy(SetResponseHeaders);
        this.RemoveResponseHeaders = RuntimeList.Copy(RemoveResponseHeaders);
    }

    public IReadOnlyList<RuntimeHeaderFieldProjection> SetRequestHeaders { get; }

    public IReadOnlyList<string> RemoveRequestHeaders { get; }

    public IReadOnlyList<RuntimeHeaderFieldProjection> SetResponseHeaders { get; }

    public IReadOnlyList<string> RemoveResponseHeaders { get; }
}

public sealed record RuntimeHeaderFieldProjection(string Name, string Value);
