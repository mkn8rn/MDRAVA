namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed record RouteMatch(int RouteIndex);

public sealed record RouteMatchCandidate(string Host, string PathPrefix);

public sealed record RouteMatchRequest(string Host, string Path);
