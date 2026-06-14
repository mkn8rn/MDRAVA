namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRedirectProjection(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery);
