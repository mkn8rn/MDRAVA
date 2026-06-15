using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public static class AcmeRenewalConfigurationSourceMapper
{
    public static AcmeRenewalConfigurationSourceSet FromRuntimeConfiguration(
        RuntimeAcmeOptions acme,
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates)
    {
        return new AcmeRenewalConfigurationSourceSet(
            acme.Enabled,
            acme.StoragePath,
            acme.DirectoryUrl,
            acme.ContactEmails,
            acme.TermsAccepted,
            acme.RetryAfterMinutes,
            acme.Certificates
                .Select(certificate => new AcmeRenewalCertificateSource(
                    certificate.Id,
                    certificate.Enabled,
                    certificate.Domains,
                    certificate.RenewBeforeDays,
                    ReadActiveAcmeCertificate(runtimeCertificates, certificate.Id)))
                .ToArray());
    }

    private static AcmeRenewalActiveCertificate? ReadActiveAcmeCertificate(
        IReadOnlyDictionary<string, RuntimeCertificate> runtimeCertificates,
        string certificateId)
    {
        if (!runtimeCertificates.TryGetValue(certificateId, out var certificate)
            || !string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new AcmeRenewalActiveCertificate(
            certificate.Certificate.NotBefore.ToUniversalTime(),
            certificate.Certificate.NotAfter.ToUniversalTime());
    }
}

public static class AcmeRenewalScheduleSourceMapper
{
    public static AcmeRenewalScheduleSource FromRuntimeConfiguration(RuntimeAcmeOptions acme)
    {
        return new AcmeRenewalScheduleSource(
            acme.Enabled,
            acme.CheckIntervalMinutes);
    }
}
