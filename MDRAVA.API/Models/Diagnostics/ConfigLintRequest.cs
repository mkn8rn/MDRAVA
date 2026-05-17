namespace MDRAVA.API.Models.Diagnostics;

public sealed record ConfigLintRequest(
    string Format,
    string Text);
