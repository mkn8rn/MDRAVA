namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeUpstream(
    string RouteName,
    string Name,
    string Address,
    int Port,
    int Weight)
{
    public string Endpoint => $"{Address}:{Port}";

    public string Identity => $"{RouteName}|{Name}|{Address}|{Port}";
}
