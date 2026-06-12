using MDRAVA.BLL.Configuration;

namespace MDRAVA.Tests;

internal sealed class SilentProxyConfigurationReloadEventSink : IProxyConfigurationReloadEventSink
{
    public static SilentProxyConfigurationReloadEventSink Instance { get; } = new();

    private SilentProxyConfigurationReloadEventSink()
    {
    }

    public void LoadFailed(string sourceDirectory, IReadOnlyList<string> errors)
    {
    }

    public void Loaded(int version, string sourceDirectory)
    {
    }
}

internal sealed class ActivatingProxyListenerReloadApplier : IProxyListenerReloadApplier
{
    public static ActivatingProxyListenerReloadApplier Instance { get; } = new();

    private ActivatingProxyListenerReloadApplier()
    {
    }

    public ValueTask<ProxyListenerReloadResult> ApplyReloadAsync(
        ProxyConfigurationSnapshot snapshot,
        Func<ProxyConfigurationSnapshot, ProxyConfigurationSnapshot> activateSnapshot,
        CancellationToken cancellationToken)
    {
        activateSnapshot(snapshot);
        return ValueTask.FromResult(ProxyListenerReloadResult.Applied(
            DateTimeOffset.UnixEpoch,
            added: 0,
            removed: 0,
            changed: 0,
            unchanged: 0,
            changes: [],
            errors: []));
    }
}
