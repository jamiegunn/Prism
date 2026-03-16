using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Results;
using Prism.Features.FineTuning.Api.Requests;
using Prism.Features.FineTuning.Application.CreateAdapter;
using Prism.Features.FineTuning.Application.DeleteAdapter;
using Prism.Features.FineTuning.Application.Dtos;
using Prism.Features.FineTuning.Application.ExportDataset;
using Prism.Features.FineTuning.Application.ListAdapters;
using Prism.Features.FineTuning.Domain;

namespace Prism.Features.FineTuning.Api;

/// <summary>
/// Defines the HTTP endpoints for the Fine-Tuning feature including LoRA adapter
/// management and dataset export in fine-tuning formats.
/// </summary>
public static class FineTuningEndpoints
{
    /// <summary>
    /// Maps the fine-tuning endpoints under <c>/api/v1/fine-tuning</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapFineTuningEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/fine-tuning")
            .WithTags("Fine-Tuning");

        // LoRA Adapters
        group.MapPost("/adapters", CreateAdapter)
            .WithName("CreateLoraAdapter")
            .WithSummary("Registers a new LoRA adapter")
            .Produces<LoraAdapterDto>(StatusCodes.Status200OK);

        group.MapGet("/adapters", ListAdapters)
            .WithName("ListLoraAdapters")
            .WithSummary("Lists registered LoRA adapters")
            .Produces<List<LoraAdapterDto>>();

        group.MapDelete("/adapters/{id:guid}", DeleteAdapter)
            .WithName("DeleteLoraAdapter")
            .WithSummary("Deletes a LoRA adapter registration")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // Export
        group.MapPost("/export", ExportFineTune)
            .WithName("ExportFineTuneDataset")
            .WithSummary("Exports a dataset in a fine-tuning format")
            .Produces<ExportFineTuneResult>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> CreateAdapter(
        [FromBody] CreateAdapterRequest request,
        CreateAdapterHandler handler,
        CancellationToken ct)
    {
        var command = new CreateAdapterCommand(
            request.Name,
            request.Description,
            request.InstanceId,
            request.AdapterPath,
            request.BaseModel);

        Result<LoraAdapterDto> result = await handler.HandleAsync(command, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> ListAdapters(
        [FromQuery] Guid? instanceId,
        ListAdaptersHandler handler,
        CancellationToken ct)
    {
        var query = new ListAdaptersQuery(instanceId);
        Result<List<LoraAdapterDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            list => TypedResults.Ok(list),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteAdapter(
        Guid id,
        DeleteAdapterHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteAdapterCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> ExportFineTune(
        [FromBody] ExportFineTuneRequest request,
        ExportFineTuneHandler handler,
        CancellationToken ct)
    {
        FineTuneExportFormat format = Enum.TryParse<FineTuneExportFormat>(request.Format, true, out FineTuneExportFormat f)
            ? f
            : FineTuneExportFormat.Alpaca;

        var command = new ExportFineTuneCommand(
            request.DatasetId,
            format,
            request.InstructionColumn,
            request.InputColumn,
            request.OutputColumn);

        Result<ExportFineTuneResult> result = await handler.HandleAsync(command, ct);

        return result.Match(
            export => TypedResults.Ok(export),
            error => error.ToHttpResult());
    }
}
