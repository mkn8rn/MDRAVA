namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRedirectPolicy(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery);
