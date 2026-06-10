using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class AcmeHttp01ChallengeResponder
{
    public const string ChallengePathPrefix = "/.well-known/acme-challenge/";

    private readonly AcmeChallengeStore _challengeStore;
    private readonly TimeProvider _timeProvider;

    public AcmeHttp01ChallengeResponder(
        AcmeChallengeStore challengeStore,
        TimeProvider timeProvider)
    {
        _challengeStore = challengeStore;
        _timeProvider = timeProvider;
    }

    public bool TryCreateResponse(Http1RequestHead requestHead, out GeneratedRouteResponse response)
    {
        if (!requestHead.Path.StartsWith(ChallengePathPrefix, StringComparison.Ordinal))
        {
            response = null!;
            return false;
        }

        var token = requestHead.Path[ChallengePathPrefix.Length..];
        if (!string.Equals(requestHead.Method, "GET", StringComparison.Ordinal)
            || !AcmeChallengeStore.IsValidToken(token))
        {
            response = NotFound();
            return true;
        }

        if (!_challengeStore.TryGetResponse(token, _timeProvider.GetUtcNow(), out var body))
        {
            response = NotFound();
            return true;
        }

        response = new GeneratedRouteResponse(
            200,
            "OK",
            "text/plain",
            body,
            []);
        return true;
    }

    private static GeneratedRouteResponse NotFound()
    {
        return new GeneratedRouteResponse(
            404,
            "Not Found",
            "text/plain",
            "Not Found",
            []);
    }
}
