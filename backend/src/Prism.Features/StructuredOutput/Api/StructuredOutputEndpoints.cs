using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.StructuredOutput.Api.Requests;
using Prism.Features.StructuredOutput.Application.CreateSchema;
using Prism.Features.StructuredOutput.Application.DeleteSchema;
using Prism.Features.StructuredOutput.Application.Dtos;
using Prism.Features.StructuredOutput.Application.GetSchema;
using Prism.Features.StructuredOutput.Application.ListSchemas;
using Prism.Features.StructuredOutput.Application.StructuredInference;

namespace Prism.Features.StructuredOutput.Api;

/// <summary>
/// Defines the HTTP endpoints for the Structured Output toolkit.
/// </summary>
public static class StructuredOutputEndpoints
{
    /// <summary>
    /// Maps the structured output endpoints under <c>/api/v1/structured-output</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapStructuredOutputEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/structured-output")
            .WithTags("Structured Output");

        // Schema CRUD
        group.MapPost("/schemas", CreateSchema)
            .WithName("CreateJsonSchema")
            .WithSummary("Creates a new JSON schema for guided decoding")
            .Produces<JsonSchemaDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem();

        group.MapGet("/schemas", ListSchemas)
            .WithName("ListJsonSchemas")
            .WithSummary("Lists all JSON schemas")
            .Produces<List<JsonSchemaDto>>();

        group.MapGet("/schemas/{id:guid}", GetSchema)
            .WithName("GetJsonSchema")
            .WithSummary("Gets a specific JSON schema")
            .Produces<JsonSchemaDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/schemas/{id:guid}", DeleteSchema)
            .WithName("DeleteJsonSchema")
            .WithSummary("Deletes a JSON schema")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Structured inference
        group.MapPost("/schemas/{id:guid}/infer", ExecuteStructuredInference)
            .WithName("StructuredInference")
            .WithSummary("Executes inference with guided JSON decoding and validates the result")
            .Produces<StructuredInferenceResultDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateSchema(
        [FromBody] CreateSchemaRequest request,
        CreateSchemaHandler handler,
        CancellationToken ct)
    {
        var command = new CreateSchemaCommand(
            request.Name, request.Description, request.SchemaJson, request.ProjectId);

        Result<JsonSchemaDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Created($"/api/v1/structured-output/schemas/{dto.Id}", dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListSchemas(
        [FromQuery] Guid? projectId,
        [FromQuery] string? search,
        ListSchemasHandler handler,
        CancellationToken ct)
    {
        var query = new ListSchemasQuery(projectId, search);
        Result<List<JsonSchemaDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dtos => TypedResults.Ok(dtos),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetSchema(
        Guid id,
        GetSchemaHandler handler,
        CancellationToken ct)
    {
        var query = new GetSchemaQuery(id);
        Result<JsonSchemaDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteSchema(
        Guid id,
        DeleteSchemaHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteSchemaCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> ExecuteStructuredInference(
        Guid id,
        [FromBody] StructuredInferenceRequest request,
        StructuredInferenceHandler handler,
        CancellationToken ct)
    {
        var command = new StructuredInferenceCommand(
            id, request.InstanceId, request.Model, request.Messages, request.Temperature, request.MaxTokens);

        Result<StructuredInferenceResultDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }
}
