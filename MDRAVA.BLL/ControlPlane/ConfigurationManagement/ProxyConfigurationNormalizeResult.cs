using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationNormalizeResult
{
    private ProxyConfigurationNormalizeResult(
        bool succeeded,
        string format,
        string? canonicalJson,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        Succeeded = succeeded;
        Format = format;
        CanonicalJson = canonicalJson;
        Errors = errors;
        FileErrors = fileErrors;
    }

    public bool Succeeded { get; }

    public string Format { get; }

    public string? CanonicalJson { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public static ProxyConfigurationNormalizeResult Normalized(
        string format,
        string canonicalJson)
    {
        return new ProxyConfigurationNormalizeResult(
            succeeded: true,
            format: format,
            canonicalJson: canonicalJson,
            errors: [],
            fileErrors: []);
    }

    public static ProxyConfigurationNormalizeResult Failed(
        string format,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        return new ProxyConfigurationNormalizeResult(
            succeeded: false,
            format: format,
            canonicalJson: null,
            errors: fileErrors.Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}").ToArray(),
            fileErrors: fileErrors);
    }
}
