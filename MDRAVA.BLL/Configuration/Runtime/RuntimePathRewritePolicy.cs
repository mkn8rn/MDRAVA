namespace MDRAVA.BLL.Configuration;

public sealed record RuntimePathRewritePolicy(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement);
