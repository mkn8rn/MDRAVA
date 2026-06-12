namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintSourceNameResolver
{
    public static string ActiveSource(
        ProxyConfigLintConfigurationSnapshot snapshot,
        IProxyConfigLintSourceNameFormatter sourceNameFormatter)
    {
        return snapshot.SourceFiles.Count == 1
            ? SourceName(snapshot.SourceFiles[0], sourceNameFormatter) ?? "active-config"
            : "active-config";
    }

    public static string? SourceName(
        string? path,
        IProxyConfigLintSourceNameFormatter sourceNameFormatter)
    {
        return sourceNameFormatter.FormatSourceName(path);
    }
}
