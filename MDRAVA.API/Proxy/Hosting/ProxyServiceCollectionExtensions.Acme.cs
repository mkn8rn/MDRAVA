using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.INF.Acme;
using MDRAVA.INF.Observability;

namespace MDRAVA.API.Proxy.Hosting;

public static partial class ProxyServiceCollectionExtensions
{
    private static void AddProxyAcmeServices(this IServiceCollection services)
    {
        services.AddSingleton<AcmeChallengeStore>();
        services.AddSingleton<AcmeHttp01ChallengeResponder>();
        services.AddSingleton<AcmeCertificateStatusStore>();
        services.AddSingleton<IAcmeCertificateMaterialWriter, AcmeCertificateMaterialWriter>();
        services.AddSingleton<IAcmeCertificateRenewalEventSink, AcmeCertificateRenewalLogger>();
        services.AddSingleton<IProxyAcmeStatusConfigurationSource, ProxyAcmeStatusConfigurationSource>();
        services.AddSingleton<IProxyAcmeCertificateLifecycleStatusSource, ProxyAcmeCertificateLifecycleStatusSource>();
        services.AddSingleton<IProxyAcmeStatusSnapshotReader, ProxyAcmeStatusSnapshotReader>();
        services.AddSingleton<ProxyAcmeAdministrationService>();
        services.AddSingleton<IAcmeCertificateIssuer, DisabledAcmeCertificateIssuer>();
        services.AddSingleton<IAcmeRenewalScheduleInputSource, ProxyConfigurationAcmeRenewalScheduleInputSource>();
        services.AddSingleton<IAcmeRenewalConfigurationSource, ProxyConfigurationAcmeRenewalConfigurationSource>();
        services.AddSingleton<IAcmeCertificateActivator, ProxyConfigurationAcmeCertificateActivator>();
        services.AddSingleton<AcmeRenewalSchedulePolicy>();
        services.AddSingleton<AcmeCertificateManager>();
    }
}
