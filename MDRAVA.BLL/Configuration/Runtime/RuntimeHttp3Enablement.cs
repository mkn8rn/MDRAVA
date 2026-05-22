namespace MDRAVA.BLL.Configuration;

public enum RuntimeHttp3Enablement
{
    Default,
    Disabled
}

public static class RuntimeHttp3EnablementExtensions
{
    public static string ToConfigText(this RuntimeHttp3Enablement enablement)
    {
        return enablement switch
        {
            RuntimeHttp3Enablement.Default => "default",
            _ => "disabled"
        };
    }
}
