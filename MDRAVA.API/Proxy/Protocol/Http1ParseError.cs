namespace MDRAVA.API.Proxy.Protocol;

public enum Http1ParseError
{
    None,
    EmptyRequest,
    InvalidRequestLine,
    UnsupportedVersion,
    InvalidTarget,
    MissingHost,
    InvalidHeaderLine,
    InvalidContentLength,
    ConflictingContentLength,
    InvalidTransferEncoding,
    ContentLengthWithTransferEncoding,
    UnsupportedTransferEncoding,
    InvalidStatusLine
}
