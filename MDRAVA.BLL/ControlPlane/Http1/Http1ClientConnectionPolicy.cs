namespace MDRAVA.BLL.ControlPlane.Http1;

public static class Http1ClientConnectionPolicy
{
    public static bool ShouldKeepOpen(Http1RequestHead requestHead)
    {
        if (HopByHopHeaderPolicy.HasConnectionToken(requestHead.Headers, "close"))
        {
            return false;
        }

        return string.Equals(requestHead.Version, "HTTP/1.1", StringComparison.OrdinalIgnoreCase);
    }
}
