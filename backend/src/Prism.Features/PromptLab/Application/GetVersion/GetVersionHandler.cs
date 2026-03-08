using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.GetVersion;

/// <summary>
/// Handles retrieval of a specific version of a prompt template.
/// </summary>
public sealed class GetVersionHandler
{
    private readonly AppDbContext _db;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetVersionHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    public GetVersionHandler(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Retrieves a specific version of a template by template ID and version number.
    /// </summary>
    /// <param name="query">The query containing the template ID and version number.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the version DTO on success.</returns>
    public async Task<Result<PromptVersionDto>> HandleAsync(GetVersionQuery query, CancellationToken ct)
    {
        PromptVersion? version = await _db.Set<PromptVersion>()
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.TemplateId == query.TemplateId && v.Version == query.Version, ct);

        if (version is null)
        {
            return Error.NotFound($"Version {query.Version} of template '{query.TemplateId}' was not found.");
        }

        return PromptVersionDto.FromEntity(version);
    }
}
