namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupDirectoryRequirement
{
    public ProxyBackupDirectoryRequirement(
        string RelativePath,
        string Classification,
        bool Sensitive)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(RelativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(Classification);

        this.RelativePath = RelativePath;
        this.Classification = Classification;
        this.Sensitive = Sensitive;
    }

    public string RelativePath { get; }

    public string Classification { get; }

    public bool Sensitive { get; }
}

public static class ProxyBackupDirectoryLayoutPolicy
{
    public static IReadOnlyList<ProxyBackupDirectoryRequirement> ExpectedDirectories()
    {
        return
        [
            new ProxyBackupDirectoryRequirement(
                "config",
                ProxyBackupFileClassificationPolicy.MustBackup,
                false),
            new ProxyBackupDirectoryRequirement(
                "config/sites",
                ProxyBackupFileClassificationPolicy.MustBackup,
                false),
            new ProxyBackupDirectoryRequirement(
                "logs",
                ProxyBackupFileClassificationPolicy.ShouldBackup,
                false),
            new ProxyBackupDirectoryRequirement(
                "certs",
                ProxyBackupFileClassificationPolicy.NeverExportByDefaultSensitive,
                true),
            new ProxyBackupDirectoryRequirement(
                "certs/acme",
                ProxyBackupFileClassificationPolicy.NeverExportByDefaultSensitive,
                true),
            new ProxyBackupDirectoryRequirement(
                "state",
                ProxyBackupFileClassificationPolicy.ShouldBackup,
                false)
        ];
    }
}
