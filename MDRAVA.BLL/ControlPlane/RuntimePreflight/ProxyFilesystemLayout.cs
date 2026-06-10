namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public sealed record ProxyFilesystemLayout(
    string DataDirectory,
    string ConfigDirectory,
    string SitesDirectory,
    string LogsDirectory,
    string CertificatesDirectory,
    string StateDirectory,
    string ProxyConfigPath);
