using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.Tests;

internal static class TestCertificates
{
    public static void WriteSelfSignedPfx(
        string path,
        string subjectName,
        string? password = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(
            $"CN={subjectName}",
            key,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                new OidCollection
                {
                    new Oid("1.3.6.1.5.5.7.3.1")
                },
                false));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false));

        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(subjectName);
        request.CertificateExtensions.Add(sanBuilder.Build());

        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));

        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }
}
