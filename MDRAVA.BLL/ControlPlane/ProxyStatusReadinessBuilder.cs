using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public static class ProxyStatusReadinessBuilder
{
    public static (ProxyReadinessStatus Readiness, ProxySubsystemSummaries Subsystems) Build(
        ProxyConfigurationSnapshot? snapshot,
        ProxyRuntimeSnapshot runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence,
        ProxyCacheStatusResponse? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        ProxyRuntimePreflightStatus? runtimePreflight = null)
    {
        var now = DateTimeOffset.UtcNow;
        var runtimePreflightStatus = runtimePreflight ?? ProxyRuntimePreflightStatus.Unknown;
        var subsystems = BuildSubsystems(snapshot, runtime, metrics, upstreams, http3, logPersistence, cacheStatus, acmeStatuses, now);
        var readiness = ProxyReadinessEvaluator.Evaluate(new ProxyReadinessEvaluationInput(
            snapshot is not null,
            snapshot?.Version,
            runtime.IsShuttingDown,
            runtime.LastListenerReload is { Succeeded: false },
            logPersistence.State,
            runtimePreflightStatus,
            subsystems,
            now));
        return (readiness, subsystems);
    }

    private static ProxySubsystemSummaries BuildSubsystems(
        ProxyConfigurationSnapshot? snapshot,
        ProxyRuntimeSnapshot runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence,
        ProxyCacheStatusResponse? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        DateTimeOffset now)
    {
        var listeners = snapshot?.Listeners ?? [];
        var activeListeners = runtime.Listeners.Count(static listener => listener.State == ProxyListenerState.Active);
        var failedListeners = runtime.Listeners.Count(static listener => listener.State == ProxyListenerState.Failed);
        var drainingListeners = runtime.Listeners.Count(static listener => listener.State == ProxyListenerState.Draining);
        var routes = snapshot?.Routes ?? [];
        var listenerSummary = new ProxyListenerSubsystemSummary(
            listeners.Count,
            listeners.Count(static listener => listener.Enabled),
            activeListeners,
            failedListeners,
            drainingListeners,
            listeners.Count(static listener => listener.Enabled && listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1)),
            listeners.Count(static listener => listener.Enabled && listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2)),
            listeners.Count(static listener => listener.Enabled && listener.Http3.EnabledForTraffic),
            runtime.Listeners.Count(static listener => listener.Kind == "quic" && listener.State == ProxyListenerState.Active));
        var routeSummary = new ProxyRouteSubsystemSummary(
            routes.Select(static route => route.SiteName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            routes.Count,
            routes.Count(static route => route.Action == RuntimeRouteAction.Proxy),
            routes.Count(static route => route.Action != RuntimeRouteAction.Proxy),
            routes.Count(static route => route.Cache.Enabled));
        var certificateSummary = BuildCertificateSummary(snapshot, now);
        var acmeSummary = BuildAcmeSummary(snapshot, acmeStatuses, now);
        var upstreamSummary = new ProxyUpstreamSubsystemSummary(
            upstreams.Count,
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Healthy),
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Unhealthy),
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Unknown),
            upstreams.Count(static upstream => upstream.HealthCheckEnabled));
        var cacheSummary = new ProxyCacheSubsystemSummary(
            routes.Any(static route => route.Cache.Enabled),
            routes.Count(static route => route.Cache.Enabled),
            cacheStatus?.EntryCount ?? 0,
            cacheStatus?.ApproximateBytes ?? 0);
        var circuitSummary = new ProxyCircuitSubsystemSummary(
            upstreams.Count(static upstream => upstream.CircuitBreaker.Enabled),
            upstreams.Count(static upstream => upstream.CircuitBreaker.State == CircuitBreakerRuntimeState.Open),
            upstreams.Count(static upstream => upstream.CircuitBreaker.State == CircuitBreakerRuntimeState.HalfOpen),
            upstreams.Count(static upstream => upstream.CircuitBreaker.State == CircuitBreakerRuntimeState.Closed));
        var limitsSummary = new ProxyLimitSubsystemSummary(
            snapshot?.Limits.MaxActiveClientConnections ?? 0,
            metrics.ActiveConnections,
            snapshot?.Limits.MaxConcurrentTlsHandshakes ?? 0,
            metrics.ActiveTlsHandshakes,
            metrics.ActiveHttp2Streams,
            metrics.ActiveHttp3Streams,
            metrics.ActiveUpstreamHttp3Streams,
            snapshot?.Limits.RequestsPerMinutePerIp ?? 0);
        var logsSummary = new ProxyLogSubsystemSummary(
            logPersistence.AccessLogEnabled,
            logPersistence.AdminAuditEnabled,
            logPersistence.State,
            logPersistence.Reason);
        var shutdownSummary = new ProxyShutdownSubsystemSummary(
            runtime.IsRunning,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc);
        var protocolSummary = new ProxyProtocolSubsystemSummary(
            listeners.Any(static listener => listener.Enabled && listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1)),
            listeners.Any(static listener => listener.Enabled && listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2)),
            http3.EnabledForTraffic,
            http3.QuicListenerReady,
            routes.SelectMany(static route => route.Upstreams)
                .Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol)),
            RuntimeHttp3UnsupportedFeatureCodes.StatusSummary);
        var configSummary = new ProxyConfigSubsystemSummary(
            snapshot is not null,
            snapshot?.Version,
            snapshot?.LoadedAtUtc,
            runtime.LastListenerReload?.Succeeded,
            runtime.LastListenerReload is null
                ? null
                : runtime.LastListenerReload.Succeeded ? "listener_reload_succeeded" : "listener_reload_failed");

        return new ProxySubsystemSummaries(
            configSummary,
            listenerSummary,
            routeSummary,
            certificateSummary,
            acmeSummary,
            upstreamSummary,
            cacheSummary,
            circuitSummary,
            limitsSummary,
            logsSummary,
            shutdownSummary,
            protocolSummary);
    }

    private static ProxyCertificateSubsystemSummary BuildCertificateSummary(
        ProxyConfigurationSnapshot? snapshot,
        DateTimeOffset now)
    {
        if (snapshot is null)
        {
            return ProxyCertificateSubsystemSummary.Unknown;
        }

        HashSet<string> referenced = new(StringComparer.OrdinalIgnoreCase);
        foreach (var listener in snapshot.Listeners)
        {
            if (!string.IsNullOrWhiteSpace(listener.DefaultCertificateId))
            {
                referenced.Add(listener.DefaultCertificateId);
            }

            foreach (var binding in listener.SniCertificates)
            {
                referenced.Add(binding.CertificateId);
            }
        }

        var loaded = snapshot.Certificates.Values.ToArray();
        var missing = referenced
            .Where(certificateId => !snapshot.Certificates.ContainsKey(certificateId))
            .OrderBy(static certificateId => certificateId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var expired = loaded
            .Where(certificate => certificate.Certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime)
            .OrderByDescending(certificate => certificate.Certificate.NotAfter.ToUniversalTime())
            .ToArray();
        var notYetValid = loaded
            .Where(certificate => certificate.Certificate.NotBefore.ToUniversalTime() > now.UtcDateTime)
            .OrderBy(certificate => certificate.Certificate.NotBefore.ToUniversalTime())
            .ToArray();
        var expiringSoon = loaded
            .Where(certificate =>
                certificate.Certificate.NotAfter.ToUniversalTime() > now.UtcDateTime
                && certificate.Certificate.NotAfter.ToUniversalTime() <= now.UtcDateTime.AddDays(30))
            .OrderBy(certificate => certificate.Certificate.NotAfter.ToUniversalTime())
            .ToArray();
        return new ProxyCertificateSubsystemSummary(
            referenced.Count,
            loaded.Length,
            missing.Length,
            expired.Length,
            notYetValid.Length,
            expiringSoon.Length,
            BuildCertificateLastIssue(missing, expired, notYetValid, expiringSoon, now));
    }

    private static ProxyAcmeSubsystemSummary BuildAcmeSummary(
        ProxyConfigurationSnapshot? snapshot,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        DateTimeOffset now)
    {
        if (snapshot is null)
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
            snapshot.Acme.Enabled,
            snapshot.Acme.Certificates.Count(static certificate => certificate.Enabled),
            acmeStatuses.Count(static status => status.Active),
            activeFailures.Length,
            renewalBackoffs.Length,
            BuildAcmeLastIssue(activeFailures, renewalBackoffs));
    }

    private static ProxySubsystemIssueSummary? BuildCertificateLastIssue(
        IReadOnlyList<string> missing,
        IReadOnlyList<RuntimeCertificate> expired,
        IReadOnlyList<RuntimeCertificate> notYetValid,
        IReadOnlyList<RuntimeCertificate> expiringSoon,
        DateTimeOffset now)
    {
        if (missing.Count > 0)
        {
            return Issue(now, "certificate", "missing_reference", missing[0]);
        }

        if (expired.Count > 0)
        {
            var certificate = expired[0];
            return Issue(CertificateTime(certificate.Certificate.NotAfter), "certificate", "expired", certificate.Id);
        }

        if (notYetValid.Count > 0)
        {
            var certificate = notYetValid[0];
            return Issue(CertificateTime(certificate.Certificate.NotBefore), "certificate", "not_yet_valid", certificate.Id);
        }

        if (expiringSoon.Count > 0)
        {
            var certificate = expiringSoon[0];
            return Issue(CertificateTime(certificate.Certificate.NotAfter), "certificate", "expiring_soon", certificate.Id);
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
