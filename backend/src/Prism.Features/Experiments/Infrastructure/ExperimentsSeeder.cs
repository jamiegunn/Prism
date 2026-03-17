using Microsoft.EntityFrameworkCore;
using Prism.Common.Database.Seeders;
using Prism.Features.Experiments.Domain;

namespace Prism.Features.Experiments.Infrastructure;

/// <summary>
/// Seeds a sample research project with an experiment and two completed runs,
/// demonstrating how to compare prompt strategies in Prism.
/// </summary>
public sealed class ExperimentsSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. Experiments seed at order 120, after models and history.
    /// </summary>
    public int Order => 120;

    /// <summary>
    /// Seeds a sample project, experiment, and runs if none exist.
    /// Creates a prompt-quality comparison experiment with two runs showing different prompt styles.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasProjects = await context.Set<Project>().AnyAsync(ct);

        if (hasProjects)
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        Guid projectId = Guid.NewGuid();
        Guid experimentId = Guid.NewGuid();

        var project = new Project
        {
            Id = projectId,
            Name = "Prompt Quality Research",
            Description = "Investigating how system prompt wording affects response quality and confidence",
            IsArchived = false,
            Experiments =
            [
                new Experiment
                {
                    Id = experimentId,
                    ProjectId = projectId,
                    Name = "System Prompt Style Comparison",
                    Status = ExperimentStatus.Active,
                    Hypothesis = "Detailed system prompts with constraints produce lower-perplexity outputs than minimal prompts",
                    Runs =
                    [
                        new Run
                        {
                            ExperimentId = experimentId,
                            Name = "Detailed prompt",
                            Model = "meta-llama/Llama-3.1-8B-Instruct",
                            Parameters = new RunParameters
                            {
                                Temperature = 0.7,
                                MaxTokens = 512
                            },
                            SystemPrompt = "You are a patient and creative science educator. Explain concepts using vivid analogies " +
                                           "and everyday objects. Keep sentences short. Avoid jargon. Target a 10-year-old reading level.",
                            Input = "Explain quantum entanglement to a 10-year-old",
                            Output = "Imagine you have two magic coins. You put one in your pocket and send the other to your " +
                                     "friend across the world. When you flip your coin and it lands on heads, your friend's coin " +
                                     "will ALWAYS land on tails — instantly, no matter how far apart you are!\n\n" +
                                     "That's kind of what quantum entanglement is like. Scientists can link two tiny particles " +
                                     "together so that whatever happens to one immediately affects the other. It's like they're " +
                                     "secretly connected by an invisible thread that stretches across the entire universe.\n\n" +
                                     "Einstein thought this was so weird, he called it 'spooky action at a distance.' And he was right " +
                                     "that it's spooky — but experiments have proven it really works!",
                            PromptTokens = 85,
                            CompletionTokens = 156,
                            TotalTokens = 241,
                            LatencyMs = 2100,
                            Perplexity = 1.85,
                            Status = RunStatus.Completed,
                            FinishReason = "stop",
                            Metrics = new Dictionary<string, double>
                            {
                                ["fluency"] = 0.92,
                                ["relevance"] = 0.88,
                                ["simplicity"] = 0.95
                            },
                            CreatedAt = now.AddMinutes(-10),
                            UpdatedAt = now.AddMinutes(-10)
                        },
                        new Run
                        {
                            ExperimentId = experimentId,
                            Name = "Minimal prompt",
                            Model = "meta-llama/Llama-3.1-8B-Instruct",
                            Parameters = new RunParameters
                            {
                                Temperature = 0.7,
                                MaxTokens = 512
                            },
                            SystemPrompt = "You are a helpful assistant.",
                            Input = "Explain quantum entanglement to a 10-year-old",
                            Output = "Quantum entanglement is when two particles become connected in a special way. When scientists " +
                                     "measure one particle, they instantly know something about the other particle, even if it's very " +
                                     "far away. It's like having two magic dice — when you roll one and get a six, the other one " +
                                     "always shows a one, no matter where it is.\n\n" +
                                     "This happens because the particles share a quantum state. It was first described by physicists " +
                                     "in the 1930s and has been confirmed by many experiments since then. Today, scientists are " +
                                     "trying to use entanglement to build quantum computers and create unbreakable codes.",
                            PromptTokens = 32,
                            CompletionTokens = 142,
                            TotalTokens = 174,
                            LatencyMs = 1980,
                            Perplexity = 2.41,
                            Status = RunStatus.Completed,
                            FinishReason = "stop",
                            Metrics = new Dictionary<string, double>
                            {
                                ["fluency"] = 0.85,
                                ["relevance"] = 0.82,
                                ["simplicity"] = 0.78
                            },
                            CreatedAt = now.AddMinutes(-8),
                            UpdatedAt = now.AddMinutes(-8)
                        }
                    ]
                }
            ]
        };

        context.Set<Project>().Add(project);
        await context.SaveChangesAsync(ct);
    }
}
