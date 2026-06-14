namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed class PathRewritePolicy
{
    public string Apply(PathRewritePolicyInput input, string target, string path)
    {
        if (!string.IsNullOrWhiteSpace(input.StripPrefix)
            && path.StartsWith(input.StripPrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, input.StripPrefix, "");
        }

        if (!string.IsNullOrWhiteSpace(input.ReplacePrefix)
            && path.StartsWith(input.ReplacePrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, input.ReplacePrefix, input.Replacement);
        }

        return target;
    }

    private static string RewriteTarget(string target, string oldPrefix, string newPrefix)
    {
        var queryIndex = target.IndexOf('?');
        var path = queryIndex < 0 ? target : target[..queryIndex];
        var query = queryIndex < 0 ? "" : target[queryIndex..];
        var remainder = path[oldPrefix.Length..];
        var rewrittenPath = string.IsNullOrEmpty(newPrefix) ? remainder : newPrefix + remainder;
        if (string.IsNullOrEmpty(rewrittenPath))
        {
            rewrittenPath = "/";
        }

        if (!rewrittenPath.StartsWith('/'))
        {
            rewrittenPath = "/" + rewrittenPath;
        }

        return rewrittenPath + query;
    }
}

public sealed record PathRewritePolicyInput
{
    public PathRewritePolicyInput(
        string? StripPrefix,
        string? ReplacePrefix,
        string? Replacement)
    {
        this.StripPrefix = StripPrefix ?? "";
        this.ReplacePrefix = ReplacePrefix ?? "";
        this.Replacement = Replacement ?? "";
    }

    public string StripPrefix { get; }

    public string ReplacePrefix { get; }

    public string Replacement { get; }
}
