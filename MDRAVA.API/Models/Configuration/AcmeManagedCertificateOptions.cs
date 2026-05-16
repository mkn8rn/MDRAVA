namespace MDRAVA.API.Models.Configuration;

public sealed class AcmeManagedCertificateOptions
{
    public string Id { get; init; } = "";

    public bool Enabled { get; init; } = true;

    public List<string> Domains { get; init; } = [];

    public int? RenewBeforeDays { get; init; }
}
