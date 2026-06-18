namespace MDRAVA.BLL.Configuration;

internal static class RuntimeSniCertificateFacts
{
    public static void Validate(string hostName, string certificateId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentException.ThrowIfNullOrWhiteSpace(certificateId);
    }
}
