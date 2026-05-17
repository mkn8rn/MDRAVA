namespace MDRAVA.API.Models.Metrics;

public sealed record ProxyHttp3RequestOutcomeSnapshot(
    string Method,
    string Outcome,
    string StatusClass,
    long Count);
