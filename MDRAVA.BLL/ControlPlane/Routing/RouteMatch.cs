namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed record RouteMatch
{
    public RouteMatch(int RouteIndex)
    {
        RouteMatchFacts.ValidateRouteIndex(RouteIndex);

        this.RouteIndex = RouteIndex;
    }

    public int RouteIndex { get; }
}

public sealed record RouteMatchCandidate
{
    public RouteMatchCandidate(
        string Host,
        string PathPrefix)
    {
        RouteMatchFacts.ValidateCandidate(Host, PathPrefix);

        this.Host = Host;
        this.PathPrefix = PathPrefix;
    }

    public string Host { get; }

    public string PathPrefix { get; }
}

public sealed record RouteMatchRequest
{
    public RouteMatchRequest(
        string Host,
        string Path)
    {
        RouteMatchFacts.ValidateRequest(Host, Path);

        this.Host = Host;
        this.Path = Path;
    }

    public string Host { get; }

    public string Path { get; }
}
