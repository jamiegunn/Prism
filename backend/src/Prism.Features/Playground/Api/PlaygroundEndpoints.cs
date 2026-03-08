using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Prism.Common.Abstractions;
using Prism.Common.Results;
using Prism.Features.Playground.Api.Requests;
using Prism.Features.Playground.Application.DeleteConversation;
using Prism.Features.Playground.Application.Dtos;
using Prism.Features.Playground.Application.ExportConversation;
using Prism.Features.Playground.Application.GetConversation;
using Prism.Features.Playground.Application.ListConversations;
using Prism.Features.Playground.Application.StreamChat;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Api;

/// <summary>
/// Defines the HTTP endpoints for the Playground feature including SSE streaming chat,
/// conversation management, and export functionality.
/// </summary>
public static class PlaygroundEndpoints
{
    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    /// <summary>
    /// Maps the playground endpoints under <c>/api/v1/playground</c>.
    /// </summary>
    /// <param name="app">The endpoint route builder (typically the WebApplication).</param>
    /// <returns>The route group builder for further configuration.</returns>
    public static RouteGroupBuilder MapPlaygroundEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/v1/playground")
            .WithTags("Playground");

        group.MapPost("/chat", StreamChat)
            .WithName("StreamChat")
            .WithSummary("Stream a chat response via SSE")
            .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
            .ProducesValidationProblem();

        group.MapGet("/conversations", ListConversations)
            .WithName("ListPlaygroundConversations")
            .WithSummary("Lists playground conversations with pagination")
            .Produces<PagedResult<ConversationSummaryDto>>();

        group.MapGet("/conversations/{id:guid}", GetConversation)
            .WithName("GetPlaygroundConversation")
            .WithSummary("Gets a playground conversation by ID with all messages")
            .Produces<ConversationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/conversations/{id:guid}", DeleteConversation)
            .WithName("DeletePlaygroundConversation")
            .WithSummary("Deletes a playground conversation and all its messages")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/conversations/{id:guid}/export", ExportConversation)
            .WithName("ExportPlaygroundConversation")
            .WithSummary("Exports a playground conversation in the specified format")
            .Produces<ExportResult>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task StreamChat(
        [FromBody] SendMessageRequest request,
        StreamChatHandler handler,
        HttpContext httpContext,
        CancellationToken ct)
    {
        httpContext.Response.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var command = new StreamChatCommand(
            request.ConversationId,
            request.InstanceId,
            request.SystemPrompt,
            request.Message,
            new ConversationParameters
            {
                Temperature = request.Temperature,
                TopP = request.TopP,
                TopK = request.TopK,
                MaxTokens = request.MaxTokens,
                StopSequences = request.StopSequences,
                FrequencyPenalty = request.FrequencyPenalty,
                PresencePenalty = request.PresencePenalty,
                Logprobs = request.Logprobs,
                TopLogprobs = request.TopLogprobs
            });

        await foreach (StreamChatEvent chatEvent in handler.HandleAsync(command, ct))
        {
            string eventType = chatEvent switch
            {
                ChatStarted => "started",
                ChatTokenReceived => "token",
                ChatCompleted => "completed",
                ChatError => "error",
                _ => "unknown"
            };

            string json = JsonSerializer.Serialize(chatEvent, chatEvent.GetType(), SseJsonOptions);
            await httpContext.Response.WriteAsync($"event: {eventType}\ndata: {json}\n\n", ct);
            await httpContext.Response.Body.FlushAsync(ct);
        }
    }

    private static async Task<IResult> ListConversations(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        ListConversationsHandler handler,
        CancellationToken ct)
    {
        var query = new ListConversationsQuery(search, page ?? 1, pageSize ?? 20);
        Result<PagedResult<ConversationSummaryDto>> result = await handler.HandleAsync(query, ct);

        return result.Match(
            pagedResult => TypedResults.Ok(pagedResult),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> GetConversation(
        Guid id,
        [FromQuery] bool? includeLogprobs,
        GetConversationHandler handler,
        CancellationToken ct)
    {
        var query = new GetConversationQuery(id, includeLogprobs ?? true);
        Result<ConversationDto> result = await handler.HandleAsync(query, ct);

        return result.Match(
            dto => TypedResults.Ok(dto),
            error => error.ToHttpResult());
    }

    private static async Task<IResult> DeleteConversation(
        Guid id,
        DeleteConversationHandler handler,
        CancellationToken ct)
    {
        var command = new DeleteConversationCommand(id);
        Result result = await handler.HandleAsync(command, ct);

        return result.ToHttpResult();
    }

    private static async Task<IResult> ExportConversation(
        Guid id,
        [FromQuery] string? format,
        ExportConversationHandler handler,
        CancellationToken ct)
    {
        ExportFormat exportFormat = ExportFormat.Json;
        if (format is not null && Enum.TryParse<ExportFormat>(format, ignoreCase: true, out ExportFormat parsed))
        {
            exportFormat = parsed;
        }

        var query = new ExportConversationQuery(id, exportFormat);
        Result<ExportResult> result = await handler.HandleAsync(query, ct);

        return result.Match(
            export => TypedResults.Text(export.Content, export.ContentType),
            error => error.ToHttpResult());
    }
}
