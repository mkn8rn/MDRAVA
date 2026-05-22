namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationValidationResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    int? WouldBeVersion,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors);
