namespace MDRAVA.BLL.ControlPlane;

public interface IAcmeCertificateRenewalEventSink
{
    void RenewalFailed(string certificateId, string? errorSummary);
}
