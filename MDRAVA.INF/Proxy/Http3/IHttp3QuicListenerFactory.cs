using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane;
using System.Net.Quic;

namespace MDRAVA.INF.Proxy.Http3;

public interface IHttp3QuicListenerFactory
{
    bool IsSupported { get; }

    ValueTask<QuicListener> ListenAsync(
        RuntimeListener listener,
        ProxyConfigurationSnapshot snapshot,
        CancellationToken cancellationToken);
}
