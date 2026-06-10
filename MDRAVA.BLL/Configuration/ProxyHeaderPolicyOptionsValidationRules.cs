namespace MDRAVA.BLL.Configuration;

internal static class ProxyHeaderPolicyOptionsValidationRules
{
    private static readonly HashSet<string> RestrictedHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection",
        "Keep-Alive",
        "Proxy-Authenticate",
        "Proxy-Authorization",
        "TE",
        "Trailer",
        "Transfer-Encoding",
        "Upgrade",
        "Content-Length",
        "Host",
        "X-Request-Id"
    };

    public static void Validate(
        List<string> failures,
        string routePrefix,
        ProxyHeaderPolicyOptions policy)
    {
        ValidateHeaderSetRules(failures, $"{routePrefix}:HeaderPolicy:SetRequestHeaders", policy.SetRequestHeaders);
        ValidateHeaderSetRules(failures, $"{routePrefix}:HeaderPolicy:SetResponseHeaders", policy.SetResponseHeaders);
        ValidateHeaderRemoveRules(failures, $"{routePrefix}:HeaderPolicy:RemoveRequestHeaders", policy.RemoveRequestHeaders);
        ValidateHeaderRemoveRules(failures, $"{routePrefix}:HeaderPolicy:RemoveResponseHeaders", policy.RemoveResponseHeaders);
    }

    public static bool IsValidHttpFieldName(string headerName)
    {
        foreach (var character in headerName)
        {
            var valid = character is >= '!' and <= '~'
                && character is not '(' and not ')' and not '<' and not '>' and not '@'
                && character is not ',' and not ';' and not ':' and not '\\' and not '"'
                && character is not '/' and not '[' and not ']' and not '?' and not '='
                && character is not '{' and not '}';
            if (!valid)
            {
                return false;
            }
        }

        return true;
    }

    private static void ValidateHeaderSetRules(
        List<string> failures,
        string prefix,
        IReadOnlyList<ProxyHeaderSetOptions> rules)
    {
        for (var index = 0; index < rules.Count; index++)
        {
            var rule = rules[index];
            var rulePrefix = $"{prefix}:{index}";
            ValidateHeaderName(failures, $"{rulePrefix}:Name", rule.Name);
            if (rule.Value.Any(static character => character is '\r' or '\n'))
            {
                failures.Add($"{rulePrefix}:Value must not contain CR or LF.");
            }
        }
    }

    private static void ValidateHeaderRemoveRules(
        List<string> failures,
        string prefix,
        IReadOnlyList<string> headerNames)
    {
        for (var index = 0; index < headerNames.Count; index++)
        {
            ValidateHeaderName(failures, $"{prefix}:{index}", headerNames[index]);
        }
    }

    private static void ValidateHeaderName(List<string> failures, string prefix, string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName))
        {
            failures.Add($"{prefix} is required.");
            return;
        }

        if (RestrictedHeaderNames.Contains(headerName))
        {
            failures.Add($"{prefix} '{headerName}' is restricted and cannot be modified by header policy.");
            return;
        }

        if (!IsValidHttpFieldName(headerName))
        {
            failures.Add($"{prefix} '{headerName}' is not a valid HTTP field name.");
        }
    }
}
