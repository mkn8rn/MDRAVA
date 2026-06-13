using MDRAVA.BLL.ControlPlane.Acme;

namespace MDRAVA.BLL.ControlPlane.Status;

public static partial class ProxySubsystemSummaryBuilder
{
    public static ProxyCertificateSubsystemSummary BuildCertificates(
        ProxyCertificateSummarySource? source,
        DateTimeOffset now)
    {
        if (source is null)
        {
            return ProxyCertificateSubsystemSummary.Unknown;
        }

        HashSet<string> referenced = new(source.ReferencedCertificateIds, StringComparer.OrdinalIgnoreCase);
        HashSet<string> loadedIds = new(
            source.LoadedCertificates.Select(static certificate => certificate.Id),
            StringComparer.OrdinalIgnoreCase);
        var missing = referenced
            .Where(certificateId => !loadedIds.Contains(certificateId))
            .OrderBy(static certificateId => certificateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expired = source.LoadedCertificates
            .Where(certificate => certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime)
            .OrderByDescending(certificate => certificate.NotAfter.ToUniversalTime())
            .ToArray();
        var notYetValid = source.LoadedCertificates
            .Where(certificate => certificate.NotBefore.ToUniversalTime() > now.UtcDateTime)
            .OrderBy(certificate => certificate.NotBefore.ToUniversalTime())
            .ToArray();
        var expiringSoon = source.LoadedCertificates
            .Where(certificate =>
                certificate.NotAfter.ToUniversalTime() > now.UtcDateTime
                && certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime.AddDays(30))
            .OrderBy(certificate => certificate.NotAfter.ToUniversalTime())
            .ToArray();

        return new ProxyCertificateSubsystemSummary(
            referenced.Count,
            source.LoadedCertificates.Count,
            missing.Length,
            expired.Length,
            notYetValid.Length,
            expiringSoon.Length,
            BuildCertificateLastIssue(missing, expired, notYetValid, expiringSoon, now));
    }

    public static ProxyAcmeSubsystemSummary BuildAcme(
        ProxyAcmeSummaryConfigurationSource? configuration,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        DateTimeOffset now)
    {
        if (configuration is null)
        {
            return ProxyAcmeSubsystemSummary.Unknown;
        }

        var activeFailures = acmeStatuses
            .Where(IsCurrentAcmeFailure)
            .OrderByDescending(static status => status.LastFailedAtUtc)
            .ToArray();
        var renewalBackoffs = acmeStatuses
            .Where(status => status.NextAttemptNotBeforeUtc.HasValue && status.NextAttemptNotBeforeUtc > now)
            .OrderByDescending(static status => status.NextAttemptNotBeforeUtc)
            .ToArray();

        return new ProxyAcmeSubsystemSummary(
            configuration.Enabled,
            configuration.ConfiguredCertificates,
            acmeStatuses.Count(static status => status.Active),
            activeFailures.Length,
            renewalBackoffs.Length,
            BuildAcmeLastIssue(activeFailures, renewalBackoffs));
    }

    private static ProxySubsystemIssueSummary? BuildCertificateLastIssue(
        IReadOnlyList<string> missing,
        IReadOnlyList<ProxyCertificateValiditySource> expired,
        IReadOnlyList<ProxyCertificateValiditySource> notYetValid,
        IReadOnlyList<ProxyCertificateValiditySource> expiringSoon,
        DateTimeOffset now)
    {
        if (missing.Count > 0)
        {
            return Issue(now, "certificate", "missing_reference", missing[0]);
        }

        if (expired.Count > 0)
        {
            var certificate = expired[0];
            return Issue(CertificateTime(certificate.NotAfter), "certificate", "expired", certificate.Id);
        }

        if (notYetValid.Count > 0)
        {
            var certificate = notYetValid[0];
            return Issue(CertificateTime(certificate.NotBefore), "certificate", "not_yet_valid", certificate.Id);
        }

        if (expiringSoon.Count > 0)
        {
            var certificate = expiringSoon[0];
            return Issue(CertificateTime(certificate.NotAfter), "certificate", "expiring_soon", certificate.Id);
        }

        return null;
    }

    private static ProxySubsystemIssueSummary? BuildAcmeLastIssue(
        IReadOnlyList<AcmeCertificateLifecycleStatus> activeFailures,
        IReadOnlyList<AcmeCertificateLifecycleStatus> renewalBackoffs)
    {
        if (activeFailures.Count > 0)
        {
            var status = activeFailures[0];
            return Issue(status.LastFailedAtUtc!.Value, "acme", NormalizeAcmeReason(status.LastResult), status.CertificateId);
        }

        if (renewalBackoffs.Count > 0)
        {
            var status = renewalBackoffs[0];
            return Issue(status.NextAttemptNotBeforeUtc!.Value, "acme", "renewal_backoff", status.CertificateId);
        }

        return null;
    }

    private static bool IsCurrentAcmeFailure(AcmeCertificateLifecycleStatus status)
    {
        return status.LastFailedAtUtc.HasValue
            && (!status.LastSucceededAtUtc.HasValue || status.LastFailedAtUtc.Value > status.LastSucceededAtUtc.Value);
    }

    private static ProxySubsystemIssueSummary Issue(
        DateTimeOffset timestampUtc,
        string category,
        string reason,
        string? affectedIdentity)
    {
        return new ProxySubsystemIssueSummary(
            timestampUtc.ToUniversalTime(),
            category,
            reason,
            SafeIdentity(affectedIdentity));
    }

    private static DateTimeOffset CertificateTime(DateTime value)
    {
        return new DateTimeOffset(value.ToUniversalTime());
    }

    private static string NormalizeAcmeReason(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "attempting" => "attempting",
            "disabled" => ProxyStatusText.Disabled,
            "failed" => ProxyStatusText.Failed,
            "not-due" => "not_due",
            "succeeded" => "succeeded",
            _ => ProxyStatusText.Unknown
        };
    }

    private static string? SafeIdentity(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[Math.Min(value.Length, 128)];
        var length = 0;
        foreach (var character in value.Trim())
        {
            if (length >= buffer.Length)
            {
                break;
            }

            buffer[length++] = char.IsControl(character) ? '_' : character;
        }

        return new string(buffer[..length]);
    }
}
