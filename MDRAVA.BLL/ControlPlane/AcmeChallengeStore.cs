namespace MDRAVA.BLL.ControlPlane;

public sealed class AcmeChallengeStore
{
    public const int MaximumTokenLength = 256;
    public const int MaximumResponseBodyLength = 4096;

    private readonly object _gate = new();
    private readonly Dictionary<string, AcmeChallengeRegistration> _challenges = new(StringComparer.Ordinal);

    public bool TryRegister(string token, string responseBody, DateTimeOffset expiresAtUtc)
    {
        if (!IsValidToken(token) || string.IsNullOrEmpty(responseBody) || responseBody.Length > MaximumResponseBodyLength)
        {
            return false;
        }

        lock (_gate)
        {
            _challenges[token] = new AcmeChallengeRegistration(token, responseBody, expiresAtUtc);
            return true;
        }
    }

    public bool TryGetResponse(string token, DateTimeOffset nowUtc, out string responseBody)
    {
        lock (_gate)
        {
            if (_challenges.TryGetValue(token, out var challenge))
            {
                if (challenge.ExpiresAtUtc > nowUtc)
                {
                    responseBody = challenge.ResponseBody;
                    return true;
                }

                _challenges.Remove(token);
            }
        }

        responseBody = "";
        return false;
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
