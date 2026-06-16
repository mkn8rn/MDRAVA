using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Acme;

public static class AcmeRenewalConfigurationSourceMapper
{
    public static AcmeRenewalConfigurationSourceSet FromSources(
        RuntimeAcmeOptions acme,
        IEnumerable<KeyValuePair<string, RuntimeCertificate>> runtimeCertificates)
    {
        ArgumentNullException.ThrowIfNull(acme);
        ArgumentNullException.ThrowIfNull(runtimeCertificates);

        var activeCertificates = runtimeCertificates.ToDictionary(
            static certificate => certificate.Key,
            static certificate => certificate.Value,
            StringComparer.OrdinalIgnoreCase);

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
                    ReadActiveAcmeCertificate(activeCertificates, certificate.Id))));
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
    public static AcmeRenewalScheduleSource FromSource(RuntimeAcmeOptions acme)
    {
        ArgumentNullException.ThrowIfNull(acme);

        return new AcmeRenewalScheduleSource(
            acme.Enabled,
            acme.CheckIntervalMinutes);
    }
}
