namespace MDRAVA.BLL.Configuration;

internal static class RuntimeListenerFacts
{
    public static void Validate(
        int port,
        int backlog,
        int maxRequestHeadBytes,
        int maxResponseHeadBytes,
        int maxChunkLineBytes,
        int forwardingBufferBytes)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        if (backlog < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(backlog));
        }

        if (maxRequestHeadBytes is < 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRequestHeadBytes));
        }

        if (maxResponseHeadBytes is < 1024 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResponseHeadBytes));
        }

        if (maxChunkLineBytes is < 64 or > 16 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(maxChunkLineBytes));
        }

        if (forwardingBufferBytes is < 4096 or > 1024 * 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(forwardingBufferBytes));
        }
    }
}
