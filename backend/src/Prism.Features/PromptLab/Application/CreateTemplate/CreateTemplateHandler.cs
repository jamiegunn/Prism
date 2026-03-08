using FluentValidation;
using FluentValidation.Results;
using Prism.Common.Results;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.CreateTemplate;

/// <summary>
/// Handles creation of a new prompt template with an initial version.
/// </summary>
public sealed class CreateTemplateHandler
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateTemplateCommand> _validator;
    private readonly ILogger<CreateTemplateHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTemplateHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="validator">The command validator.</param>
    /// <param name="logger">The logger instance.</param>
    public CreateTemplateHandler(
        AppDbContext db,
        IValidator<CreateTemplateCommand> validator,
        ILogger<CreateTemplateHandler> logger)
    {
        _db = db;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new prompt template with version 1 and persists it.
    /// </summary>
    /// <param name="command">The create template command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the created template with its initial version.</returns>
    public async Task<Result<PromptTemplateWithVersionDto>> HandleAsync(CreateTemplateCommand command, CancellationToken ct)
    {
        ValidationResult validation = await _validator.ValidateAsync(command, ct);
        if (!validation.IsValid)
        {
            return Error.Validation(string.Join(" ", validation.Errors.Select(e => e.ErrorMessage)));
        }

        var template = new PromptTemplate
        {
            ProjectId = command.ProjectId,
            Name = command.Name,
            Category = command.Category,
            Description = command.Description,
            Tags = command.Tags ?? [],
            LatestVersion = 1
        };

        var version = new PromptVersion
        {
            TemplateId = template.Id,
            Version = 1,
            SystemPrompt = command.SystemPrompt,
            UserTemplate = command.UserTemplate,
            Variables = command.Variables ?? [],
            FewShotExamples = command.FewShotExamples ?? []
        };

        _db.Set<PromptTemplate>().Add(template);
        _db.Set<PromptVersion>().Add(version);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created template {TemplateId} with name {TemplateName}", template.Id, template.Name);

        return new PromptTemplateWithVersionDto(
            PromptTemplateDto.FromEntity(template),
            PromptVersionDto.FromEntity(version));
    }
}
