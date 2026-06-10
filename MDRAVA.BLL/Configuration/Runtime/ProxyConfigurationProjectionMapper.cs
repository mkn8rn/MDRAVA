using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.Configuration;

public static class ProxyConfigurationProjectionMapper
{
    public static ProxyConfigurationProjection ToProjection(ProxyConfigurationSnapshot snapshot)
    {
        var adminSecurity = new RuntimeAdminSecurityProjection(
            snapshot.AdminSecurity.Urls,
            snapshot.AdminSecurity.RequireAuthentication,
            snapshot.AdminSecurity.HasConfiguredToken,
            SecretRedactor.RedactConfiguredSecret(snapshot.AdminSecurity.HasConfiguredToken),
            snapshot.AdminSecurity.TokenEnvironmentVariable,
            snapshot.AdminSecurity.TokenSource,
            snapshot.AdminSecurity.RecentAuditCapacity);
        var acme = new RuntimeAcmeProjection(
            snapshot.Acme.Enabled,
            snapshot.Acme.UseStaging,
            snapshot.Acme.DirectoryUrl,
            snapshot.Acme.ContactEmails,
            snapshot.Acme.TermsAccepted,
            snapshot.Acme.StoragePath,
            snapshot.Acme.RenewBeforeDays,
            snapshot.Acme.CheckIntervalMinutes,
            snapshot.Acme.RetryAfterMinutes,
            snapshot.Acme.Certificates);

        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Discovery,
            adminSecurity,
            acme,
            snapshot.Timeouts,
            snapshot.ConnectionLimits,
            snapshot.Observability,
            snapshot.Limits,
            snapshot.ForwardedHeaders,
            snapshot.Certificates.Values
                .Select(static certificate => new RuntimeCertificateProjection(
                    certificate.Id,
                    certificate.Path,
                    certificate.Format,
                    certificate.Source,
                    certificate.Domains ?? [],
                    certificate.HasConfiguredPassword,
                    certificate.Certificate.Subject,
                    certificate.Certificate.Thumbprint,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .OrderBy(static certificate => certificate.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            snapshot.Listeners,
            snapshot.Routes)
        {
            Metrics = snapshot.Metrics,
            Http3 = Http3RuntimeSupport.Project(snapshot.Listeners, routes: snapshot.Routes)
        };
    }
}
