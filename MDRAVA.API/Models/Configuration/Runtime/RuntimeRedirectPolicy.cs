namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeRedirectPolicy(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery);
