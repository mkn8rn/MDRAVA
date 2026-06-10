namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeMetricsSink
{
    void AcmeRenewalAttempted();

    void AcmeRenewalSucceeded();

    void AcmeRenewalFailed();
}
