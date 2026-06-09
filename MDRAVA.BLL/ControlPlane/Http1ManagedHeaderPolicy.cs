namespace MDRAVA.BLL.ControlPlane;

public static class Http1ManagedHeaderPolicy
{
    public static bool IsManagedFramingHeader(string headerName)
    {
        return string.Equals(headerName, "Content-Length", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "Connection", StringComparison.OrdinalIgnoreCase)
            || string.Equals(headerName, "X-Request-Id", StringComparison.OrdinalIgnoreCase);
    }
}
