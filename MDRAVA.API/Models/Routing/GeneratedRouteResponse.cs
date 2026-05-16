namespace MDRAVA.API.Models.Routing;

public sealed record GeneratedRouteResponse(
    int StatusCode,
    string ReasonPhrase,
    string? ContentType,
    string Body,
    IReadOnlyList<Http1HeaderField> Headers);
