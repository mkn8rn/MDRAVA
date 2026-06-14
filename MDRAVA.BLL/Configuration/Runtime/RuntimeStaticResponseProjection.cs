namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeStaticResponseProjection(
    int StatusCode,
    string ContentType,
    string Body);
