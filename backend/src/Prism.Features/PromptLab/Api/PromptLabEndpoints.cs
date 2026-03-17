using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Prism.Common.Results;
using Prism.Features.PromptLab.Api.Requests;
using Prism.Features.PromptLab.Application.CreateTemplate;
using Prism.Features.PromptLab.Application.CreateVersion;
using Prism.Features.PromptLab.Application.DeleteTemplate;
using Prism.Features.PromptLab.Application.DiffVersions;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Application.GetTemplate;
using Prism.Features.PromptLab.Application.GetVersion;
using Prism.Features.PromptLab.Application.ListTemplates;
using Prism.Features.PromptLab.Application.ListVersions;
using Prism.Features.PromptLab.Application.AbTest;
using Prism.Features.PromptLab.Application.TestPrompt;
using Prism.Features.PromptLab.Application.UpdateTemplate;
using Prism.Features.PromptLab.Application.ForkTemplate;

namespace Prism.Features.PromptLab.Api;

/// <summary>
/// Defines the HTTP endpoints for managing prompt templates and versions.
/// </summary>
public static class PromptLabEndpoints
{
    /// <summary>
    /// Maps the prompt lab endpoints under <c>/api/v1/prompts</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapPromptLabEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/prompts")
            .WithTags("Prompts");

        // Template CRUD
        group.MapPost("/", CreateTemplate)
            .WithName("CreateTemplate")
            .WithSummary("Creates a new prompt template with an initial version")
            .Produces<PromptTemplateWithVersionDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/", ListTemplates)
            .WithName("ListTemplates")
            .WithSummary("Lists prompt templates with optional category, search, and project filters")
            .Produces<List<PromptTemplateDto>>();

        group.MapGet("/{id:guid}", GetTemplate)
            .WithName("GetTemplate")
            .WithSummary("Gets a prompt template by ID with its latest version")
            .Produces<PromptTemplateWithVersionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{id:guid}", UpdateTemplate)
            .WithName("UpdateTemplate")
            .WithSummary("Updates a prompt template's metadata")
            .Produces<PromptTemplateDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteTemplate)
            .WithName("DeleteTemplate")
            .WithSummary("Deletes a prompt template and all its versions")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Version management
        group.MapPost("/{id:guid}/versions", CreateVersion)
            .WithName("CreateVersion")
            .WithSummary("Creates a new version of a prompt template")
            .Produces<PromptVersionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/versions", ListVersions)
            .WithName("ListVersions")
            .WithSummary("Lists all versions of a prompt template")
            .Produces<List<PromptVersionDto>>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/versions/{version:int}", GetVersion)
            .WithName("GetVersion")
            .WithSummary("Gets a specific version of a prompt template")
            .Produces<PromptVersionDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/diff", DiffVersions)
            .WithName("DiffVersions")
            .WithSummary("Gets two versions of a template for comparison")
            .Produces<VersionDiffDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Test execution
        group.MapPost("/{id:guid}/test", TestPrompt)
            .WithName("TestPrompt")
            .WithSummary("Tests a prompt template by rendering and executing it")
            .Produces<TestPromptResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // A/B testing
        group.MapPost("/ab-test", StartAbTest)
            .WithName("StartAbTest")
            .WithSummary("Starts an A/B test across prompt variations, instances, and parameter sets")
            .Produces<AbTestResultDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Fork
        group.MapPost("/{id:guid}/fork", ForkTemplate)
            .WithName("ForkTemplate")
            .WithSummary("Forks a template version into a new template")
            .Produces<PromptTemplateWithVersionDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CreateTemplateHandler handler,
        CancellationToken ct)
    {
        var command = new CreateTemplateCommand(
            request.ProjectId,
            request.Name,
            request.Category,
            request.Description,
            request.Tags,
            request.SystemPrompt,
            request.UserTemplate,
            request.Variables,
            request.FewShotExamples);

        Result<PromptTemplateWithVersionDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/prompts/{dto.Template.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListTemplates(
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] Guid? projectId,
        ListTemplatesHandler handler,
        CancellationToken ct)
    {
        var query = new ListTemplatesQuery(category, search, projectId);
        Result<List<PromptTemplateDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetTemplate(
        Guid id,
        GetTemplateHandler handler,
        CancellationToken ct)
    {
        var query = new GetTemplateQuery(id);
        Result<PromptTemplateWithVersionDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> UpdateTemplate(
        Guid id,
        [FromBody] UpdateTemplateRequest request,
        UpdateTemplateHandler handler,
        CancellationToken ct)
    {
        var command = new UpdateTemplateCommand(
            id, request.Name, request.Category, request.Description, request.Tags, request.ProjectId);
        Result<PromptTemplateDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteTemplate(
        Guid id,
        DeleteTemplateHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteTemplateCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> CreateVersion(
        Guid id,
        [FromBody] CreateVersionRequest request,
        CreateVersionHandler handler,
        CancellationToken ct)
    {
        var command = new CreateVersionCommand(
            id, request.SystemPrompt, request.UserTemplate, request.Variables, request.FewShotExamples, request.Notes);
        Result<PromptVersionDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/prompts/{id}/versions/{dto.Version}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListVersions(
        Guid id,
        ListVersionsHandler handler,
        CancellationToken ct)
    {
        var query = new ListVersionsQuery(id);
        Result<List<PromptVersionDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetVersion(
        Guid id,
        int version,
        GetVersionHandler handler,
        CancellationToken ct)
    {
        var query = new GetVersionQuery(id, version);
        Result<PromptVersionDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DiffVersions(
        Guid id,
        [FromQuery] int v1,
        [FromQuery] int v2,
        DiffVersionsHandler handler,
        CancellationToken ct)
    {
        var query = new DiffVersionsQuery(id, v1, v2);
        Result<VersionDiffDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> TestPrompt(
        Guid id,
        [FromBody] TestPromptRequest request,
        TestPromptHandler handler,
        CancellationToken ct)
    {
        var command = new TestPromptCommand(
            id,
            request.Version,
            request.Variables,
            request.InstanceId,
            request.Temperature,
            request.TopP,
            request.TopK,
            request.MaxTokens,
            request.Logprobs,
            request.TopLogprobs,
            request.SaveAsRunExperimentId,
            request.RunName);

        Result<TestPromptResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> StartAbTest(
        [FromBody] AbTestRequest request,
        AbTestHandler handler,
        CancellationToken ct)
    {
        var command = new AbTestCommand(
            request.ProjectId,
            request.ExperimentName,
            request.Variations,
            request.InstanceIds,
            request.ParameterSets,
            request.RunsPerCombo);

        Result<AbTestResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/experiments/{dto.ExperimentId}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ForkTemplate(
        Guid id,
        [FromBody] ForkTemplateRequest request,
        ForkTemplateHandler handler,
        CancellationToken ct)
    {
        var command = new ForkTemplateCommand(
            id,
            request.SourceVersion,
            request.NewName,
            request.NewDescription,
            request.ProjectId);

        Result<PromptTemplateWithVersionDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/prompts/{dto.Template.Id}", dto),
            error => error.ToHttpResult());
    }
}
