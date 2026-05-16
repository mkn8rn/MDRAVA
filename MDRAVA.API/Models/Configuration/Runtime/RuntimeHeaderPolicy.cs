namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeHeaderPolicy(
    IReadOnlyList<Http1HeaderField> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<Http1HeaderField> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicy Empty { get; } = new([], [], [], []);
}
