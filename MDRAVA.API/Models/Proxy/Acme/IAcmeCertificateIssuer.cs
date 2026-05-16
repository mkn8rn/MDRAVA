namespace MDRAVA.API.Proxy.Acme;

public interface IAcmeCertificateIssuer
{
    ValueTask<AcmeCertificateIssueResult> IssueAsync(
        AcmeCertificateIssueRequest request,
        AcmeChallengeStore challengeStore,
        CancellationToken cancellationToken);
}
