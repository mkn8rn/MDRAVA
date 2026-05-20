namespace MDRAVA.API.Models.Configuration.Runtime;

public enum RuntimeHttp3Enablement
{
    Default,
    Disabled,
    // Legacy explicit modes are still accepted for config compatibility.
    Preview,
    Beta
}

public static class RuntimeHttp3EnablementExtensions
{
    public static string ToConfigText(this RuntimeHttp3Enablement enablement)
    {
        return enablement switch
        {
            RuntimeHttp3Enablement.Default => "default",
            RuntimeHttp3Enablement.Beta => "beta",
            RuntimeHttp3Enablement.Preview => "preview",
            _ => "disabled"
        };
    }
}
