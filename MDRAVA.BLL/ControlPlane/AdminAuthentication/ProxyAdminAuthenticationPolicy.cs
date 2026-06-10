using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.AdminAuthentication;

public sealed record ProxyAdminAuthenticationInput(
    bool RequireAuthentication,
    string? ExpectedToken,
    ProxyAdminPresentedCredentials PresentedCredentials);

public sealed record ProxyAdminPresentedCredentials
{
    public ProxyAdminPresentedCredentials(
        IReadOnlyList<string> authorizationHeaders,
        IReadOnlyList<string> apiKeyHeaders)
    {
        AuthorizationHeaders = CopyValidated(authorizationHeaders, nameof(authorizationHeaders));
        ApiKeyHeaders = CopyValidated(apiKeyHeaders, nameof(apiKeyHeaders));
    }

    public IReadOnlyList<string> AuthorizationHeaders { get; }

    public IReadOnlyList<string> ApiKeyHeaders { get; }

    public static ProxyAdminPresentedCredentials FromRawHeaders(
        IEnumerable<string?> authorizationHeaders,
        IEnumerable<string?> apiKeyHeaders)
    {
        return new ProxyAdminPresentedCredentials(
            CopyNonNull(authorizationHeaders, nameof(authorizationHeaders)),
            CopyNonNull(apiKeyHeaders, nameof(apiKeyHeaders)));
    }

    private static IReadOnlyList<string> CopyValidated(
        IReadOnlyList<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);

        var copy = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (value is null)
            {
                throw new ArgumentException("Header values cannot contain null entries.", parameterName);
            }

            copy.Add(value);
        }

        return copy;
    }

    private static IReadOnlyList<string> CopyNonNull(
        IEnumerable<string?> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values);

        var copy = new List<string>();
        foreach (var value in values)
        {
            if (value is not null)
            {
                copy.Add(value);
            }
        }

        return copy;
    }
}

public sealed record ProxyAdminAuthenticationDecision(
    bool Allowed,
    bool AuthenticationRequired,
    string Result,
    bool ShouldChallenge);

public static class ProxyAdminAuthenticationPolicy
{
    public const string AdminApiKeyHeaderName = "X-MDRAVA-Admin-Key";
    public const string NotRequiredResult = "not-required";
    public const string NotConfiguredResult = "not-configured";
    public const string MissingResult = "missing";
    public const string InvalidResult = "invalid";
    public const string ValidResult = "valid";

    public static ProxyAdminAuthenticationDecision Authenticate(ProxyAdminAuthenticationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (!input.RequireAuthentication)
        {
            return new ProxyAdminAuthenticationDecision(
                Allowed: true,
                AuthenticationRequired: false,
                NotRequiredResult,
                ShouldChallenge: false);
        }

        if (string.IsNullOrEmpty(input.ExpectedToken))
        {
            return Deny(NotConfiguredResult, shouldChallenge: false);
        }

        var presentedToken = ReadBearerToken(input.PresentedCredentials.AuthorizationHeaders)
            ?? ReadApiKey(input.PresentedCredentials.ApiKeyHeaders);

        if (presentedToken is null)
        {
            return Deny(MissingResult, shouldChallenge: true);
        }

        if (!FixedTimeEquals(input.ExpectedToken, presentedToken))
        {
            return Deny(InvalidResult, shouldChallenge: false);
        }

        return new ProxyAdminAuthenticationDecision(
            Allowed: true,
            AuthenticationRequired: true,
            ValidResult,
            ShouldChallenge: false);
    }

    private static ProxyAdminAuthenticationDecision Deny(string result, bool shouldChallenge)
    {
        return new ProxyAdminAuthenticationDecision(
            Allowed: false,
            AuthenticationRequired: true,
            result,
            shouldChallenge);
    }

    private static string? ReadBearerToken(IReadOnlyList<string> authorizationValues)
    {
        foreach (var value in authorizationValues)
        {
            const string prefix = "Bearer ";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var token = value[prefix.Length..].Trim();
                return token.Length == 0 ? null : token;
            }
        }

        return null;
    }

    private static string? ReadApiKey(IReadOnlyList<string> values)
    {
        var value = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static bool FixedTimeEquals(string expected, [NotNullWhen(true)] string? actual)
    {
        if (actual is null)
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var actualBytes = Encoding.UTF8.GetBytes(actual);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }
}
