using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.DeleteTemplate;

/// <summary>
/// Handles deletion of a prompt template and all its versions.
/// </summary>
public sealed class DeleteTemplateHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeleteTemplateHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteTemplateHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public DeleteTemplateHandler(AppDbContext db, ILogger<DeleteTemplateHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Deletes a prompt template and all associated versions (via cascade).
    /// </summary>
    /// <param name="command">The delete template command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    public async Task<Result> HandleAsync(DeleteTemplateCommand command, CancellationToken ct)
    {
        PromptTemplate? template = await _db.Set<PromptTemplate>()
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct);

        if (template is null)
        {
            return Error.NotFound($"Prompt template '{command.TemplateId}' was not found.");
        }

        _db.Set<PromptTemplate>().Remove(template);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted template {TemplateId}", template.Id);

        return Result.Success();
    }
}
