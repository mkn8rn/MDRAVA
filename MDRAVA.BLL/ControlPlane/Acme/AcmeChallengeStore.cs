namespace MDRAVA.BLL.ControlPlane.Acme;

public sealed class AcmeChallengeStore
{
    public const int MaximumTokenLength = 256;
    public const int MaximumResponseBodyLength = 4096;

    private readonly object _gate = new();
    private readonly Dictionary<string, AcmeChallengeRegistration> _challenges = new(StringComparer.Ordinal);

    public AcmeChallengeRegistrationResult Register(
        string token,
        string responseBody,
        DateTimeOffset expiresAtUtc)
    {
        if (!IsValidToken(token))
        {
            return AcmeChallengeRegistrationResult.Rejected("invalid-token");
        }

        if (string.IsNullOrEmpty(responseBody) || responseBody.Length > MaximumResponseBodyLength)
        {
            return AcmeChallengeRegistrationResult.Rejected("invalid-response-body");
        }

        lock (_gate)
        {
            _challenges[token] = new AcmeChallengeRegistration(token, responseBody, expiresAtUtc);
            return AcmeChallengeRegistrationResult.Registered;
        }
    }

    public AcmeChallengeResponseLookupResult FindResponse(string token, DateTimeOffset nowUtc)
    {
        lock (_gate)
        {
            if (_challenges.TryGetValue(token, out var challenge))
            {
                if (challenge.ExpiresAtUtc > nowUtc)
                {
                    return AcmeChallengeResponseLookupResult.Found(challenge.ResponseBody);
                }

                _challenges.Remove(token);
            }
        }

        return AcmeChallengeResponseLookupResult.Missing;
    }

    public bool Remove(string token)
    {
        lock (_gate)
        {
            return _challenges.Remove(token);
        }
    }

    public static bool IsValidToken(string token)
    {
        if (token.Length is 0 or > MaximumTokenLength)
        {
            return false;
        }

        foreach (var character in token)
        {
            if (!char.IsAsciiLetterOrDigit(character)
                && character is not '-' and not '_')
            {
                return false;
            }
        }

        return true;
    }
}

public abstract record AcmeChallengeRegistrationResult
{
    private AcmeChallengeRegistrationResult()
    {
    }

    public static AcmeChallengeRegistrationResult Registered { get; } = new RegisteredResult();

    public static AcmeChallengeRegistrationResult Rejected(string reason)
    {
        return new RejectedResult(reason);
    }

    private sealed record RegisteredResult : AcmeChallengeRegistrationResult;

    public sealed record RejectedResult : AcmeChallengeRegistrationResult
    {
        public RejectedResult(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("ACME challenge registration rejection reason is required.", nameof(reason));
            }

            Reason = reason;
        }

        public string Reason { get; }
    }
}

public abstract record AcmeChallengeResponseLookupResult
{
    private AcmeChallengeResponseLookupResult()
    {
    }

    public static AcmeChallengeResponseLookupResult Missing { get; } = new MissingResult();

    public static AcmeChallengeResponseLookupResult Found(string responseBody)
    {
        return new FoundResult(responseBody);
    }

    public sealed record FoundResult : AcmeChallengeResponseLookupResult
    {
        public FoundResult(string responseBody)
        {
            if (string.IsNullOrEmpty(responseBody))
            {
                throw new ArgumentException("ACME challenge response body is required.", nameof(responseBody));
            }

            ResponseBody = responseBody;
        }

        public string ResponseBody { get; }
    }

    private sealed record MissingResult : AcmeChallengeResponseLookupResult;
}
