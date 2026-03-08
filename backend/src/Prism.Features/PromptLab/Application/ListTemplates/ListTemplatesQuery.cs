namespace Prism.Features.PromptLab.Application.ListTemplates;

/// <summary>
/// Query to list prompt templates with optional filtering.
/// </summary>
/// <param name="Category">Optional category to filter by.</param>
/// <param name="Search">Optional search term to filter by name.</param>
/// <param name="ProjectId">Optional project ID to filter by.</param>
public sealed record ListTemplatesQuery(string? Category = null, string? Search = null, Guid? ProjectId = null);
