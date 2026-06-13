namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyTlsMetricsSnapshot(
    long HandshakeAttempts,
    long HandshakeSuccesses,
    long HandshakeFailures,
    long HandshakeTimeouts,
    long NoCertificateForSniFailures,
    long ActiveHandshakes,
    long HandshakeAdmissionRejections);
