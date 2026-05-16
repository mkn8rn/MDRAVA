namespace MDRAVA.API.Models.Acme;

public sealed record AcmeChallengeRegistration(
    string Token,
    string ResponseBody,
    DateTimeOffset ExpiresAtUtc);
