namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationDiscovery
{
    public ProxyConfigurationDiscovery(
        ProxyFilesystemLayout layout,
        IReadOnlyList<ProxyConfigurationFileDiscovery> files,
        IReadOnlyList<string> createdPaths,
        IReadOnlyList<string> existingPaths)
    {
        ArgumentNullException.ThrowIfNull(layout);

        Layout = layout;
        Files = RuntimeList.Copy(files);
        CreatedPaths = RuntimeList.Copy(createdPaths);
        ExistingPaths = RuntimeList.Copy(existingPaths);
    }

    public ProxyFilesystemLayout Layout { get; init; }

    public IReadOnlyList<ProxyConfigurationFileDiscovery> Files { get; init; }

    public IReadOnlyList<string> CreatedPaths { get; init; }

    public IReadOnlyList<string> ExistingPaths { get; init; }
}
