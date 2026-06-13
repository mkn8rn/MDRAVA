using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeRenewalScheduleInput(
    bool Enabled,
    int CheckIntervalMinutes);

public sealed record AcmeRenewalScheduleSource(
    bool Enabled,
    int CheckIntervalMinutes);

public interface IAcmeRenewalScheduleInputSource
{
    AcmeRenewalScheduleInputReadResult ReadInput();
}

public abstract record AcmeRenewalScheduleInputReadResult
{
    private AcmeRenewalScheduleInputReadResult()
    {
    }

    public static AcmeRenewalScheduleInputReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static AcmeRenewalScheduleInputReadResult Available(AcmeRenewalScheduleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new AvailableResult(input);
    }

    public sealed record AvailableResult : AcmeRenewalScheduleInputReadResult
    {
        public AvailableResult(AcmeRenewalScheduleInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            Input = input;
        }

        public AcmeRenewalScheduleInput Input { get; }
    }

    public sealed record MissingConfigurationResult : AcmeRenewalScheduleInputReadResult;
}

public static class AcmeRenewalScheduleInputMapper
{
    public static AcmeRenewalScheduleInput FromSource(AcmeRenewalScheduleSource source)
    {
        return new AcmeRenewalScheduleInput(
            source.Enabled,
            source.CheckIntervalMinutes);
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

public sealed class ProxyConfigurationAcmeRenewalScheduleInputSource : IAcmeRenewalScheduleInputSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationAcmeRenewalScheduleInputSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public AcmeRenewalScheduleInputReadResult ReadInput()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? AcmeRenewalScheduleInputReadResult.Available(
                AcmeRenewalScheduleInputMapper.FromSource(
                    AcmeRenewalScheduleSourceMapper.FromRuntimeConfiguration(available.Snapshot.Acme)))
            : AcmeRenewalScheduleInputReadResult.MissingConfiguration;
    }
}

public sealed class AcmeRenewalSchedulePolicy
{
    public TimeSpan ResolveDelay(AcmeRenewalScheduleInputReadResult input)
    {
        if (input is AcmeRenewalScheduleInputReadResult.AvailableResult { Input.Enabled: true } available)
        {
            return TimeSpan.FromMinutes(Math.Clamp(available.Input.CheckIntervalMinutes, 5, 1440));
        }

        return TimeSpan.FromHours(12);
    }
}
