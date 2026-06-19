using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintSubmittedRequestInput
{
    public ConfigLintSubmittedRequestInput(
        string text,
        ProxyConfigurationNormalizeFormat format)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!Enum.IsDefined(format))
        {
            throw new ArgumentOutOfRangeException(nameof(format), format, "Submitted config format must be a defined format.");
        }

        Text = text;
        Format = format;
    }

    public string Text { get; }

    public ProxyConfigurationNormalizeFormat Format { get; }
}

public static class ConfigLintSubmittedRequestReader
{
    public static ConfigLintSubmittedRequestDecision Read(ConfigLintRequest? request)
    {
        if (request is null)
        {
            return ConfigLintSubmittedRequestDecision.Rejected(
                ConfigLintFindingFactory.Error(
                    "missing_request",
                    "A lint request body is required.",
                    SiteConfigurationSource.LintInputPath,
                    null,
                    "Submit config text with an explicit format."));
        }

        var formatDecision = ParseFormat(request.Format);
        if (formatDecision is ConfigLintSubmittedFormatDecision.RejectedDecision)
        {
            return ConfigLintSubmittedRequestDecision.Rejected(
                ConfigLintFindingFactory.Error(
                    "invalid_format",
                    "Format must be 'json' or 'yaml'.",
                    SiteConfigurationSource.LintInputPath,
                    "format",
                    "Set format to 'json', 'yaml', or 'yml'."));
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return ConfigLintSubmittedRequestDecision.Rejected(
                ConfigLintFindingFactory.Error(
                    "empty_config",
                    "Submitted config text is required.",
                    SiteConfigurationSource.LintInputPath,
                    "text",
                    "Submit one site configuration object."));
        }

        var format = ((ConfigLintSubmittedFormatDecision.AcceptedDecision)formatDecision).Format;
        return ConfigLintSubmittedRequestDecision.Accepted(new ConfigLintSubmittedRequestInput(request.Text, format));
    }

    private static ConfigLintSubmittedFormatDecision ParseFormat(string? format)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigLintSubmittedFormatDecision.Accepted(ProxyConfigurationNormalizeFormat.Json);
        }

        if (string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "yml", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigLintSubmittedFormatDecision.Accepted(ProxyConfigurationNormalizeFormat.Yaml);
        }

        return ConfigLintSubmittedFormatDecision.Rejected;
    }

    private abstract record ConfigLintSubmittedFormatDecision
    {
        private ConfigLintSubmittedFormatDecision()
        {
        }

        public static ConfigLintSubmittedFormatDecision Rejected { get; } = new RejectedDecision();

        public static ConfigLintSubmittedFormatDecision Accepted(ProxyConfigurationNormalizeFormat format)
        {
            return new AcceptedDecision(format);
        }

        public sealed record AcceptedDecision : ConfigLintSubmittedFormatDecision
        {
            public AcceptedDecision(ProxyConfigurationNormalizeFormat format)
            {
                if (!Enum.IsDefined(format))
                {
                    throw new ArgumentOutOfRangeException(nameof(format), format, "Submitted config format must be a defined format.");
                }

                Format = format;
            }

            public ProxyConfigurationNormalizeFormat Format { get; }
        }

        public sealed record RejectedDecision : ConfigLintSubmittedFormatDecision;
    }
}

public abstract record ConfigLintSubmittedRequestDecision
{
    private ConfigLintSubmittedRequestDecision()
    {
    }

    public static ConfigLintSubmittedRequestDecision Accepted(ConfigLintSubmittedRequestInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new AcceptedDecision(input);
    }

    public static ConfigLintSubmittedRequestDecision Rejected(ConfigLintFinding failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new RejectedDecision(failure);
    }

    public sealed record AcceptedDecision : ConfigLintSubmittedRequestDecision
    {
        public AcceptedDecision(ConfigLintSubmittedRequestInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            Input = input;
        }

        public ConfigLintSubmittedRequestInput Input { get; }
    }

    public sealed record RejectedDecision : ConfigLintSubmittedRequestDecision
    {
        public RejectedDecision(ConfigLintFinding failure)
        {
            ArgumentNullException.ThrowIfNull(failure);

            Failure = failure;
        }

        public ConfigLintFinding Failure { get; }
    }
}
