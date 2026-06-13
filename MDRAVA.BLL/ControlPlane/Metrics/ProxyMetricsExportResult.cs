namespace MDRAVA.BLL.ControlPlane.Metrics;

public abstract record ProxyMetricsExportResult
{
    private ProxyMetricsExportResult()
    {
    }

    public static ProxyMetricsExportResult Unavailable { get; } = new UnavailableResult();

    public static ProxyMetricsExportResult Exported(string content, string contentType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        return new ExportedResult(content, contentType);
    }

    public sealed record ExportedResult : ProxyMetricsExportResult
    {
        public ExportedResult(string content, string contentType)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

            Content = content;
            ContentType = contentType;
        }

        public string Content { get; }

        public string ContentType { get; }
    }

    public sealed record UnavailableResult : ProxyMetricsExportResult;
}
