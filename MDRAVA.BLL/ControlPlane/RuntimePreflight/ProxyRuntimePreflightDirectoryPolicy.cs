using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public enum ProxyRuntimePreflightDirectoryKind
{
    Data,
    Config,
    Sites,
    Logs,
    Certificates,
    State
}

public sealed record ProxyRuntimePreflightDirectoryRequirement
{
    public ProxyRuntimePreflightDirectoryRequirement(
        ProxyRuntimePreflightDirectoryKind Kind,
        string Name,
        string RelativePath,
        bool Critical)
    {
        ProxyStatusFacts.RequireText(Name, nameof(Name));
        ProxyStatusFacts.RequireText(RelativePath, nameof(RelativePath));

        this.Kind = Kind;
        this.Name = Name;
        this.RelativePath = RelativePath;
        this.Critical = Critical;
    }

    public ProxyRuntimePreflightDirectoryKind Kind { get; }

    public string Name { get; }

    public string RelativePath { get; }

    public bool Critical { get; }
}

public static class ProxyRuntimePreflightDirectoryPolicy
{
    public static IReadOnlyList<ProxyRuntimePreflightDirectoryRequirement> ExpectedDirectories()
    {
        return
        [
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.Data,
                "data_directory",
                ".",
                true),
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.Config,
                "config_directory",
                "config",
                true),
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.Sites,
                "sites_directory",
                "config/sites",
                true),
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.Logs,
                "logs_directory",
                "logs",
                false),
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.Certificates,
                "certificates_directory",
                "certs",
                false),
            new ProxyRuntimePreflightDirectoryRequirement(
                ProxyRuntimePreflightDirectoryKind.State,
                "state_directory",
                "state",
                false)
        ];
    }
}
