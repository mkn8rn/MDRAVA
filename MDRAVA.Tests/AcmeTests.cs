using MDRAVA.API.Controllers;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class AcmeTests
{
    public static async Task ManualPfxCertificateBehaviorRemainsValid()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "manual.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "manual.test", "secret");
        ConfigurationTests.WriteHttpsSite(temp.Path, "manual.json", 18443, 15000, "manual-cert");
        ConfigurationTests.WriteOperationalConfig(
            temp.Path,
            certificateId: "manual-cert",
            certificatePath: "certs/manual.pfx",
            certificatePassword: "secret");

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        var certificate = snapshot.Certificates["manual-cert"];
        AssertEx.Equal("manualPfx", certificate.Source);
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            TestHttp3PlatformSupport.Supported);
        AssertEx.Equal("manualPfx", projection.Certificates[0].Source);
    }

    public static void AcmeConfigValidationRejectsMissingTermsAcceptance()
    {
        var failures = ProxyOperationalOptionsValidationRules.Validate(
            new ProxyOperationalOptions
            {
                Acme = new ProxyAcmeOptions
                {
                    Enabled = true,
                    TermsAccepted = false,
                    Certificates =
                    [
                        new AcmeManagedCertificateOptions
                        {
                            Id = "home-acme",
                            Domains = ["home.example.test"]
                        }
                    ]
                }
            },
            static _ => null);

        AssertEx.True(failures.Any(static failure => failure.Contains("TermsAccepted", StringComparison.Ordinal)));
    }

    public static void Http01ChallengeReturnsExactTokenResponse()
    {
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var store = new AcmeChallengeStore();
        AssertEx.True(store.TryRegister("abc_123-token", "abc_123-token.thumbprint", time.GetUtcNow().AddMinutes(5)));
        var responder = new AcmeHttp01ChallengeResponder(store, time);

        var handled = responder.TryCreateResponse(
            Request("GET", "/.well-known/acme-challenge/abc_123-token"),
            out var response);

        AssertEx.True(handled);
        AssertEx.Equal(200, response.StatusCode);
        AssertEx.Equal("abc_123-token.thumbprint", response.Body);
    }

    public static void UnknownHttp01ChallengeReturnsSafe404()
    {
        var responder = new AcmeHttp01ChallengeResponder(
            new AcmeChallengeStore(),
            new ManualTimeProvider(DateTimeOffset.UtcNow));

        var handled = responder.TryCreateResponse(
            Request("GET", "/.well-known/acme-challenge/missing"),
            out var response);

        AssertEx.True(handled);
        AssertEx.Equal(404, response.StatusCode);
    }

    public static async Task AcmeRenewalStoresMaterialUnderCertsDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        var issuer = new FakeIssuer(TestCertificates.CreateSelfSignedPfxBytes("home.example.test"));
        var store = CreateStore(temp.Path);
        var statusStore = new AcmeCertificateStatusStore();
        var manager = CreateManager(temp.Path, store, issuer, statusStore);

        await manager.CheckRenewalsAsync(CancellationToken.None);

        var layout = AcmeCertificateMaterialStore.GetLayout(temp.Path, "acme");
        AssertEx.True(File.Exists(AcmeCertificateMaterialStore.GetPrivateKeyPfxPath(layout, "home-acme")));
        AssertEx.True(File.Exists(AcmeCertificateMaterialStore.GetCertificatePemPath(layout, "home-acme")));
        AssertEx.True(File.Exists(AcmeCertificateMaterialStore.GetMetadataPath(layout, "home-acme")));
        AssertEx.Equal("acme", store.Snapshot.Certificates["home-acme"].Source);
        AssertEx.True(statusStore.Get("home-acme")?.Active == true);
    }

    public static async Task LoaderLoadsStoredAcmeCertificateOnStartup()
    {
        using var temp = TemporaryDirectory.Create();
        var acmeOptions = new ProxyAcmeOptions
        {
            Enabled = true,
            TermsAccepted = true,
            Certificates =
            [
                new AcmeManagedCertificateOptions
                {
                    Id = "home-acme",
                    Domains = ["home.example.test"]
                }
            ]
        };
        var runtimeAcme = ProxyConfigurationRuntimeMapper.ToRuntimeAcmeOptions(acmeOptions);
        AcmeCertificateMaterialStore.WriteAndLoad(
            runtimeAcme,
            runtimeAcme.Certificates[0],
            temp.Path,
            TestCertificates.CreateSelfSignedPfxBytes("home.example.test"));
        var config = Directory.CreateDirectory(Path.Combine(temp.Path, "config")).FullName;
        File.WriteAllText(
            Path.Combine(config, "proxy.json"),
            """
            {
              "acme": {
                "enabled": true,
                "termsAccepted": true,
                "certificates": [
                  {
                    "id": "home-acme",
                    "domains": [ "home.example.test" ]
                  }
                ]
              }
            }
            """);

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.Equal("acme", AssertEx.NotNull(result.Snapshot).Certificates["home-acme"].Source);
    }

    public static async Task FailedAcmeRenewalPreservesCurrentActiveCertificate()
    {
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path);
        var initial = AcmeCertificateMaterialStore.WriteAndLoad(
            store.Snapshot.Acme,
            store.Snapshot.Acme.Certificates[0],
            temp.Path,
            TestCertificates.CreateSelfSignedPfxBytes("home.example.test", validDays: 10));
        store.Replace(store.Snapshot with
        {
            Certificates = new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase)
            {
                ["home-acme"] = initial
            }
        });
        var originalThumbprint = initial.Certificate.Thumbprint;
        var statusStore = new AcmeCertificateStatusStore();
        var manager = CreateManager(temp.Path, store, new FakeIssuer("issuer failed"), statusStore);

        await manager.CheckRenewalsAsync(CancellationToken.None);

        AssertEx.Equal(originalThumbprint, store.Snapshot.Certificates["home-acme"].Certificate.Thumbprint);
        AssertEx.Equal("failed", AssertEx.NotNull(statusStore.Get("home-acme")).LastResult);
    }

    public static async Task AcmeStatusProjectionDoesNotExposePrivateMaterial()
    {
        using var temp = TemporaryDirectory.Create();
        var store = CreateStore(temp.Path);
        var statusStore = new AcmeCertificateStatusStore();
        var manager = CreateManager(
            temp.Path,
            store,
            new FakeIssuer(TestCertificates.CreateSelfSignedPfxBytes("home.example.test")),
            statusStore);
        await manager.CheckRenewalsAsync(CancellationToken.None);
        var controller = new ProxyAcmeController(
            new ProxyAcmeAdministrationService(
                CreateStatusReader(store, statusStore)));

        var result = controller.Status();
        var ok = (OkObjectResult)AssertEx.NotNull(result.Result);
        var status = (AcmeStatusResponse)AssertEx.NotNull(ok.Value);
        var text = status.ToString();

        AssertEx.False(text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(text.Contains("current.pfx", StringComparison.OrdinalIgnoreCase));
        AssertEx.False(text.Contains(temp.Path, StringComparison.OrdinalIgnoreCase));
    }

    public static void AcmeStatusSnapshotReaderProjectsSourceState()
    {
        var notBefore = DateTimeOffset.UnixEpoch.AddDays(1);
        var notAfter = DateTimeOffset.UnixEpoch.AddDays(91);
        var lifecycle = new AcmeCertificateLifecycleStatus(
            "home-acme",
            true,
            ["home.example.test"],
            true,
            "acme",
            notBefore,
            notAfter,
            notAfter.AddDays(-30),
            null,
            null,
            null,
            null,
            "loaded",
            null);
        var reader = new ProxyAcmeStatusSnapshotReader(
            new FixedAcmeStatusConfigurationSource(new ProxyAcmeStatusConfigurationSourceSnapshot(
                true,
                "https://acme.example.test/directory",
                true,
                [new ProxyAcmeConfiguredCertificateSource("home-acme", true, ["home.example.test"], 30)],
                [new ProxyAcmeRuntimeCertificateSource("home-acme", "home-acme", "acme", notBefore, notAfter)])),
            new FixedAcmeCertificateLifecycleStatusSource([lifecycle]));

        var found = reader.TryGetSnapshot(out var snapshot);
        var statuses = reader.GetLifecycleStatuses();
        var missingReader = new ProxyAcmeStatusSnapshotReader(
            new FixedAcmeStatusConfigurationSource(null),
            new FixedAcmeCertificateLifecycleStatusSource([]));
        var missing = missingReader.TryGetSnapshot(out var missingSnapshot);

        AssertEx.True(found);
        var projected = AssertEx.NotNull(snapshot);
        AssertEx.True(projected.Enabled);
        AssertEx.Equal("https://acme.example.test/directory", projected.DirectoryUrl);
        AssertEx.True(projected.UseStaging);
        AssertEx.Equal(1, projected.Certificates.Count);
        AssertEx.Equal("home-acme", projected.Certificates[0].Id);
        AssertEx.Equal("home.example.test", projected.Certificates[0].Domains[0]);
        AssertEx.Equal(30, projected.Certificates[0].RenewBeforeDays);
        AssertEx.True(projected.RuntimeCertificates.ContainsKey("home-acme"));
        AssertEx.Equal("acme", projected.RuntimeCertificates["home-acme"].Source);
        AssertEx.Equal(notBefore, projected.RuntimeCertificates["home-acme"].NotBeforeUtc);
        AssertEx.Equal(notAfter, projected.RuntimeCertificates["home-acme"].NotAfterUtc);
        AssertEx.Equal(1, statuses.Count);
        AssertEx.Equal(lifecycle, statuses[0]);
        AssertEx.False(missing);
        AssertEx.Equal(null, missingSnapshot);
    }

    public static async Task AcmeRenewalAvoidsTightRetryLoopAfterFailure()
    {
        using var temp = TemporaryDirectory.Create();
        var issuer = new FakeIssuer("issuer failed");
        var statusStore = new AcmeCertificateStatusStore();
        var now = DateTimeOffset.UtcNow;
        var manager = CreateManager(
            temp.Path,
            CreateStore(temp.Path),
            issuer,
            statusStore,
            new ManualTimeProvider(now));

        await manager.CheckRenewalsAsync(CancellationToken.None);
        await manager.CheckRenewalsAsync(CancellationToken.None);

        AssertEx.Equal(1, issuer.Calls);
        AssertEx.True(statusStore.Get("home-acme")?.NextAttemptNotBeforeUtc > now);
    }

    public static void AcmeRenewalScheduleUsesDisabledBackoffWithoutActiveConfig()
    {
        var delay = new AcmeRenewalSchedulePolicy().ResolveDelay(null);

        AssertEx.Equal(TimeSpan.FromHours(12), delay);
    }

    public static void AcmeRenewalScheduleClampsConfiguredInterval()
    {
        using var temp = TemporaryDirectory.Create();
        var policy = new AcmeRenewalSchedulePolicy();
        var belowMinimum = CreateStore(temp.Path, checkIntervalMinutes: 1).Snapshot;
        var aboveMaximum = CreateStore(temp.Path, checkIntervalMinutes: 2000).Snapshot;

        AssertEx.Equal(TimeSpan.FromMinutes(5), policy.ResolveDelay(belowMinimum));
        AssertEx.Equal(TimeSpan.FromMinutes(1440), policy.ResolveDelay(aboveMaximum));
    }

    private static Http1RequestHead Request(string method, string path)
    {
        return new Http1RequestHead(
            method,
            path,
            path,
            "HTTP/1.1",
            "home.example.test",
            Http1RequestFraming.None,
            []);
    }

    private static ProxyConfigurationStore CreateStore(
        string dataDirectory,
        int checkIntervalMinutes = 60)
    {
        var options = new ProxyOperationalOptions
        {
            Acme = new ProxyAcmeOptions
            {
                Enabled = true,
                TermsAccepted = true,
                RetryAfterMinutes = 60,
                CheckIntervalMinutes = checkIntervalMinutes,
                Certificates =
                [
                    new AcmeManagedCertificateOptions
                    {
                        Id = "home-acme",
                        Domains = ["home.example.test"],
                        RenewBeforeDays = 30
                    }
                ]
            }
        };
        var snapshot = ProxyConfigurationRuntimeMapper.ToRuntimeSnapshot(
            new ProxyOptions(),
            options,
            ProxyAdminSecurityTokenPolicy.Resolve(options.Admin, static _ => null),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            1,
            DateTimeOffset.UtcNow,
            Path.Combine(dataDirectory, "config", "sites"),
            [],
            new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout(
                    dataDirectory,
                    Path.Combine(dataDirectory, "config"),
                    Path.Combine(dataDirectory, "config", "sites"),
                    Path.Combine(dataDirectory, "logs"),
                    Path.Combine(dataDirectory, "certs"),
                    Path.Combine(dataDirectory, "state"),
                    Path.Combine(dataDirectory, "config", "proxy.json")),
                [],
                [],
                []));
        var store = new ProxyConfigurationStore();
        store.Replace(snapshot);
        return store;
    }

    private static AcmeCertificateManager CreateManager(
        string dataDirectory,
        ProxyConfigurationStore store,
        IAcmeCertificateIssuer issuer,
        AcmeCertificateStatusStore statusStore,
        TimeProvider? timeProvider = null)
    {
        return new AcmeCertificateManager(
            store,
            new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            }),
            issuer,
            new AcmeCertificateMaterialWriter(),
            new AcmeChallengeStore(),
            statusStore,
            timeProvider ?? new ManualTimeProvider(DateTimeOffset.UtcNow),
            new ProxyMetrics(),
            SilentAcmeCertificateRenewalEventSink.Instance);
    }

    private static ProxyAcmeStatusSnapshotReader CreateStatusReader(
        ProxyConfigurationStore store,
        AcmeCertificateStatusStore statusStore)
    {
        return new ProxyAcmeStatusSnapshotReader(
            new ProxyAcmeStatusConfigurationSource(store),
            new ProxyAcmeCertificateLifecycleStatusSource(statusStore));
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        });
        return new ProxyConfigurationLoader(
            provider,
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private sealed class FakeIssuer : IAcmeCertificateIssuer
    {
        private readonly byte[]? _pfxBytes;
        private readonly string? _error;

        public FakeIssuer(byte[] pfxBytes)
        {
            _pfxBytes = pfxBytes;
        }

        public FakeIssuer(string error)
        {
            _error = error;
        }

        public int Calls { get; private set; }

        public ValueTask<AcmeCertificateIssueResult> IssueAsync(
            AcmeCertificateIssueRequest request,
            AcmeChallengeStore challengeStore,
            CancellationToken cancellationToken)
        {
            _ = request;
            _ = challengeStore;
            _ = cancellationToken;
            Calls++;
            return ValueTask.FromResult(_pfxBytes is not null
                ? AcmeCertificateIssueResult.Success(_pfxBytes)
                : AcmeCertificateIssueResult.Failure(_error ?? "failed"));
        }
    }

    private sealed class SilentAcmeCertificateRenewalEventSink : IAcmeCertificateRenewalEventSink
    {
        public static SilentAcmeCertificateRenewalEventSink Instance { get; } = new();

        private SilentAcmeCertificateRenewalEventSink()
        {
        }

        public void RenewalFailed(string certificateId, string? errorSummary)
        {
        }
    }

    private sealed class FixedAcmeStatusConfigurationSource : IProxyAcmeStatusConfigurationSource
    {
        private readonly ProxyAcmeStatusConfigurationSourceSnapshot? _snapshot;

        public FixedAcmeStatusConfigurationSource(ProxyAcmeStatusConfigurationSourceSnapshot? snapshot)
        {
            _snapshot = snapshot;
        }

        public bool TryGetSnapshot(out ProxyAcmeStatusConfigurationSourceSnapshot? snapshot)
        {
            snapshot = _snapshot;
            return snapshot is not null;
        }
    }

    private sealed class FixedAcmeCertificateLifecycleStatusSource
        : IProxyAcmeCertificateLifecycleStatusSource
    {
        private readonly IReadOnlyList<AcmeCertificateLifecycleStatus> _statuses;

        public FixedAcmeCertificateLifecycleStatusSource(
            IReadOnlyList<AcmeCertificateLifecycleStatus> statuses)
        {
            _statuses = statuses;
        }

        public IReadOnlyList<AcmeCertificateLifecycleStatus> GetLifecycleStatuses()
        {
            return _statuses;
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-acme-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
