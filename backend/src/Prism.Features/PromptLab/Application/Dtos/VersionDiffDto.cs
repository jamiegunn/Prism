namespace Prism.Features.PromptLab.Application.Dtos;

/// <summary>
/// Data transfer object for a diff between two prompt versions.
/// </summary>
/// <param name="Version1">The first version content.</param>
/// <param name="Version2">The second version content.</param>
public sealed record VersionDiffDto(
    PromptVersionDto Version1,
    PromptVersionDto Version2);
