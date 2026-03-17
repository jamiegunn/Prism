using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.ForkTemplate;

/// <summary>
/// Forks an existing prompt template version into a new template.
/// Copies the version content (system prompt, user template, variables, few-shot examples)
/// as version 1 of the new template.
/// </summary>
public sealed class ForkTemplateHandler
{
    private readonly AppDbContext _db;
    private readonly ILogger<ForkTemplateHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForkTemplateHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger.</param>
    public ForkTemplateHandler(AppDbContext db, ILogger<ForkTemplateHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Forks the specified version of a template into a new template.
    /// </summary>
    /// <param name="command">The fork command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the new template with version DTO.</returns>
    public async Task<Result<PromptTemplateWithVersionDto>> HandleAsync(ForkTemplateCommand command, CancellationToken ct)
    {
        PromptVersion? sourceVersion = await _db.Set<PromptVersion>()
            .AsNoTracking()
            .Include(v => v.Template)
            .FirstOrDefaultAsync(v => v.TemplateId == command.SourceTemplateId && v.Version == command.SourceVersion, ct);

        if (sourceVersion is null)
        {
            return Result<PromptTemplateWithVersionDto>.Failure(
                Error.NotFound($"Version {command.SourceVersion} of template {command.SourceTemplateId} not found."));
        }

        PromptTemplate sourceTemplate = sourceVersion.Template!;
        Guid templateId = Guid.NewGuid();
        Guid versionId = Guid.NewGuid();

        var newTemplate = new PromptTemplate
        {
            Id = templateId,
            Name = command.NewName ?? $"{sourceTemplate.Name} (fork)",
            Category = sourceTemplate.Category,
            Description = command.NewDescription ?? sourceTemplate.Description,
            Tags = [..sourceTemplate.Tags, "forked"],
            ProjectId = command.ProjectId ?? sourceTemplate.ProjectId,
            LatestVersion = 1
        };

        var newVersion = new PromptVersion
        {
            Id = versionId,
            TemplateId = templateId,
            Version = 1,
            SystemPrompt = sourceVersion.SystemPrompt,
            UserTemplate = sourceVersion.UserTemplate,
            Variables = sourceVersion.Variables.Select(v => new PromptVariable
            {
                Name = v.Name,
                Type = v.Type,
                DefaultValue = v.DefaultValue,
                Description = v.Description,
                Required = v.Required
            }).ToList(),
            FewShotExamples = sourceVersion.FewShotExamples.Select(e => new FewShotExample
            {
                Input = e.Input,
                Output = e.Output,
                Label = e.Label
            }).ToList(),
            Notes = $"Forked from {sourceTemplate.Name} v{sourceVersion.Version}"
        };

        _db.Set<PromptTemplate>().Add(newTemplate);
        _db.Set<PromptVersion>().Add(newVersion);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Forked template {SourceTemplate} v{Version} into new template {NewTemplate}",
            command.SourceTemplateId, command.SourceVersion, templateId);

        return new PromptTemplateWithVersionDto(
            PromptTemplateDto.FromEntity(newTemplate),
            PromptVersionDto.FromEntity(newVersion));
    }
}

/// <summary>
/// Command for forking a template version into a new template.
/// </summary>
/// <param name="SourceTemplateId">The source template ID to fork from.</param>
/// <param name="SourceVersion">The version number to fork.</param>
/// <param name="NewName">Optional name for the new template.</param>
/// <param name="NewDescription">Optional description for the new template.</param>
/// <param name="ProjectId">Optional project to assign the fork to.</param>
public sealed record ForkTemplateCommand(
    Guid SourceTemplateId,
    int SourceVersion,
    string? NewName = null,
    string? NewDescription = null,
    Guid? ProjectId = null);
