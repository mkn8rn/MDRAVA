namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeAcmeCertificateOptions
{
    public RuntimeAcmeCertificateOptions(
        string Id,
        bool Enabled,
        IReadOnlyList<string> Domains,
        int RenewBeforeDays)
    {
        this.Id = Id;
        this.Enabled = Enabled;
        this.Domains = RuntimeList.Copy(Domains);
        this.RenewBeforeDays = RenewBeforeDays;
    }

    public string Id { get; init; }

    public bool Enabled { get; init; }

    public IReadOnlyList<string> Domains { get; }

    public int RenewBeforeDays { get; init; }
}
