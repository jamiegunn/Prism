using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Prism.Common.Database;
using Prism.Features.Rag.Application.QueryCollection;
using Prism.Features.Rag.Domain;

namespace Prism.Features.Agents.Domain.Tools;

/// <summary>
/// A tool that queries a RAG collection and returns matching chunks.
/// Input format: "collection_id|query" or just "query" (uses first available collection).
/// </summary>
public sealed class RagQueryTool : IAgentTool
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RagQueryTool"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for resolving scoped dependencies.</param>
    public RagQueryTool(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public string Name => "rag_query";

    /// <inheritdoc />
    public string Description => "Searches a RAG collection for relevant documents. Input format: 'collection_id|query' or just 'query' (uses first available collection).";

    /// <inheritdoc />
    public string ParameterSchema => """{"type": "string", "description": "Search query, optionally prefixed with 'collection_id|'"}""";

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(string input, CancellationToken ct)
    {
        try
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            QueryCollectionHandler handler = scope.ServiceProvider.GetRequiredService<QueryCollectionHandler>();
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string query;
            Guid collectionId;

            string[] parts = input.Split('|', 2);
            if (parts.Length == 2 && Guid.TryParse(parts[0].Trim(), out Guid parsedId))
            {
                collectionId = parsedId;
                query = parts[1].Trim();
            }
            else
            {
                query = input.Trim();
                // Find first available collection
                RagCollection? collection = await db.Set<RagCollection>()
                    .OrderByDescending(c => c.CreatedAt)
                    .FirstOrDefaultAsync(ct);

                if (collection is null)
                {
                    return ToolResult.Fail("No RAG collections available.");
                }

                collectionId = collection.Id;
            }

            var queryObj = new QueryCollectionQuery(collectionId, query, 5, SearchType.Hybrid);
            Common.Results.Result<List<Rag.Application.Dtos.ChunkSearchResultDto>> result = await handler.HandleAsync(queryObj, ct);

            if (result.IsFailure)
            {
                return ToolResult.Fail($"RAG query failed: {result.Error.Message}");
            }

            if (result.Value.Count == 0)
            {
                return ToolResult.Ok("No relevant documents found.");
            }

            var sb = new System.Text.StringBuilder();
            foreach (Rag.Application.Dtos.ChunkSearchResultDto chunk in result.Value)
            {
                sb.AppendLine($"[{chunk.DocumentFilename} (score: {chunk.Score:F3})]");
                sb.AppendLine(chunk.Content);
                sb.AppendLine();
            }

            return ToolResult.Ok(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            return ToolResult.Fail($"RAG query error: {ex.Message}");
        }
    }
}
