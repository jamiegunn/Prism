using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Experiments.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.PromptLab.Application.Dtos;
using Prism.Features.PromptLab.Application.Rendering;
using Prism.Features.PromptLab.Domain;

namespace Prism.Features.PromptLab.Application.AbTest;

/// <summary>
/// Handles A/B test execution by creating an experiment and running all combinations
/// of variations, instances, and parameter sets.
/// </summary>
public sealed class AbTestHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly TemplateRenderer _renderer;
    private readonly ILogger<AbTestHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AbTestHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="renderer">The template renderer for variable substitution.</param>
    /// <param name="logger">The logger instance.</param>
    public AbTestHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        TemplateRenderer renderer,
        ILogger<AbTestHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>
    /// Creates an experiment and executes all A/B test combinations.
    /// Runs are executed sequentially in the current request (no background queue yet).
    /// </summary>
    /// <param name="command">The A/B test command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the A/B test result with the experiment ID.</returns>
    public async Task<Result<AbTestResultDto>> HandleAsync(AbTestCommand command, CancellationToken ct)
    {
        // Validate project exists
        bool projectExists = await _db.Set<Experiments.Domain.Project>()
            .AnyAsync(p => p.Id == command.ProjectId, ct);

        if (!projectExists)
        {
            return Error.NotFound($"Project '{command.ProjectId}' was not found.");
        }

        // Resolve all instances
        List<Guid> instanceIds = command.InstanceIds;
        List<InferenceInstance> instances = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .Where(i => instanceIds.Contains(i.Id))
            .ToListAsync(ct);

        if (instances.Count != instanceIds.Count)
        {
            List<Guid> found = instances.Select(i => i.Id).ToList();
            List<Guid> missing = instanceIds.Except(found).ToList();
            return Error.NotFound($"Inference instances not found: {string.Join(", ", missing)}");
        }

        // Resolve all prompt versions
        var versionLookup = new Dictionary<(Guid TemplateId, int Version), PromptVersion>();
        foreach (AbTestVariation variation in command.Variations)
        {
            var key = (variation.TemplateId, variation.VersionNumber);
            if (!versionLookup.ContainsKey(key))
            {
                PromptVersion? version = await _db.Set<PromptVersion>()
                    .AsNoTracking()
                    .FirstOrDefaultAsync(v => v.TemplateId == variation.TemplateId && v.Version == variation.VersionNumber, ct);

                if (version is null)
                {
                    return Error.NotFound($"Version {variation.VersionNumber} of template '{variation.TemplateId}' was not found.");
                }

                versionLookup[key] = version;
            }
        }

        // Create the experiment
        int totalCombinations = command.Variations.Count * instances.Count * command.ParameterSets.Count * command.RunsPerCombo;

        var experiment = new Experiment
        {
            ProjectId = command.ProjectId,
            Name = command.ExperimentName,
            Description = $"A/B test with {command.Variations.Count} variations, {instances.Count} instances, {command.ParameterSets.Count} parameter sets ({totalCombinations} total runs)",
            Hypothesis = "A/B comparison of prompt variations"
        };

        _db.Set<Experiment>().Add(experiment);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Created A/B test experiment {ExperimentId} with {TotalCombinations} combinations",
            experiment.Id, totalCombinations);

        // Execute all combinations
        int completed = 0;
        int failed = 0;

        foreach (AbTestVariation variation in command.Variations)
        {
            PromptVersion version = versionLookup[(variation.TemplateId, variation.VersionNumber)];

            Result<RenderResult> renderResult = _renderer.Render(version, variation.Variables);
            if (renderResult.IsFailure)
            {
                _logger.LogWarning(
                    "Skipping variation for template {TemplateId} v{Version}: {Error}",
                    variation.TemplateId, variation.VersionNumber, renderResult.Error.Message);
                continue;
            }

            RenderResult rendered = renderResult.Value;

            foreach (InferenceInstance instance in instances)
            {
                IInferenceProvider provider = _providerFactory.CreateProvider(
                    instance.Name, instance.Endpoint, instance.ProviderType);

                foreach (AbTestParameterSet paramSet in command.ParameterSets)
                {
                    for (int i = 0; i < command.RunsPerCombo; i++)
                    {
                        if (ct.IsCancellationRequested) break;

                        var run = new Run
                        {
                            ExperimentId = experiment.Id,
                            Name = $"v{variation.VersionNumber} | {instance.Name} | T={paramSet.Temperature} | #{i + 1}",
                            Model = instance.ModelId ?? "",
                            InstanceId = instance.Id,
                            Parameters = new RunParameters
                            {
                                Temperature = paramSet.Temperature,
                                TopP = paramSet.TopP,
                                MaxTokens = paramSet.MaxTokens
                            },
                            PromptVersionId = version.Id,
                            Input = rendered.RenderedUserPrompt,
                            SystemPrompt = version.SystemPrompt,
                            Status = RunStatus.Running
                        };

                        try
                        {
                            var chatRequest = new ChatRequest
                            {
                                Model = instance.ModelId ?? "",
                                Messages = rendered.Messages,
                                Temperature = paramSet.Temperature,
                                TopP = paramSet.TopP,
                                MaxTokens = paramSet.MaxTokens,
                                Stream = false,
                                SourceModule = "prompt-lab-ab-test"
                            };

                            var stopwatch = Stopwatch.StartNew();
                            Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);
                            stopwatch.Stop();

                            if (chatResult.IsSuccess)
                            {
                                ChatResponse response = chatResult.Value;
                                run.Output = response.Content;
                                run.PromptTokens = response.Usage?.PromptTokens ?? 0;
                                run.CompletionTokens = response.Usage?.CompletionTokens ?? 0;
                                run.TotalTokens = response.Usage?.TotalTokens ?? 0;
                                run.LatencyMs = response.Timing?.LatencyMs ?? stopwatch.ElapsedMilliseconds;
                                run.TtftMs = response.Timing?.TtftMs.HasValue == true ? (int)response.Timing.TtftMs.Value : null;
                                run.TokensPerSecond = response.Timing?.TokensPerSecond;
                                run.FinishReason = response.FinishReason;
                                run.Status = RunStatus.Completed;
                                completed++;
                            }
                            else
                            {
                                run.Error = chatResult.Error.Message;
                                run.Status = RunStatus.Failed;
                                failed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            run.Error = ex.Message;
                            run.Status = RunStatus.Failed;
                            failed++;
                        }

                        _db.Set<Run>().Add(run);
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "A/B test {ExperimentId} completed: {Completed} succeeded, {Failed} failed",
            experiment.Id, completed, failed);

        return new AbTestResultDto(experiment.Id, totalCombinations, "completed");
    }
}
