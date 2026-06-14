namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHealthCheckProjection(
    bool Enabled,
    string Path,
    TimeSpan Interval,
    TimeSpan Timeout,
    int HealthyThreshold,
    int UnhealthyThreshold);
