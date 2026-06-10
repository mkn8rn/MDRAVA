namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeStatusSnapshotReader
{
    bool TryGetSnapshot(out ProxyAcmeStatusSnapshot? snapshot);

    IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses();
}
