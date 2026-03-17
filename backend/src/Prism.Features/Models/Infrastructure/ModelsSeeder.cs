using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Common.Inference;
using Prism.Features.Models.Domain;

namespace Prism.Features.Models.Infrastructure;

/// <summary>
/// Seeds sample inference provider instances on first launch.
/// Provides well-known IDs that other seeders can reference for foreign key relationships.
/// </summary>
public sealed class ModelsSeeder : IDataSeeder
{
    /// <summary>
    /// Well-known seed ID for the vLLM inference instance. Referenced by other seeders.
    /// </summary>
    public static readonly Guid VllmSeedInstanceId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// Well-known seed ID for the Ollama inference instance. Referenced by other seeders.
    /// </summary>
    public static readonly Guid OllamaSeedInstanceId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>
    /// Gets the execution order. Models seed at order 10 so other seeders can reference instances.
    /// </summary>
    public int Order => 10;

    /// <summary>
    /// Seeds sample inference provider instances if none exist.
    /// Creates a local vLLM instance and a local Ollama instance with realistic capabilities.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasInstances = await context.Set<InferenceInstance>().AnyAsync(ct);

        if (hasInstances)
        {
            return;
        }

        List<InferenceInstance> instances =
        [
            new InferenceInstance
            {
                Id = VllmSeedInstanceId,
                Name = "Local vLLM (Llama 3.1 8B)",
                Endpoint = "http://localhost:8000/v1",
                ProviderType = InferenceProviderType.Vllm,
                Status = InstanceStatus.Unknown,
                ModelId = "meta-llama/Llama-3.1-8B-Instruct",
                MaxContextLength = 4096,
                SupportsLogprobs = true,
                MaxTopLogprobs = 20,
                SupportsStreaming = true,
                SupportsMetrics = true,
                SupportsTokenize = true,
                SupportsGuidedDecoding = true,
                SupportsMultimodal = false,
                SupportsModelSwap = false,
                IsDefault = true,
                Tags = ["local", "vllm", "llama"]
            },
            new InferenceInstance
            {
                Id = OllamaSeedInstanceId,
                Name = "Local Ollama (Mistral 7B)",
                Endpoint = "http://localhost:11434",
                ProviderType = InferenceProviderType.Ollama,
                Status = InstanceStatus.Unknown,
                ModelId = "mistral:7b-instruct",
                MaxContextLength = 8192,
                SupportsLogprobs = true,
                MaxTopLogprobs = 5,
                SupportsStreaming = true,
                SupportsMetrics = false,
                SupportsTokenize = false,
                SupportsGuidedDecoding = false,
                SupportsMultimodal = false,
                SupportsModelSwap = true,
                IsDefault = false,
                Tags = ["local", "ollama", "mistral"]
            }
        ];

        context.Set<InferenceInstance>().AddRange(instances);
        await context.SaveChangesAsync(ct);
    }
}
