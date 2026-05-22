namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationNormalizeResult(
    bool Succeeded,
    string Format,
    string? CanonicalJson,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors);
