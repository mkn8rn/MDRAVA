namespace MDRAVA.Tests;

internal static class ProxyAdminAuthenticationPolicyTests
{
    public static void ClassifiesAdminAuthenticationAttempts()
    {
        var notRequired = Authenticate(
            requireAuthentication: false,
            expectedToken: null,
            authorizationHeaders: [],
            apiKeyHeaders: []);
        AssertEx.True(notRequired.Allowed);
        AssertEx.False(notRequired.AuthenticationRequired);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.NotRequiredResult, notRequired.Result);

        var notConfigured = Authenticate(
            requireAuthentication: true,
            expectedToken: null,
            authorizationHeaders: [],
            apiKeyHeaders: []);
        AssertEx.False(notConfigured.Allowed);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.NotConfiguredResult, notConfigured.Result);
        AssertEx.False(notConfigured.ShouldChallenge);

        var missing = Authenticate(
            requireAuthentication: true,
            expectedToken: "secret",
            authorizationHeaders: [],
            apiKeyHeaders: []);
        AssertEx.False(missing.Allowed);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.MissingResult, missing.Result);
        AssertEx.True(missing.ShouldChallenge);

        var invalid = Authenticate(
            requireAuthentication: true,
            expectedToken: "secret",
            authorizationHeaders: ["Bearer wrong"],
            apiKeyHeaders: []);
        AssertEx.False(invalid.Allowed);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.InvalidResult, invalid.Result);

        var validBearer = Authenticate(
            requireAuthentication: true,
            expectedToken: "secret",
            authorizationHeaders: ["bearer  secret  "],
            apiKeyHeaders: []);
        AssertEx.True(validBearer.Allowed);
        AssertEx.True(validBearer.AuthenticationRequired);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.ValidResult, validBearer.Result);

        var validApiKey = Authenticate(
            requireAuthentication: true,
            expectedToken: "secret",
            authorizationHeaders: [],
            apiKeyHeaders: [" secret "]);
        AssertEx.True(validApiKey.Allowed);

        var bearerWinsOverApiKey = Authenticate(
            requireAuthentication: true,
            expectedToken: "secret",
            authorizationHeaders: ["Bearer wrong"],
            apiKeyHeaders: ["secret"]);
        AssertEx.False(bearerWinsOverApiKey.Allowed);
        AssertEx.Equal(ProxyAdminAuthenticationPolicy.InvalidResult, bearerWinsOverApiKey.Result);
    }

    private static ProxyAdminAuthenticationDecision Authenticate(
        bool requireAuthentication,
        string? expectedToken,
        IReadOnlyList<string?> authorizationHeaders,
        IReadOnlyList<string?> apiKeyHeaders)
    {
        return ProxyAdminAuthenticationPolicy.Authenticate(
            new ProxyAdminAuthenticationInput(
                requireAuthentication,
                expectedToken,
                authorizationHeaders,
                apiKeyHeaders));
    }
}
