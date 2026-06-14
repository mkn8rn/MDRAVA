namespace MDRAVA.BLL.ControlPlane.Routing;

public interface IRouteMatcher
{
    RouteMatch? Match(IReadOnlyList<RouteMatchCandidate> routes, RouteMatchRequest request);
}
