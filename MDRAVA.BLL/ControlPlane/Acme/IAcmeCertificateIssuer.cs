namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IAcmeCertificateIssuer
{
    ValueTask<AcmeCertificateIssueResult> IssueAsync(
        AcmeCertificateIssueRequest request,
        AcmeChallengeStore challengeStore,
        CancellationToken cancellationToken);
}
