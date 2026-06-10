namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed record AcmeChallengeRegistration(
    string Token,
    string ResponseBody,
    DateTimeOffset ExpiresAtUtc);
