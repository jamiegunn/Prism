using Microsoft.EntityFrameworkCore;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Application.Rendering;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.CreateVersion;

/// <summary>
/// Handles creation of a new version for a prompt template.
/// </summary>
public sealed class CreateVersionHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<CreateVersionHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateVersionHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateVersionHandler(AppDbContext db, ILogger<CreateVersionHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new version for the specified template, auto-incrementing the version number.
    /// </summary>
    /// <param name="command">The create version command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created version DTO on success.</returns>
    public async Task<Result<PromptVersionDto>> HandleAsync(CreateVersionCommand command, CancellationToken ct)
    {
        PromptTemplate? template = await _db.Set<PromptTemplate>()
            .FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct);

        if (template is null)
        {
            return Error.NotFound($"Prompt template '{command.TemplateId}' was not found.");
        }

        // Caught here rather than at render time. A version referencing {{code}} without
        // declaring it used to be accepted with a 201 and then refused by every attempt to run
        // it, so the author learned about the mistake somewhere other than where they made it —
        // and the version stayed in the history, permanently untestable.
        List<string> undeclared = TemplateRenderer.FindUndeclared(
            command.UserTemplate, (command.Variables ?? []).Select(v => v.Name));

        if (undeclared.Count > 0)
        {
            return Error.Validation(
                $"The template uses variables it does not declare: {string.Join(", ", undeclared)}. " +
                "Declare them, or remove the placeholders.");
        }

        int nextVersion = template.LatestVersion + 1;

        var version = new PromptVersion
        {
            TemplateId = template.Id,
            Version = nextVersion,
            SystemPrompt = command.SystemPrompt,
            UserTemplate = command.UserTemplate,
            Variables = command.Variables ?? [],
            FewShotExamples = command.FewShotExamples ?? [],
            Notes = command.Notes
        };

        template.LatestVersion = nextVersion;

        _db.Set<PromptVersion>().Add(version);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created version {Version} for template {TemplateId}",
            nextVersion, template.Id);

        return PromptVersionDto.FromEntity(version);
    }
}
