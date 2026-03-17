using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Inference.Models;
using Prism.Common.Results;
using Prism.Features.Experiments.Application.Dtos;
using Prism.Features.Experiments.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;

namespace Prism.Features.Experiments.Application.RunSweep;

/// <summary>
/// Executes a parameter sweep across specified parameter ranges,
/// creating one run per combination. Runs inference for each combination
/// and stores the results.
/// </summary>
public sealed class RunSweepHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly ILogger<RunSweepHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RunSweepHandler"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="providerFactory">The provider factory for executing inference.</param>
    /// <param name="logger">The logger.</param>
    public RunSweepHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        ILogger<RunSweepHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes a parameter sweep, running inference for each parameter combination.
    /// </summary>
    /// <param name="command">The sweep configuration.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the sweep summary.</returns>
    public async Task<Result<SweepResultDto>> HandleAsync(RunSweepCommand command, CancellationToken ct)
    {
        Experiment? experiment = await _db.Set<Experiment>()
            .FirstOrDefaultAsync(e => e.Id == command.ExperimentId, ct);

        if (experiment is null)
        {
            return Result<SweepResultDto>.Failure(Error.NotFound($"Experiment {command.ExperimentId} not found."));
        }

        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Result<SweepResultDto>.Failure(Error.NotFound($"Instance {command.InstanceId} not found."));
        }

        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        // Generate cartesian product of parameter values
        List<RunParameters> combinations = GenerateCombinations(command);

        _logger.LogInformation(
            "Starting sweep for experiment {ExperimentId} with {CombinationCount} combinations",
            command.ExperimentId, combinations.Count);

        int completed = 0;
        int failed = 0;
        List<Run> runs = [];

        for (int i = 0; i < combinations.Count; i++)
        {
            RunParameters paramSet = combinations[i];

            var chatRequest = new ChatRequest
            {
                Model = instance.ModelId ?? command.Model ?? "",
                Messages = BuildMessages(command.SystemPrompt, command.Input),
                Temperature = paramSet.Temperature,
                TopP = paramSet.TopP,
                TopK = paramSet.TopK,
                MaxTokens = paramSet.MaxTokens,
                Logprobs = command.CaptureLogprobs,
                TopLogprobs = command.CaptureLogprobs ? 5 : null,
                Stream = false,
                SourceModule = "experiments-sweep"
            };

            var run = new Run
            {
                ExperimentId = command.ExperimentId,
                Name = $"Sweep #{i + 1} (T={paramSet.Temperature:F2})",
                Model = chatRequest.Model,
                InstanceId = command.InstanceId,
                Parameters = paramSet,
                PromptVersionId = command.PromptVersionId,
                Input = command.Input,
                SystemPrompt = command.SystemPrompt,
                Tags = ["sweep", $"sweep-batch:{DateTime.UtcNow:yyyyMMddHHmmss}"],
                Status = RunStatus.Running
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                Result<ChatResponse> result = await provider.ChatAsync(chatRequest, ct);
                stopwatch.Stop();

                if (result.IsSuccess)
                {
                    ChatResponse response = result.Value;
                    run.Output = response.Content;
                    run.PromptTokens = response.Usage?.PromptTokens ?? 0;
                    run.CompletionTokens = response.Usage?.CompletionTokens ?? 0;
                    run.TotalTokens = response.Usage?.TotalTokens ?? 0;
                    run.LatencyMs = stopwatch.ElapsedMilliseconds;
                    run.TtftMs = response.Timing?.TtftMs is long ttft ? (int)ttft : null;
                    run.TokensPerSecond = response.Timing?.TokensPerSecond;
                    run.FinishReason = response.FinishReason;
                    run.Status = RunStatus.Completed;
                    completed++;
                }
                else
                {
                    run.Error = result.Error.Message;
                    run.LatencyMs = stopwatch.ElapsedMilliseconds;
                    run.Status = RunStatus.Failed;
                    failed++;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                run.Error = ex.Message;
                run.LatencyMs = stopwatch.ElapsedMilliseconds;
                run.Status = RunStatus.Failed;
                failed++;
                _logger.LogWarning(ex, "Sweep run #{Index} failed", i + 1);
            }

            runs.Add(run);
        }

        _db.Set<Run>().AddRange(runs);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Sweep completed for experiment {ExperimentId}: {Completed} completed, {Failed} failed",
            command.ExperimentId, completed, failed);

        return new SweepResultDto(
            ExperimentId: command.ExperimentId,
            TotalCombinations: combinations.Count,
            Completed: completed,
            Failed: failed,
            RunIds: runs.Select(r => r.Id).ToList());
    }

    /// <summary>
    /// Generates the cartesian product of all parameter ranges.
    /// </summary>
    private static List<RunParameters> GenerateCombinations(RunSweepCommand command)
    {
        List<double> temperatures = command.TemperatureValues.Count > 0
            ? command.TemperatureValues
            : [0.7];
        List<double> topPs = command.TopPValues.Count > 0
            ? command.TopPValues
            : [1.0];
        List<int> maxTokensList = command.MaxTokensValues.Count > 0
            ? command.MaxTokensValues
            : [2048];

        List<RunParameters> combos = [];

        foreach (double temp in temperatures)
        {
            foreach (double topP in topPs)
            {
                foreach (int maxTokens in maxTokensList)
                {
                    combos.Add(new RunParameters
                    {
                        Temperature = temp,
                        TopP = topP,
                        MaxTokens = maxTokens
                    });
                }
            }
        }

        return combos;
    }

    /// <summary>
    /// Builds the chat message list from system prompt and user input.
    /// </summary>
    private static List<ChatMessage> BuildMessages(string? systemPrompt, string input)
    {
        List<ChatMessage> messages = [];
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(ChatMessage.System(systemPrompt));
        }
        messages.Add(ChatMessage.User(input));
        return messages;
    }
}

/// <summary>
/// Command for running a parameter sweep on an experiment.
/// </summary>
/// <param name="ExperimentId">The experiment to add sweep runs to.</param>
/// <param name="InstanceId">The inference provider instance to use.</param>
/// <param name="Input">The user prompt text.</param>
/// <param name="SystemPrompt">Optional system prompt.</param>
/// <param name="Model">Optional model override.</param>
/// <param name="PromptVersionId">Optional prompt version ID for linkage.</param>
/// <param name="TemperatureValues">Temperature values to sweep.</param>
/// <param name="TopPValues">Top-P values to sweep.</param>
/// <param name="MaxTokensValues">Max tokens values to sweep.</param>
/// <param name="CaptureLogprobs">Whether to capture logprobs data.</param>
public sealed record RunSweepCommand(
    Guid ExperimentId,
    Guid InstanceId,
    string Input,
    string? SystemPrompt = null,
    string? Model = null,
    Guid? PromptVersionId = null,
    List<double>? TemperatureValues = null,
    List<double>? TopPValues = null,
    List<int>? MaxTokensValues = null,
    bool CaptureLogprobs = false)
{
    /// <summary>
    /// Gets the temperature values, defaulting to an empty list.
    /// </summary>
    public List<double> TemperatureValues { get; init; } = TemperatureValues ?? [];

    /// <summary>
    /// Gets the top-P values, defaulting to an empty list.
    /// </summary>
    public List<double> TopPValues { get; init; } = TopPValues ?? [];

    /// <summary>
    /// Gets the max tokens values, defaulting to an empty list.
    /// </summary>
    public List<int> MaxTokensValues { get; init; } = MaxTokensValues ?? [];
}

/// <summary>
/// Result summary of a completed parameter sweep.
/// </summary>
/// <param name="ExperimentId">The experiment that received the sweep runs.</param>
/// <param name="TotalCombinations">Total number of parameter combinations executed.</param>
/// <param name="Completed">Number of successfully completed runs.</param>
/// <param name="Failed">Number of failed runs.</param>
/// <param name="RunIds">IDs of all created runs.</param>
public sealed record SweepResultDto(
    Guid ExperimentId,
    int TotalCombinations,
    int Completed,
    int Failed,
    List<Guid> RunIds);
