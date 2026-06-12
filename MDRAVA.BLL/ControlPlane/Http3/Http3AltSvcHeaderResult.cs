using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.Http3;

public abstract record Http3AltSvcHeaderResult
{
    private Http3AltSvcHeaderResult()
    {
    }

    public static Http3AltSvcHeaderResult Suppressed { get; } = new SuppressedResult();

    public static Http3AltSvcHeaderResult Emitted(ProxyHeaderField header)
    {
        return new EmittedResult(header);
    }

    public sealed record SuppressedResult : Http3AltSvcHeaderResult;

    public sealed record EmittedResult : Http3AltSvcHeaderResult
    {
        public EmittedResult(ProxyHeaderField header)
        {
            ArgumentNullException.ThrowIfNull(header);

            Header = header;
        }

        public ProxyHeaderField Header { get; }
    }
}
