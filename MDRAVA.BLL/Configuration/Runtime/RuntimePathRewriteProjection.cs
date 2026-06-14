namespace MDRAVA.BLL.Configuration;

public sealed record RuntimePathRewriteProjection(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement);
