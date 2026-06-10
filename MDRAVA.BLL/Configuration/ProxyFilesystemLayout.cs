namespace MDRAVA.BLL.Configuration;

public sealed record ProxyFilesystemLayout(
    string DataDirectory,
    string ConfigDirectory,
    string SitesDirectory,
    string LogsDirectory,
    string CertificatesDirectory,
    string StateDirectory,
    string ProxyConfigPath);
