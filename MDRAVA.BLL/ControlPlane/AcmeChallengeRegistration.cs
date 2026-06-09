namespace MDRAVA.BLL.ControlPlane;

public sealed record AcmeChallengeRegistration(
    string Token,
    string ResponseBody,
    DateTimeOffset ExpiresAtUtc);
