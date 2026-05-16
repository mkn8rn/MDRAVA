namespace MDRAVA.API.Proxy.Configuration.Paths;

public sealed class MdravaDataDirectoryOptions
{
    public const string SectionName = "Mdrava";

    public string? DataDirectory { get; init; }
}
