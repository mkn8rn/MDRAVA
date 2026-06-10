namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeCertificateLifecycleStatusSource
{
    IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses();
}
