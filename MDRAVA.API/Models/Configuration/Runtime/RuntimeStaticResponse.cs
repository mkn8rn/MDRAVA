namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeStaticResponse(
    int StatusCode,
    string ContentType,
    string Body);
