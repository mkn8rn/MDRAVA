namespace MDRAVA.Tests;

internal static class ProxyGeneratedFailurePolicyTests
{
    public static void AllowsGeneratedFailureOnlyBeforeResponseAndWithoutSuppression()
    {
        AssertEx.True(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: false,
            suppressGeneratedFailureResponse: false));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: true,
            suppressGeneratedFailureResponse: false));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: false,
            suppressGeneratedFailureResponse: true));
        AssertEx.False(ProxyGeneratedFailurePolicy.CanWriteFailureResponse(
            responseStarted: true,
            suppressGeneratedFailureResponse: true));
    }
}
