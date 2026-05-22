namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAcmeStatusSnapshotReader
{
    bool TryGetSnapshot(out ProxyAcmeStatusSnapshot? snapshot);

    IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses();
}
