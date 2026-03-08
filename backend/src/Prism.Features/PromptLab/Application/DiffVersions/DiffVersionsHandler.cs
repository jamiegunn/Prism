using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.DiffVersions;

/// <summary>
/// Handles retrieval of two versions of a template for comparison.
/// </summary>
public sealed class DiffVersionsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiffVersionsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public DiffVersionsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves two versions of a template for side-by-side comparison.
    /// </summary>
    /// <param name="query">The query containing the template ID and two version numbers.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing both version DTOs for diffing.</returns>
    public async Task<Result<VersionDiffDto>> HandleAsync(DiffVersionsQuery query, CancellationToken ct)
    {
        List<PromptVersion> versions = await _db.Set<PromptVersion>()
            .AsNoTracking()
            .Where(v => v.TemplateId == query.TemplateId
                && (v.Version == query.Version1 || v.Version == query.Version2))
            .ToListAsync(ct);

        PromptVersion? v1 = versions.FirstOrDefault(v => v.Version == query.Version1);
        PromptVersion? v2 = versions.FirstOrDefault(v => v.Version == query.Version2);

        if (v1 is null)
        {
            return Error.NotFound($"Version {query.Version1} of template '{query.TemplateId}' was not found.");
        }

        if (v2 is null)
        {
            return Error.NotFound($"Version {query.Version2} of template '{query.TemplateId}' was not found.");
        }

        return new VersionDiffDto(
            PromptVersionDto.FromEntity(v1),
            PromptVersionDto.FromEntity(v2));
    }
}
