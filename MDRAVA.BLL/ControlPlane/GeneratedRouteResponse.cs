using MDRAVA.BLL.ControlPlane.Http1;
namespace MDRAVA.BLL.ControlPlane;

public sealed record GeneratedRouteResponse(
    int StatusCode,
    string ReasonPhrase,
    string? ContentType,
    string Body,
    IReadOnlyList<Http1HeaderField> Headers);
