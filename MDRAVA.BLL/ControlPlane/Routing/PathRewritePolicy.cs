using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed class PathRewritePolicy
{
    public string Apply(RuntimeRoute route, string target, string path)
    {
        var rewrite = route.PathRewrite;
        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix)
            && path.StartsWith(rewrite.StripPrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, rewrite.StripPrefix, "");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix)
            && path.StartsWith(rewrite.ReplacePrefix, StringComparison.Ordinal))
        {
            return RewriteTarget(target, rewrite.ReplacePrefix, rewrite.Replacement);
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
