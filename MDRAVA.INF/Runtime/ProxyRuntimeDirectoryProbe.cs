using MDRAVA.BLL.ControlPlane.RuntimePreflight;

namespace MDRAVA.INF.Runtime;

public sealed class ProxyRuntimeDirectoryProbe : IProxyRuntimeDirectoryProbe
{
    public ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing)
    {
        var created = false;
        try
        {
            if (!Directory.Exists(path))
            {
                if (!createIfMissing)
                {
                    return new ProxyRuntimeDirectoryProbeResult(false, false, false, false, "missing");
                }

                Directory.CreateDirectory(path);
                created = true;
            }

            var canRead = CanRead(path);
            var canWrite = CanWrite(path);
            return new ProxyRuntimeDirectoryProbeResult(true, created, canRead, canWrite, canWrite ? null : "not_writable");
        }
        catch (UnauthorizedAccessException)
        {
            return new ProxyRuntimeDirectoryProbeResult(Directory.Exists(path), created, false, false, "access_denied");
        }
        catch (IOException)
        {
            return new ProxyRuntimeDirectoryProbeResult(Directory.Exists(path), created, false, false, "io_error");
        }
    }

    private static bool CanRead(string path)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Take(1).ToArray();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool CanWrite(string path)
    {
        var fileName = Path.Combine(path, $".mdrava-preflight-{Guid.NewGuid():N}.tmp");
        try
        {
            using (File.Create(fileName, 1, FileOptions.DeleteOnClose))
            {
            }

            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            return true;
        }
        catch
        {
            try
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
            catch
            {
            }

            return false;
        }
    }
}
