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

namespace Prism.Features.PromptLab.Application.TestPrompt;

/// <summary>
/// Handles testing a prompt template by rendering it, executing inference, and optionally saving the result as a run.
/// </summary>
public sealed class TestPromptHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly TemplateRenderer _renderer;
    private readonly ILogger<TestPromptHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestPromptHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="renderer">The template renderer for variable substitution.</param>
    /// <param name="logger">The logger instance.</param>
    public TestPromptHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        TemplateRenderer renderer,
        ILogger<TestPromptHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _renderer = renderer;
        _logger = logger;
    }

    /// <summary>
    /// Renders a prompt template version, executes inference, and optionally saves the result as an experiment run.
    /// </summary>
    /// <param name="command">The test prompt command.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the test result DTO on success.</returns>
    public async Task<Result<TestPromptResultDto>> HandleAsync(TestPromptCommand command, CancellationToken ct)
    {
        // Resolve the instance
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            return Error.NotFound($"Inference instance '{command.InstanceId}' was not found.");
        }

        // Resolve the version
        PromptVersion? version;
        if (command.Version.HasValue)
        {
            version = await _db.Set<PromptVersion>()
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.TemplateId == command.TemplateId && v.Version == command.Version.Value, ct);
        }
        else
        {
            version = await _db.Set<PromptVersion>()
                .AsNoTracking()
                .Where(v => v.TemplateId == command.TemplateId)
                .OrderByDescending(v => v.Version)
                .FirstOrDefaultAsync(ct);
        }

        if (version is null)
        {
            return Error.NotFound($"Prompt version not found for template '{command.TemplateId}'.");
        }

        // Render the template
        Result<RenderResult> renderResult = _renderer.Render(version, command.Variables);
        if (renderResult.IsFailure)
        {
            return renderResult.Error;
        }

        RenderResult rendered = renderResult.Value;

        // Create the inference request
        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        var chatRequest = new ChatRequest
        {
            Model = instance.ModelId ?? "",
            Messages = rendered.Messages,
            Temperature = command.Temperature,
            TopP = command.TopP,
            TopK = command.TopK,
            MaxTokens = command.MaxTokens,
            Logprobs = command.Logprobs,
            TopLogprobs = command.TopLogprobs,
            Stream = false,
            SourceModule = "prompt-lab"
        };

        var stopwatch = Stopwatch.StartNew();
        Result<ChatResponse> chatResult = await provider.ChatAsync(chatRequest, ct);
        stopwatch.Stop();

        if (chatResult.IsFailure)
        {
            _logger.LogWarning(
                "TestPrompt failed for template {TemplateId}: {ErrorMessage}",
                command.TemplateId, chatResult.Error.Message);
            return chatResult.Error;
        }

        ChatResponse response = chatResult.Value;

        long latencyMs = response.Timing?.LatencyMs ?? stopwatch.ElapsedMilliseconds;
        int promptTokens = response.Usage?.PromptTokens ?? 0;
        int completionTokens = response.Usage?.CompletionTokens ?? 0;
        int totalTokens = response.Usage?.TotalTokens ?? 0;
        double? tokensPerSecond = response.Timing?.TokensPerSecond;
        long? ttftMs = response.Timing?.TtftMs;

        // Optionally save as a run
        Guid? runId = null;
        if (command.SaveAsRunExperimentId.HasValue)
        {
            bool experimentExists = await _db.Set<Experiment>()
                .AnyAsync(e => e.Id == command.SaveAsRunExperimentId.Value, ct);

            if (experimentExists)
            {
                var run = new Run
                {
                    ExperimentId = command.SaveAsRunExperimentId.Value,
                    Name = command.RunName ?? $"Test: {version.Version}",
                    Model = response.ModelId,
                    InstanceId = command.InstanceId,
                    Parameters = new RunParameters
                    {
                        Temperature = command.Temperature,
                        TopP = command.TopP,
                        TopK = command.TopK,
                        MaxTokens = command.MaxTokens
                    },
                    PromptVersionId = version.Id,
                    Input = rendered.RenderedUserPrompt,
                    Output = response.Content,
                    SystemPrompt = version.SystemPrompt,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    LatencyMs = latencyMs,
                    TtftMs = ttftMs.HasValue ? (int)ttftMs.Value : null,
                    TokensPerSecond = tokensPerSecond,
                    FinishReason = response.FinishReason,
                    Status = RunStatus.Completed
                };

                _db.Set<Run>().Add(run);
                await _db.SaveChangesAsync(ct);
                runId = run.Id;

                _logger.LogInformation(
                    "Saved test result as run {RunId} in experiment {ExperimentId}",
                    run.Id, command.SaveAsRunExperimentId.Value);
            }
        }

        return new TestPromptResultDto(
            response.Content,
            rendered.RenderedUserPrompt,
            response.ModelId,
            promptTokens,
            completionTokens,
            totalTokens,
            latencyMs,
            ttftMs,
            tokensPerSecond,
            response.FinishReason,
            runId);
    }
}
