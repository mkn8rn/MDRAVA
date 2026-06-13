namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeStatusSnapshotReader
{
    ProxyAcmeStatusSnapshotReadResult ReadSnapshot();

    IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses();
}

public abstract record ProxyAcmeStatusSnapshotReadResult
{
    private ProxyAcmeStatusSnapshotReadResult()
    {
    }

    public static ProxyAcmeStatusSnapshotReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyAcmeStatusSnapshotReadResult Available(ProxyAcmeStatusSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AvailableResult(snapshot);
    }

    public sealed record AvailableResult : ProxyAcmeStatusSnapshotReadResult
    {
        public AvailableResult(ProxyAcmeStatusSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            Snapshot = snapshot;
        }

        public ProxyAcmeStatusSnapshot Snapshot { get; }
    }

    public sealed record MissingConfigurationResult : ProxyAcmeStatusSnapshotReadResult;
}
