namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeStaticResponse(
    int StatusCode,
    string ContentType,
    string Body);
