namespace MDRAVA.BLL.ControlPlane;

public interface IAcmeCertificateIssuer
{
    ValueTask<AcmeCertificateIssueResult> IssueAsync(
        AcmeCertificateIssueRequest request,
        AcmeChallengeStore challengeStore,
        CancellationToken cancellationToken);
}
