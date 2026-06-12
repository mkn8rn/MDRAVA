namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportResult
{
    public static ProxyMetricsExportResult NotAvailable { get; } = new(
        available: false,
        content: string.Empty,
        contentType: string.Empty);

    private ProxyMetricsExportResult(bool available, string content, string contentType)
    {
        Available = available;
        Content = content;
        ContentType = contentType;
    }

    public bool Available { get; }

    public string Content { get; }

    public string ContentType { get; }

    public static ProxyMetricsExportResult Create(string content, string contentType)
    {
        return new ProxyMetricsExportResult(
            available: true,
            content: content,
            contentType: contentType);
    }
}
