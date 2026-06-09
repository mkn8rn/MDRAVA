namespace MDRAVA.Tests;

internal static class FramedUpstreamResponsePolicyTests
{
    public static void BuildsHttp1HeadFromNarrowUpstreamFacts()
    {
        var endedWithHead = Build("GET", new FramedUpstreamResponseTranslationInput(
            200,
            [new Http1HeaderField("content-length", "12")],
            ResponseEndedWithHead: true));
        AssertEx.Equal(Http1BodyKind.None, endedWithHead.Framing.Kind);

        var headMethod = Build("HEAD", new FramedUpstreamResponseTranslationInput(
            200,
            [new Http1HeaderField("content-length", "12")],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.None, headMethod.Framing.Kind);

        var noContentStatus = Build("GET", new FramedUpstreamResponseTranslationInput(
            204,
            [new Http1HeaderField("content-length", "12")],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.None, noContentStatus.Framing.Kind);

        var contentLength = Build("GET", new FramedUpstreamResponseTranslationInput(
            200,
            [new Http1HeaderField("content-length", "12")],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.ContentLength, contentLength.Framing.Kind);
        AssertEx.Equal(12L, contentLength.Framing.ContentLength);
        AssertEx.Equal("OK", contentLength.ReasonPhrase);

        var zeroContentLength = Build("GET", new FramedUpstreamResponseTranslationInput(
            200,
            [new Http1HeaderField("content-length", "0")],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.None, zeroContentLength.Framing.Kind);

        var missingContentLength = Build("GET", new FramedUpstreamResponseTranslationInput(
            200,
            [],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.Chunked, missingContentLength.Framing.Kind);

        var invalidContentLength = Build("GET", new FramedUpstreamResponseTranslationInput(
            200,
            [new Http1HeaderField("content-length", "-1")],
            ResponseEndedWithHead: false));
        AssertEx.Equal(Http1BodyKind.Chunked, invalidContentLength.Framing.Kind);
    }

    private static Http1ResponseHead Build(
        string method,
        FramedUpstreamResponseTranslationInput input)
    {
        return FramedUpstreamResponsePolicy.BuildHttp1ResponseHead(
            CreateRequestHead(method),
            input);
    }

    private static Http1RequestHead CreateRequestHead(string method)
    {
        return new Http1RequestHead(
            method,
            "/resource",
            "/resource",
            "HTTP/1.1",
            "example.test",
            Http1RequestFraming.None,
            []);
    }
}
