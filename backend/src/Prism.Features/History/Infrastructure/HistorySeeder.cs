using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Common.Inference;
using Prism.Features.History.Domain;
using Prism.Features.Models.Infrastructure;

namespace Prism.Features.History.Infrastructure;

/// <summary>
/// Seeds sample inference history records on first launch, demonstrating successful
/// and failed inference calls across different providers and modules.
/// </summary>
public sealed class HistorySeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. History seeds at order 110, after models.
    /// </summary>
    public int Order => 110;

    /// <summary>
    /// Seeds sample inference history records if none exist.
    /// Creates records for a playground chat, a token-explorer call, and a failed connection.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasRecords = await context.Set<InferenceRecord>().AnyAsync(ct);

        if (hasRecords)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;

        List<InferenceRecord> records =
        [
            new InferenceRecord
            {
                SourceModule = "Playground",
                ProviderName = "Local vLLM",
                ProviderType = InferenceProviderType.Vllm,
                ProviderEndpoint = "http://localhost:8000/v1",
                Model = "meta-llama/Llama-3.1-8B-Instruct",
                RequestJson = JsonSerializer.Serialize(new
                {
                    model = "meta-llama/Llama-3.1-8B-Instruct",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful geography assistant." },
                        new { role = "user", content = "What is the capital of France?" }
                    },
                    temperature = 0.7,
                    max_tokens = 256
                }),
                ResponseJson = JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-seed-001",
                    model = "meta-llama/Llama-3.1-8B-Instruct",
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = "The capital of France is Paris." }, finish_reason = "stop" }
                    },
                    usage = new { prompt_tokens = 45, completion_tokens = 128, total_tokens = 173 }
                }),
                PromptTokens = 45,
                CompletionTokens = 128,
                TotalTokens = 173,
                LatencyMs = 1250,
                TtftMs = 85,
                Perplexity = 2.34,
                IsSuccess = true,
                Tags = ["demo", "geography"],
                StartedAt = now.AddMilliseconds(-1250),
                CompletedAt = now
            },
            new InferenceRecord
            {
                SourceModule = "TokenExplorer",
                ProviderName = "Local vLLM",
                ProviderType = InferenceProviderType.Vllm,
                ProviderEndpoint = "http://localhost:8000/v1",
                Model = "meta-llama/Llama-3.1-8B-Instruct",
                RequestJson = JsonSerializer.Serialize(new
                {
                    model = "meta-llama/Llama-3.1-8B-Instruct",
                    messages = new[]
                    {
                        new { role = "user", content = "The quick brown" }
                    },
                    max_tokens = 1,
                    logprobs = true,
                    top_logprobs = 10
                }),
                ResponseJson = JsonSerializer.Serialize(new
                {
                    id = "chatcmpl-seed-002",
                    model = "meta-llama/Llama-3.1-8B-Instruct",
                    choices = new[]
                    {
                        new { message = new { role = "assistant", content = " fox" }, finish_reason = "length" }
                    },
                    usage = new { prompt_tokens = 12, completion_tokens = 1, total_tokens = 13 }
                }),
                PromptTokens = 12,
                CompletionTokens = 1,
                TotalTokens = 13,
                LatencyMs = 95,
                IsSuccess = true,
                Tags = ["demo", "next-token"],
                StartedAt = now.AddMilliseconds(-95),
                CompletedAt = now
            },
            new InferenceRecord
            {
                SourceModule = "Playground",
                ProviderName = "Local Ollama",
                ProviderType = InferenceProviderType.Ollama,
                ProviderEndpoint = "http://localhost:11434",
                Model = "mistral:7b-instruct",
                RequestJson = JsonSerializer.Serialize(new
                {
                    model = "mistral:7b-instruct",
                    messages = new[]
                    {
                        new { role = "user", content = "Hello, how are you?" }
                    },
                    temperature = 0.7
                }),
                ResponseJson = null,
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0,
                LatencyMs = 3000,
                IsSuccess = false,
                ErrorMessage = "Connection refused: target machine actively refused it",
                Tags = ["demo", "error"],
                StartedAt = now.AddMilliseconds(-3000),
                CompletedAt = now
            }
        ];

        context.Set<InferenceRecord>().AddRange(records);
        await context.SaveChangesAsync(ct);
    }
}
