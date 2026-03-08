namespace Prism.Features.Experiments.Application.ListProjects;

/// <summary>
/// Query to list research projects with optional filtering.
/// </summary>
/// <param name="IncludeArchived">Whether to include archived projects.</param>
/// <param name="Search">Optional search term to filter by name.</param>
public sealed record ListProjectsQuery(bool IncludeArchived = false, string? Search = null);
