using MDRAVA.BLL.Infrastructure;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Acme;

public sealed class AcmeCertificateManager
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IAcmeCertificateIssuer _issuer;
    private readonly AcmeChallengeStore _challengeStore;
    private readonly AcmeCertificateStatusStore _statusStore;
    private readonly TimeProvider _timeProvider;
    private readonly ProxyMetrics? _metrics;
    private readonly ILogger<AcmeCertificateManager> _logger;

    public AcmeCertificateManager(
        IProxyConfigurationStore configurationStore,
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IAcmeCertificateIssuer issuer,
        AcmeChallengeStore challengeStore,
        AcmeCertificateStatusStore statusStore,
        TimeProvider timeProvider,
        ProxyMetrics? metrics,
        ILogger<AcmeCertificateManager> logger)
    {
        _configurationStore = configurationStore;
        _dataDirectoryProvider = dataDirectoryProvider;
        _issuer = issuer;
        _challengeStore = challengeStore;
        _statusStore = statusStore;
        _timeProvider = timeProvider;
        _metrics = metrics;
        _logger = logger;
    }

    public AcmeCertificateManager(
        IProxyConfigurationStore configurationStore,
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IAcmeCertificateIssuer issuer,
        AcmeChallengeStore challengeStore,
        AcmeCertificateStatusStore statusStore,
        TimeProvider timeProvider,
        ILogger<AcmeCertificateManager> logger)
        : this(configurationStore, dataDirectoryProvider, issuer, challengeStore, statusStore, timeProvider, null, logger)
    {
    }

    public async ValueTask CheckRenewalsAsync(CancellationToken cancellationToken)
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null || !snapshot.Acme.Enabled)
        {
            return;
        }

        AcmeCertificateMaterialStore.EnsureLayout(_dataDirectoryProvider.GetDataDirectory(), snapshot.Acme.StoragePath);
        var nowUtc = _timeProvider.GetUtcNow();
        foreach (var certificateOptions in snapshot.Acme.Certificates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CheckCertificateAsync(snapshot, certificateOptions, nowUtc, cancellationToken);
        }
    }

    private async ValueTask CheckCertificateAsync(
        ProxyConfigurationSnapshot snapshot,
        RuntimeAcmeCertificateOptions certificateOptions,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (!certificateOptions.Enabled)
        {
            _statusStore.Upsert(CreateStatus(snapshot, certificateOptions, nowUtc, "disabled", null, null));
            return;
        }

        var activeCertificate = snapshot.Certificates.TryGetValue(certificateOptions.Id, out var certificate)
            && string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase)
            ? certificate
            : null;
        var renewalDueAtUtc = CalculateRenewalDueAt(activeCertificate, certificateOptions);
        var existingStatus = _statusStore.Get(certificateOptions.Id);
        if (existingStatus?.NextAttemptNotBeforeUtc is not null
            && existingStatus.NextAttemptNotBeforeUtc > nowUtc
            && (activeCertificate is null || renewalDueAtUtc <= nowUtc))
        {
            _statusStore.Upsert(CopyHistory(
                CreateStatus(snapshot, certificateOptions, nowUtc, existingStatus.LastResult, existingStatus.ErrorSummary, existingStatus.NextAttemptNotBeforeUtc),
                existingStatus));
            return;
        }

        if (activeCertificate is not null && renewalDueAtUtc > nowUtc)
        {
            _statusStore.Upsert(CopyHistory(
                CreateStatus(snapshot, certificateOptions, nowUtc, "not-due", null, renewalDueAtUtc),
                existingStatus));
            return;
        }

        var attemptStartedAtUtc = nowUtc;
        _metrics?.AcmeRenewalAttempted();
        _statusStore.Upsert(CreateStatus(snapshot, certificateOptions, nowUtc, "attempting", null, null) with
        {
            LastAttemptAtUtc = attemptStartedAtUtc
        });

        var request = new AcmeCertificateIssueRequest(
            certificateOptions.Id,
            certificateOptions.Domains,
            snapshot.Acme.DirectoryUrl,
            snapshot.Acme.ContactEmails,
            snapshot.Acme.TermsAccepted);
        AcmeCertificateIssueResult result;
        try
        {
            result = await _issuer.IssueAsync(request, _challengeStore, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result = AcmeCertificateIssueResult.Failure(exception.Message);
        }

        if (!result.Succeeded || result.PfxBytes is null)
        {
            _metrics?.AcmeRenewalFailed();
            var nextAttempt = attemptStartedAtUtc.AddMinutes(snapshot.Acme.RetryAfterMinutes);
            _logger.LogWarning(
                "ACME renewal for certificate {CertificateId} failed: {ErrorSummary}",
                certificateOptions.Id,
                result.ErrorSummary);
            _statusStore.Upsert(CreateStatus(snapshot, certificateOptions, nowUtc, "failed", SafeError(result.ErrorSummary), nextAttempt) with
            {
                LastAttemptAtUtc = attemptStartedAtUtc,
                LastFailedAtUtc = attemptStartedAtUtc
            });
            return;
        }

        RuntimeCertificate renewedCertificate;
        try
        {
            renewedCertificate = AcmeCertificateMaterialStore.WriteAndLoad(
                snapshot.Acme,
                certificateOptions,
                _dataDirectoryProvider.GetDataDirectory(),
                result.PfxBytes);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _metrics?.AcmeRenewalFailed();
            var nextAttempt = attemptStartedAtUtc.AddMinutes(snapshot.Acme.RetryAfterMinutes);
            _statusStore.Upsert(CreateStatus(snapshot, certificateOptions, nowUtc, "failed", SafeError(exception.Message), nextAttempt) with
            {
                LastAttemptAtUtc = attemptStartedAtUtc,
                LastFailedAtUtc = attemptStartedAtUtc
            });
            return;
        }

        ReplaceCertificate(renewedCertificate);
        _metrics?.AcmeRenewalSucceeded();
        var refreshedSnapshot = _configurationStore.Snapshot;
        _statusStore.Upsert(CreateStatus(refreshedSnapshot, certificateOptions, nowUtc, "succeeded", null, CalculateRenewalDueAt(renewedCertificate, certificateOptions)) with
        {
            LastAttemptAtUtc = attemptStartedAtUtc,
            LastSucceededAtUtc = attemptStartedAtUtc
        });
    }

    private void ReplaceCertificate(RuntimeCertificate renewedCertificate)
    {
        var snapshot = _configurationStore.Snapshot;
        Dictionary<string, RuntimeCertificate> certificates = new(snapshot.Certificates, StringComparer.OrdinalIgnoreCase)
        {
            [renewedCertificate.Id] = renewedCertificate
        };

        _configurationStore.Replace(snapshot with { Certificates = certificates });
    }

    private AcmeCertificateLifecycleStatus CreateStatus(
        ProxyConfigurationSnapshot snapshot,
        RuntimeAcmeCertificateOptions certificateOptions,
        DateTimeOffset nowUtc,
        string result,
        string? errorSummary,
        DateTimeOffset? nextAttemptNotBeforeUtc)
    {
        var active = snapshot.Certificates.TryGetValue(certificateOptions.Id, out var certificate)
            && string.Equals(certificate.Source, "acme", StringComparison.OrdinalIgnoreCase);
        var renewalDueAtUtc = CalculateRenewalDueAt(active ? certificate : null, certificateOptions);
        return new AcmeCertificateLifecycleStatus(
            certificateOptions.Id,
            certificateOptions.Enabled,
            certificateOptions.Domains,
            active,
            active ? "acme" : "none",
            active ? certificate!.Certificate.NotBefore.ToUniversalTime() : null,
            active ? certificate!.Certificate.NotAfter.ToUniversalTime() : null,
            active ? renewalDueAtUtc : nowUtc,
            null,
            null,
            null,
            nextAttemptNotBeforeUtc,
            result,
            errorSummary);
    }

    private static DateTimeOffset CalculateRenewalDueAt(
        RuntimeCertificate? certificate,
        RuntimeAcmeCertificateOptions certificateOptions)
    {
        if (certificate is null)
        {
            return DateTimeOffset.MinValue;
        }

        return new DateTimeOffset(certificate.Certificate.NotAfter.ToUniversalTime())
            .AddDays(-certificateOptions.RenewBeforeDays);
    }

    private static string? SafeError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return null;
        }

        var sanitized = new string(error.Where(static character => !char.IsControl(character)).ToArray());
        return sanitized.Length <= 256 ? sanitized : sanitized[..256];
    }

    private static AcmeCertificateLifecycleStatus CopyHistory(
        AcmeCertificateLifecycleStatus status,
        AcmeCertificateLifecycleStatus? previous)
    {
        if (previous is null)
        {
            return status;
        }

        return status with
        {
            LastAttemptAtUtc = previous.LastAttemptAtUtc,
            LastSucceededAtUtc = previous.LastSucceededAtUtc,
            LastFailedAtUtc = previous.LastFailedAtUtc
        };
    }
}
