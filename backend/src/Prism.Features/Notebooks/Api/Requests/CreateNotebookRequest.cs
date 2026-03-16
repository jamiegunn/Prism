namespace Prism.Features.Notebooks.Api.Requests;

/// <summary>
/// Request to create a new notebook.
/// </summary>
public sealed record CreateNotebookRequest(
    string Name,
    string? Description,
    string? Content);
