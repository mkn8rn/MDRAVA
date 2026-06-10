using MDRAVA.BLL.ControlPlane;
namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyReadinessEvaluationInput(
    bool HasActiveConfiguration,
    int? ConfigGeneration,
    bool IsShuttingDown,
    bool LastListenerReloadFailed,
    string LogPersistenceState,
    ProxyRuntimePreflightStatus RuntimePreflight,
    ProxySubsystemSummaries Subsystems,
    DateTimeOffset EvaluatedAtUtc);
