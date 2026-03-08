using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.ListVersions;

/// <summary>
/// Handles listing all versions of a prompt template.
/// </summary>
public sealed class ListVersionsHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListVersionsHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListVersionsHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns all versions for a template, ordered by version number descending.
    /// </summary>
    /// <param name="query">The query containing the template ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of version DTOs.</returns>
    public async Task<Result<List<PromptVersionDto>>> HandleAsync(ListVersionsQuery query, CancellationToken ct)
    {
        bool templateExists = await _db.Set<PromptTemplate>()
            .AnyAsync(t => t.Id == query.TemplateId, ct);

        if (!templateExists)
        {
            return Error.NotFound($"Prompt template '{query.TemplateId}' was not found.");
        }

        List<PromptVersion> versions = await _db.Set<PromptVersion>()
            .AsNoTracking()
            .Where(v => v.TemplateId == query.TemplateId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

        return versions.Select(PromptVersionDto.FromEntity).ToList();
    }
}
