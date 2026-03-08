using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.GetTemplate;

/// <summary>
/// Handles retrieval of a specific prompt template with its latest version.
/// </summary>
public sealed class GetTemplateHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetTemplateHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetTemplateHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves a template by ID, including its latest version content.
    /// </summary>
    /// <param name="query">The query containing the template ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the template with its latest version.</returns>
    public async Task<Result<PromptTemplateWithVersionDto>> HandleAsync(GetTemplateQuery query, CancellationToken ct)
    {
        PromptTemplate? template = await _db.Set<PromptTemplate>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == query.TemplateId, ct);

        if (template is null)
        {
            return Error.NotFound($"Prompt template '{query.TemplateId}' was not found.");
        }

        PromptVersion? latestVersion = await _db.Set<PromptVersion>()
            .AsNoTracking()
            .Where(v => v.TemplateId == template.Id)
            .OrderByDescending(v => v.Version)
            .FirstOrDefaultAsync(ct);

        return new PromptTemplateWithVersionDto(
            PromptTemplateDto.FromEntity(template),
            latestVersion is not null ? PromptVersionDto.FromEntity(latestVersion) : null);
    }
}
