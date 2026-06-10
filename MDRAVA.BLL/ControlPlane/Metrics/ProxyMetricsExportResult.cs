namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportResult(bool Available, string Content, string ContentType)
{
    public static ProxyMetricsExportResult NotAvailable { get; } = new(false, string.Empty, string.Empty);

    public static ProxyMetricsExportResult Create(string content, string contentType)
    {
        return new ProxyMetricsExportResult(true, content, contentType);
    }
}
