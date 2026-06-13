using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public abstract record ProxyConfigurationNormalizeResult
{
    private ProxyConfigurationNormalizeResult(
        string format,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(fileErrors);

        Format = format;
        Errors = errors.ToArray();
        FileErrors = fileErrors.ToArray();
    }

    public string Format { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public static ProxyConfigurationNormalizeResult Normalized(
        string format,
        string canonicalJson)
    {
        return new NormalizedResult(format, canonicalJson);
    }

    public static ProxyConfigurationNormalizeResult Failed(
        string format,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors)
    {
        return new FailedResult(format, fileErrors);
    }

    public sealed record NormalizedResult : ProxyConfigurationNormalizeResult
    {
        internal NormalizedResult(string format, string canonicalJson)
            : base(format, [], [])
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(canonicalJson);
            CanonicalJson = canonicalJson;
        }

        public string CanonicalJson { get; }
    }

    public sealed record FailedResult : ProxyConfigurationNormalizeResult
    {
        internal FailedResult(
            string format,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors)
            : base(
                format,
                CreateErrors(fileErrors),
                fileErrors)
        {
        }

        private static IReadOnlyList<string> CreateErrors(IReadOnlyList<ProxyConfigurationFileError> fileErrors)
        {
            ArgumentNullException.ThrowIfNull(fileErrors);
            return fileErrors
                .Select(static error => error.Path is null ? error.Message : $"{error.Path}: {error.Message}")
                .ToArray();
        }
    }
}
