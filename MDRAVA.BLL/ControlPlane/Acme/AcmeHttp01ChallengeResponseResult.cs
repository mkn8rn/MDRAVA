using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Acme;

public abstract record AcmeHttp01ChallengeResponseResult
{
    private AcmeHttp01ChallengeResponseResult()
    {
    }

    public static AcmeHttp01ChallengeResponseResult NoMatch { get; } = new NoMatchResult();

    public static AcmeHttp01ChallengeResponseResult Handled(GeneratedRouteResponse response)
    {
        return new HandledResult(response);
    }

    public sealed record NoMatchResult : AcmeHttp01ChallengeResponseResult;

    public sealed record HandledResult : AcmeHttp01ChallengeResponseResult
    {
        public HandledResult(GeneratedRouteResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            Response = response;
        }

        public GeneratedRouteResponse Response { get; }
    }
}
