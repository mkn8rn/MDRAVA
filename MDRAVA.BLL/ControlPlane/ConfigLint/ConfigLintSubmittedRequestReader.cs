using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintSubmittedRequestInput(
    string Text,
    ProxyConfigurationNormalizeFormat Format);

public static class ConfigLintSubmittedRequestReader
{
    public static bool TryRead(
        ConfigLintRequest? request,
        out ConfigLintSubmittedRequestInput? input,
        out ConfigLintFinding? failure)
    {
        input = null;
        if (request is null)
        {
            failure = Error("missing_request", "A lint request body is required.", "lint-input", null, "Submit config text with an explicit format.");
            return false;
        }

        if (!TryParseFormat(request.Format, out var format))
        {
            failure = Error("invalid_format", "Format must be 'json' or 'yaml'.", "lint-input", "format", "Set format to 'json', 'yaml', or 'yml'.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            failure = Error("empty_config", "Submitted config text is required.", "lint-input", "text", "Submit one site configuration object.");
            return false;
        }

        input = new ConfigLintSubmittedRequestInput(request.Text, format);
        failure = null;
        return true;
    }

    private static bool TryParseFormat(
        string? format,
        out ProxyConfigurationNormalizeFormat parsed)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Json;
            return true;
        }

        if (string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "yml", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Yaml;
            return true;
        }

        parsed = ProxyConfigurationNormalizeFormat.Json;
        return false;
    }

    private static ConfigLintFinding Error(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("error", code, message, source, path, suggestedFix);
    }
}
