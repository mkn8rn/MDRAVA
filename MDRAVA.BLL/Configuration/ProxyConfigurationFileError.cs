namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationFileError
{
    private ProxyConfigurationFileError(string? path, string message)
    {
        Path = path;
        Message = message;
    }

    public string? Path { get; }

    public string Message { get; }

    public static ProxyConfigurationFileError Global(string message)
    {
        return new ProxyConfigurationFileError(
            path: null,
            message: message);
    }

    public static ProxyConfigurationFileError ForPath(string path, string message)
    {
        return new ProxyConfigurationFileError(
            path: path,
            message: message);
    }
}
