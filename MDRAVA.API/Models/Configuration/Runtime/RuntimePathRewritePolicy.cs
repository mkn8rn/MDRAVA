namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimePathRewritePolicy(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement);
