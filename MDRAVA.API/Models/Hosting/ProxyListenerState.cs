namespace MDRAVA.API.Models.Hosting;

public enum ProxyListenerState
{
    Starting = 0,
    Active = 1,
    Draining = 2,
    Stopped = 3,
    Failed = 4
}
