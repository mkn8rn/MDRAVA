namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAcmeCertificateLifecycleStatusSource
{
    IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses();
}
