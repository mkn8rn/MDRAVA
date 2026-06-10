namespace MDRAVA.BLL.ControlPlane.Http3;

public interface IProxyHttp3AltSvcMetricsSink
{
    void Http3AltSvcEmitted();

    void Http3AltSvcSuppressed();
}
