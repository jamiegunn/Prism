namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// HTTP request body for updating a prompt template's metadata.
/// </summary>
/// <param name="Name">The updated template name.</param>
/// <param name="Category">The updated category.</param>
/// <param name="Description">The updated description.</param>
/// <param name="Tags">The updated tags.</param>
/// <param name="ProjectId">The updated project association.</param>
public sealed record UpdateTemplateRequest(
    string Name,
    string? Category = null,
    string? Description = null,
    List<string>? Tags = null,
    Guid? ProjectId = null);
