namespace MDRAVA.BLL.ControlPlane;

public static class ProxyGeneratedFailurePolicy
{
    public static bool CanWriteFailureResponse(
        bool responseStarted,
        bool suppressGeneratedFailureResponse)
    {
        return !responseStarted && !suppressGeneratedFailureResponse;
    }
}
