namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IAcmeCertificateRenewalEventSink
{
    void RenewalFailed(string certificateId, string? errorSummary);
}
