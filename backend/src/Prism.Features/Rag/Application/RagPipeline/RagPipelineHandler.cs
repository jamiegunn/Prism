using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.Rag.Application.Dtos;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Rag.Application.RagPipeline;

/// <summary>
/// Command to execute the full RAG pipeline: retrieve, format context, generate.
/// </summary>
public sealed record RagPipelineCommand(
    Guid CollectionId,
    string Query,
    string Model,
    Guid InstanceId,
    string? SystemPrompt,
    string? PromptTemplate,
    int TopK,
    SearchType SearchType,
    double? Temperature,
    int? MaxTokens);

/// <summary>
/// Handles end-to-end RAG pipeline execution.
/// </summary>
public sealed class RagPipelineHandler
{
    private readonly QueryCollectionHandler _queryHandler;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<RagPipelineHandler> _logger;

    private const string DefaultTemplate =
        """
        Use the following context to answer the question. If the answer is not found in the context, say so.

        Context:
        {{context}}

        Question: {{query}}

        Answer:
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagPipelineHandler"/> class.
    /// </summary>
    /// <param name="queryHandler">The query handler for retrieval.</param>
    /// <param name="providerFactory">The inference provider factory.</param>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public RagPipelineHandler(
        QueryCollectionHandler queryHandler,
        InferenceProviderFactory providerFactory,
        AppDbContext db,
        ILogger<RagPipelineHandler> logger)
    {
        _queryHandler = queryHandler;
        _providerFactory = providerFactory;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Executes the full RAG pipeline.
    /// </summary>
    /// <param name="command">The pipeline command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the pipeline result with generation and sources.</returns>
    public async Task<Result<RagPipelineResultDto>> HandleAsync(RagPipelineCommand command, CancellationToken ct)
    {
        // Resolve inference instance
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
            return Error.NotFound($"Inference instance {command.InstanceId} not found.");

        var sw = Stopwatch.StartNew();

        // Step 1: Retrieve relevant chunks
        var searchQuery = new QueryCollectionQuery(
            command.CollectionId,
            command.Query,
            command.TopK > 0 ? command.TopK : 5,
            command.SearchType);

        Result<List<ChunkSearchResultDto>> searchResult = await _queryHandler.HandleAsync(searchQuery, ct);
        if (searchResult.IsFailure)
            return Result<RagPipelineResultDto>.Failure(searchResult.Error);

        List<ChunkSearchResultDto> chunks = searchResult.Value;

        // Step 2: Format context from retrieved chunks
        string context = string.Join("\n\n---\n\n", chunks.Select((c, i) =>
            $"[Source {i + 1}: {c.DocumentFilename}]\n{c.Content}"));

        // Step 3: Render prompt
        string template = command.PromptTemplate ?? DefaultTemplate;
        string renderedPrompt = template
            .Replace("{{context}}", context)
            .Replace("{{query}}", command.Query);

        // Step 4: Call LLM
        var messages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(command.SystemPrompt))
            messages.Add(new ChatMessage("system", command.SystemPrompt));

        messages.Add(new ChatMessage("user", renderedPrompt));

        var chatRequest = new ChatRequest
        {
            Model = command.Model,
            Messages = messages,
            Temperature = command.Temperature ?? 0.1,
            MaxTokens = command.MaxTokens ?? 2048,
            Logprobs = true,
            TopLogprobs = 5,
            SourceModule = "rag"
        };

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);
        if (chatResult.IsFailure)
            return Result<RagPipelineResultDto>.Failure(chatResult.Error);

        sw.Stop();

        ChatResponse response = chatResult.Value;

        _logger.LogInformation("RAG pipeline completed for collection {CollectionId}: {ChunkCount} chunks, {LatencyMs}ms",
            command.CollectionId, chunks.Count, sw.ElapsedMilliseconds);

        // Persist trace
        var trace = new RagTrace
        {
            CollectionId = command.CollectionId,
            Query = command.Query,
            SearchType = command.SearchType.ToString(),
            RetrievedChunkCount = chunks.Count,
            RetrievedChunksJson = JsonSerializer.Serialize(chunks.Select(c => new
            {
                chunkId = c.ChunkId,
                documentFilename = c.DocumentFilename,
                score = c.Score,
                contentPreview = c.Content.Length > 200 ? c.Content[..200] + "..." : c.Content
            })),
            AssembledContext = context,
            GeneratedResponse = response.Content,
            Model = command.Model,
            LatencyMs = sw.ElapsedMilliseconds,
            TotalTokens = (response.Usage?.TotalTokens) ?? 0,
            IsSuccess = true
        };

        _db.Set<RagTrace>().Add(trace);
        await _db.SaveChangesAsync(ct);

        return new RagPipelineResultDto(
            command.Query,
            response.Content,
            chunks,
            command.Model,
            response.Usage?.PromptTokens ?? 0,
            response.Usage?.CompletionTokens ?? 0,
            sw.Elapsed.TotalMilliseconds,
            renderedPrompt);
    }
}
