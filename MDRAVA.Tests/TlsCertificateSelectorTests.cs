using System.Security.Cryptography.X509Certificates;

namespace MDRAVA.Tests;

internal static class TlsCertificateSelectorTests
{
    public static void SelectsSniCertificateBeforeDefaultAndFallsBackSafely()
    {
        using var defaultCertificate = Certificate("default.test");
        using var homeCertificate = Certificate("home.test");
        var certificates = new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase)
        {
            ["default"] = RuntimeCertificate("default", defaultCertificate),
            ["home"] = RuntimeCertificate("home", homeCertificate)
        };
        var bindings = new[]
        {
            new RuntimeSniCertificateBinding("Home.Test", "home"),
            new RuntimeSniCertificateBinding("missing.test", "missing")
        };

        var selectedSni = TlsCertificateSelector.SelectCertificate(new TlsCertificateSelectionInput(
            certificates,
            "default",
            bindings,
            "home.test"));
        var selectedFallback = TlsCertificateSelector.SelectCertificate(new TlsCertificateSelectionInput(
            certificates,
            "default",
            bindings,
            "unknown.test"));
        var selectedMissingSniFallback = TlsCertificateSelector.SelectCertificate(new TlsCertificateSelectionInput(
            certificates,
            "default",
            bindings,
            "missing.test"));
        var selectedNone = TlsCertificateSelector.SelectCertificate(new TlsCertificateSelectionInput(
            certificates,
            "missing",
            bindings,
            "absent.test"));

        AssertEx.Equal(homeCertificate.Thumbprint, AssertEx.NotNull(selectedSni).Thumbprint);
        AssertEx.Equal(defaultCertificate.Thumbprint, AssertEx.NotNull(selectedFallback).Thumbprint);
        AssertEx.Equal(defaultCertificate.Thumbprint, AssertEx.NotNull(selectedMissingSniFallback).Thumbprint);
        AssertEx.True(selectedNone is null);
    }

    private static X509Certificate2 Certificate(string subjectName)
    {
        return X509CertificateLoader.LoadPkcs12(
            TestCertificates.CreateSelfSignedPfxBytes(subjectName),
            null);
    }

    private static RuntimeCertificate RuntimeCertificate(string id, X509Certificate2 certificate)
    {
        return new RuntimeCertificate(
            id,
            $"{id}.pfx",
            "pfx",
            false,
            certificate);
    }
}
