namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeForwardedHeadersProjection
{
    public RuntimeForwardedHeadersProjection(
        bool Enabled,
        IReadOnlyList<string> TrustedProxies)
    {
        this.Enabled = Enabled;
        this.TrustedProxies = RuntimeList.Copy(TrustedProxies);
    }

    public bool Enabled { get; init; }

    public IReadOnlyList<string> TrustedProxies { get; }
}
