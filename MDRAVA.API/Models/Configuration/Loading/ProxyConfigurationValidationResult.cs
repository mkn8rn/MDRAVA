namespace MDRAVA.API.Models.Configuration.Loading;

public sealed record ProxyConfigurationValidationResult(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    int? WouldBeVersion,
    IReadOnlyList<string> SourceFiles,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors);
