using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.UpdateTemplate;

/// <summary>
/// Handles updating a prompt template's metadata.
/// </summary>
public sealed class UpdateTemplateHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateTemplateHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTemplateHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public UpdateTemplateHandler(AppDbContext db, ILogger<UpdateTemplateHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Updates the metadata of an existing prompt template.
    /// </summary>
    /// <param name="command">The update template command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the updated template DTO on success.</returns>
    public async Task<Result<PromptTemplateDto>> HandleAsync(UpdateTemplateCommand command, CancellationToken ct)
    {
        PromptTemplate? template = await _db.Set<PromptTemplate>()
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct);

        if (template is null)
        {
            return Error.NotFound($"Prompt template '{command.TemplateId}' was not found.");
        }

        template.Name = command.Name;
        template.Category = command.Category;
        template.Description = command.Description;
        template.Tags = command.Tags ?? [];
        template.ProjectId = command.ProjectId;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Updated template {TemplateId}", template.Id);

        return PromptTemplateDto.FromEntity(template);
    }
}
