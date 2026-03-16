namespace Prism.Features.Notebooks.Api.Requests;

/// <summary>
/// Request to update a notebook.
/// </summary>
public sealed record UpdateNotebookRequest(
    string? Name,
    string? Description,
    string? Content);
