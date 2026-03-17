namespace Prism.Features.PromptLab.Api.Requests;

/// <summary>
/// Request body for forking a template version into a new template.
/// </summary>
/// <param name="SourceVersion">The version number to fork from.</param>
/// <param name="NewName">Optional name for the forked template.</param>
/// <param name="NewDescription">Optional description for the forked template.</param>
/// <param name="ProjectId">Optional project ID to assign the fork to.</param>
public sealed record ForkTemplateRequest(
    int SourceVersion,
    string? NewName = null,
    string? NewDescription = null,
    Guid? ProjectId = null);
