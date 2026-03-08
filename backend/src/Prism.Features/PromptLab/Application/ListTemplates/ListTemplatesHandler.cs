using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.ListTemplates;

/// <summary>
/// Handles listing of prompt templates with optional filtering.
/// </summary>
public sealed class ListTemplatesHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="ListTemplatesHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public ListTemplatesHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Returns a list of prompt templates, optionally filtered by category, search, or project.
    /// </summary>
    /// <param name="query">The query containing filter parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the list of template DTOs.</returns>
    public async Task<Result<List<PromptTemplateDto>>> HandleAsync(ListTemplatesQuery query, CancellationToken ct)
    {
        IQueryable<PromptTemplate> queryable = _db.Set<PromptTemplate>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            queryable = queryable.Where(t => t.Category == query.Category);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string search = query.Search.ToLowerInvariant();
            queryable = queryable.Where(t => t.Name.ToLower().Contains(search));
        }

        if (query.ProjectId.HasValue)
        {
            queryable = queryable.Where(t => t.ProjectId == query.ProjectId.Value);
        }

        List<PromptTemplate> templates = await queryable
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(ct);

        List<PromptTemplateDto> dtos = templates
            .Select(PromptTemplateDto.FromEntity)
            .ToList();

        return dtos;
    }
}
