namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeHealthCheckOptions(
    bool Enabled,
    string Path,
    TimeSpan Interval,
    TimeSpan Timeout,
    int HealthyThreshold,
    int UnhealthyThreshold);
