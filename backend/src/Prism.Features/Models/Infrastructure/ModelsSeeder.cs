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
            await RepointSeededInstancesAsync(context, ct);
            return;
        }

        List<InferenceInstance> instances = SeedInstances();

        context.Set<InferenceInstance>().AddRange(instances);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Corrects the seeded instances' endpoints when the API has moved across the container
    /// boundary since they were written.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// A database seeded by a host-run API and then opened by a containerised one — which is
    /// what happens the first time anyone takes the new default — holds two instances pointing
    /// at <c>localhost</c>, meaning the container itself. They sit permanently offline next to
    /// a working provider, and nothing in the UI can edit an endpoint. Only rows this seeder
    /// owns are touched, and only their address: a registration someone made themselves is
    /// theirs to keep.
    /// </remarks>
    private static async Task RepointSeededInstancesAsync(AppDbContext context, CancellationToken ct)
    {
        Guid[] seededIds = [VllmSeedInstanceId, OllamaSeedInstanceId];

        List<InferenceInstance> seeded = await context.Set<InferenceInstance>()
            .Where(i => seededIds.Contains(i.Id))
            .ToListAsync(ct);

        bool changed = false;

        foreach (InferenceInstance instance in seeded)
        {
            string reachable = LocalEndpoint.AsReachable(instance.Endpoint);

            if (!string.Equals(reachable, instance.Endpoint, StringComparison.OrdinalIgnoreCase))
            {
                instance.Endpoint = reachable;
                instance.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// The instances a fresh development database is seeded with.
    /// </summary>
    /// <returns>The seed instances.</returns>
    /// <remarks>
    /// Separated from <see cref="SeedAsync"/> so the capability flags can be asserted against
    /// what each provider actually declares, without needing a database. Those flags are what
    /// the UI trusts until someone presses Probe Capabilities, so a wrong one here is visible
    /// to every new developer.
    /// </remarks>
    internal static List<InferenceInstance> SeedInstances()
    {
        return
        [
            new InferenceInstance
            {
                Id = VllmSeedInstanceId,
                Name = "Local vLLM (Llama 3.1 8B)",

                // Addressed for wherever the API is running. The conventional endpoint is
                // written from the host's point of view and means the container itself when
                // the API is containerised, which is now the default way it runs.
                Endpoint = LocalEndpoint.AsReachable("http://localhost:8000/v1"),
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
                Endpoint = LocalEndpoint.AsReachable("http://localhost:11434"),
                ProviderType = InferenceProviderType.Ollama,
                Status = InstanceStatus.Unknown,
                ModelId = "mistral:7b-instruct",
                MaxContextLength = 8192,

                // Ollama returns per-token probabilities from 0.12.11 onwards, so the heatmap,
                // entropy and surprise views do work here. An older server is corrected down to
                // false when its capabilities are probed — the seed describes a current Ollama,
                // which is what a new developer will have installed.
                SupportsLogprobs = true,
                MaxTopLogprobs = 20,
                SupportsStreaming = true,
                SupportsMetrics = false,
                SupportsTokenize = false,

                // Ollama constrains generation to a JSON schema via `format` from 0.5.0,
                // verified against a live server. Prism's transport has always sent it; the
                // capability flag was what kept Structured Output on the fallback path.
                SupportsGuidedDecoding = true,
                SupportsMultimodal = false,
                SupportsModelSwap = true,
                IsDefault = false,
                Tags = ["local", "ollama", "mistral"]
            }
        ];
    }
}
