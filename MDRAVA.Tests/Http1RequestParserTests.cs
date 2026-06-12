using System.Text;

namespace MDRAVA.Tests;

internal static class Http1RequestParserTests
{
    public static void ParsesValidGet()
    {
        var bytes = Bytes("GET /health?verbose=true HTTP/1.1\r\nHost: example.test:8080\r\nUser-Agent: test\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out var requestHead, out var error);

        AssertEx.True(parsed);
        AssertEx.Equal(Http1ParseError.None, error);

        var head = AssertEx.NotNull(requestHead);
        AssertEx.Equal("GET", head.Method);
        AssertEx.Equal("/health?verbose=true", head.Target);
        AssertEx.Equal("/health", head.Path);
        AssertEx.Equal("HTTP/1.1", head.Version);
        AssertEx.Equal("example.test:8080", head.Host);
        AssertEx.Equal(null, head.ContentLength);
        AssertEx.False(head.HasTransferEncoding);
    }

    public static void RejectsMissingHost()
    {
        var bytes = Bytes("GET / HTTP/1.1\r\nUser-Agent: test\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out var requestHead, out var error);

        AssertEx.False(parsed);
        AssertEx.Equal(Http1ParseError.MissingHost, error);
        AssertEx.Equal(null, requestHead);
    }

    public static void RejectsInvalidContentLength()
    {
        var bytes = Bytes("GET / HTTP/1.1\r\nHost: example.test\r\nContent-Length: nope\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out _, out var error);

        AssertEx.False(parsed);
        AssertEx.Equal(Http1ParseError.InvalidContentLength, error);
    }

    public static void DetectsRequestBodyIndicators()
    {
        var bytes = Bytes("GET / HTTP/1.1\r\nHost: example.test\r\nContent-Length: 12\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out var requestHead, out var error);

        AssertEx.True(parsed);
        AssertEx.Equal(Http1ParseError.None, error);

        var head = AssertEx.NotNull(requestHead);
        AssertEx.Equal(12L, head.ContentLength);
        AssertEx.False(head.HasTransferEncoding);
        AssertEx.Equal(Http1BodyKind.ContentLength, head.Framing.Kind);
    }

    public static void ParsesChunkedTransferEncoding()
    {
        var bytes = Bytes("POST / HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: chunked\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out var requestHead, out var error);

        AssertEx.True(parsed);
        AssertEx.Equal(Http1ParseError.None, error);
        AssertEx.Equal(Http1BodyKind.Chunked, AssertEx.NotNull(requestHead).Framing.Kind);
    }

    public static void RejectsConflictingContentLength()
    {
        var bytes = Bytes("POST / HTTP/1.1\r\nHost: example.test\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out _, out var error);

        AssertEx.False(parsed);
        AssertEx.Equal(Http1ParseError.ConflictingContentLength, error);
    }

    public static void RejectsContentLengthWithTransferEncoding()
    {
        var bytes = Bytes("POST / HTTP/1.1\r\nHost: example.test\r\nContent-Length: 1\r\nTransfer-Encoding: chunked\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out _, out var error);

        AssertEx.False(parsed);
        AssertEx.Equal(Http1ParseError.ContentLengthWithTransferEncoding, error);
    }

    public static void RejectsUnsupportedTransferEncoding()
    {
        var bytes = Bytes("POST / HTTP/1.1\r\nHost: example.test\r\nTransfer-Encoding: gzip\r\n\r\n");

        var parsed = Http1RequestParser.TryParse(bytes, out _, out var error);

        AssertEx.False(parsed);
        AssertEx.Equal(Http1ParseError.UnsupportedTransferEncoding, error);
    }

    public static void ContentLengthAnalysisNamesAcceptedLength()
    {
        var result = Http1RequestParser.AnalyzeContentLength(["12", "12"]);

        AssertEx.True(result is Http1ContentLengthAnalysisResult.Accepted);
        AssertEx.Equal(12L, ((Http1ContentLengthAnalysisResult.Accepted)result).ContentLength);
    }

    public static void ContentLengthAnalysisNamesRejectedLength()
    {
        var invalid = Http1RequestParser.AnalyzeContentLength(["nope"]);
        var conflicting = Http1RequestParser.AnalyzeContentLength(["1", "2"]);

        AssertEx.True(invalid is Http1ContentLengthAnalysisResult.Rejected);
        AssertEx.Equal(Http1ParseError.InvalidContentLength, ((Http1ContentLengthAnalysisResult.Rejected)invalid).Error);
        AssertEx.True(conflicting is Http1ContentLengthAnalysisResult.Rejected);
        AssertEx.Equal(Http1ParseError.ConflictingContentLength, ((Http1ContentLengthAnalysisResult.Rejected)conflicting).Error);
    }

    private static byte[] Bytes(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }
}
