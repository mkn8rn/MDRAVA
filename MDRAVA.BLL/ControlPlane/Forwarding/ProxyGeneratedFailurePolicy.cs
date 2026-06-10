namespace MDRAVA.BLL.ControlPlane.Forwarding;

public static class ProxyGeneratedFailurePolicy
{
    public static bool CanWriteFailureResponse(
        bool responseStarted,
        bool suppressGeneratedFailureResponse)
    {
        return !responseStarted && !suppressGeneratedFailureResponse;
    }
}
