using System.Diagnostics;
using System.Runtime.CompilerServices;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Prism.Common.Inference;
using Prism.Common.Inference.Metrics;
using Prism.Common.Inference.Models;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.Playground.Application.Dtos;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Application.StreamChat;

/// <summary>
/// Handles streaming chat requests in the playground. Manages conversation persistence,
/// provider interaction via SSE streaming, logprobs collection, and performance metrics.
/// </summary>
public sealed class StreamChatHandler
{
    private readonly AppDbContext _db;
    private readonly InferenceProviderFactory _providerFactory;
    private readonly IValidator<StreamChatCommand> _validator;
    private readonly ILogger<StreamChatHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamChatHandler"/> class.
    /// </summary>
    /// <param name="db">The application database context.</param>
    /// <param name="providerFactory">The factory for creating inference provider instances.</param>
    /// <param name="validator">The validator for the stream chat command.</param>
    /// <param name="logger">The logger instance.</param>
    public StreamChatHandler(
        AppDbContext db,
        InferenceProviderFactory providerFactory,
        IValidator<StreamChatCommand> validator,
        ILogger<StreamChatHandler> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Streams a chat response as an async enumerable of <see cref="StreamChatEvent"/> instances.
    /// Creates or continues a conversation, streams tokens from the inference provider,
    /// collects logprobs and metrics, and persists the completed message.
    /// </summary>
    /// <param name="command">The stream chat command with conversation and inference parameters.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>An async enumerable of stream chat events.</returns>
    public async IAsyncEnumerable<StreamChatEvent> HandleAsync(
        StreamChatCommand command,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Validate
        ValidationResult validationResult = await _validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            string errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            yield return new ChatError(errors);
            yield break;
        }

        // Find the inference instance
        InferenceInstance? instance = await _db.Set<InferenceInstance>()
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InstanceId, ct);

        if (instance is null)
        {
            yield return new ChatError($"Inference instance '{command.InstanceId}' was not found.");
            yield break;
        }

        // Load or create conversation
        Conversation conversation;
        if (command.ConversationId.HasValue)
        {
            Conversation? existing = await _db.Set<Conversation>()
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == command.ConversationId.Value, ct);

            if (existing is null)
            {
                yield return new ChatError($"Conversation '{command.ConversationId.Value}' was not found.");
                yield break;
            }

            conversation = existing;
        }
        else
        {
            string title = command.UserMessage.Length > 100
                ? command.UserMessage[..100] + "..."
                : command.UserMessage;

            conversation = new Conversation
            {
                Title = title,
                SystemPrompt = command.SystemPrompt,
                ModelId = command.Model ?? instance.ModelId ?? "unknown",
                InstanceId = command.InstanceId,
                Parameters = command.Parameters
            };

            _db.Set<Conversation>().Add(conversation);
            await _db.SaveChangesAsync(ct);
        }

        // Add the user message
        int nextSortOrder = conversation.Messages.Count > 0
            ? conversation.Messages.Max(m => m.SortOrder) + 1
            : 0;

        var userMessage = new Message
        {
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Content = command.UserMessage,
            SortOrder = nextSortOrder
        };

        conversation.Messages.Add(userMessage);
        _db.Set<Message>().Add(userMessage);
        await _db.SaveChangesAsync(ct);

        // Prepare the assistant message placeholder
        var assistantMessage = new Message
        {
            ConversationId = conversation.Id,
            Role = MessageRole.Assistant,
            Content = "",
            SortOrder = nextSortOrder + 1
        };

        // Emit started event
        yield return new ChatStarted(conversation.Id, assistantMessage.Id);

        // Build chat request from conversation messages
        var chatMessages = new List<ChatMessage>();

        if (!string.IsNullOrWhiteSpace(conversation.SystemPrompt))
        {
            chatMessages.Add(ChatMessage.System(conversation.SystemPrompt));
        }

        foreach (Message msg in conversation.Messages.OrderBy(m => m.SortOrder))
        {
            string role = msg.Role switch
            {
                MessageRole.System => ChatMessage.SystemRole,
                MessageRole.User => ChatMessage.UserRole,
                MessageRole.Assistant => ChatMessage.AssistantRole,
                _ => ChatMessage.UserRole
            };

            chatMessages.Add(new ChatMessage(role, msg.Content));
        }

        // The model that runs is the one the chosen instance serves, not the one stored on the
        // conversation when it was created. Those differ in two ordinary situations, and the
        // stored value was wrong in both: after switching instance mid-conversation, and on the
        // seeded conversations, which carry the placeholder "sample". Asking a real server for
        // "sample" is a 404 the Playground displayed as an empty answer in eight milliseconds,
        // while the picker and the status bar both named the model that should have run.
        string requestModel = ModelSelection
            .Resolve(instance, command.Model, instance.ModelId, conversation.ModelId)
            .Match(model => model, _ => conversation.ModelId);

        // Kept on the conversation so its record of what it ran on stays true.
        if (!string.Equals(conversation.ModelId, requestModel, StringComparison.Ordinal))
        {
            conversation.ModelId = requestModel;
            conversation.InstanceId = command.InstanceId;
        }

        var chatRequest = new ChatRequest
        {
            Model = requestModel,
            Messages = chatMessages,
            Temperature = command.Parameters.Temperature,
            TopP = command.Parameters.TopP,
            TopK = command.Parameters.TopK,
            MaxTokens = command.Parameters.MaxTokens,
            StopSequences = command.Parameters.StopSequences,
            FrequencyPenalty = command.Parameters.FrequencyPenalty,
            PresencePenalty = command.Parameters.PresencePenalty,
            Logprobs = command.Parameters.Logprobs,
            TopLogprobs = command.Parameters.TopLogprobs,
            Stream = true,
            SourceModule = "playground"
        };

        // Create provider and stream
        IInferenceProvider provider = _providerFactory.CreateProvider(
            instance.Name, instance.Endpoint, instance.ProviderType);

        var contentBuilder = new System.Text.StringBuilder();
        var tokenLogprobs = new List<TokenLogprob>();
        string? finishReason = null;
        int tokenCount = 0;
        long? ttftMs = null;
        UsageInfo? usage = null;
        Stopwatch stopwatch = Stopwatch.StartNew();

        // Note: yield return cannot appear inside try/catch, so we use a queue pattern
        // to collect events and yield them outside exception-handling blocks.
        IAsyncEnumerable<StreamChunk>? stream = null;
        string? initError = null;

        try
        {
            stream = provider.StreamChatAsync(chatRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initiate streaming chat with provider {ProviderName}", instance.Name);
            initError = $"Failed to connect to inference provider: {ex.Message}";
        }

        if (initError is not null)
        {
            yield return new ChatError(initError);
            yield break;
        }

        string? streamError = null;

        // Enumerated manually rather than with `await foreach`: an iterator method cannot wrap
        // `yield return` in a try/catch, so a provider fault mid-stream previously escaped this
        // method, aborted the SSE response, and discarded the partial assistant message.
        // Advancing the enumerator inside the try lets the fault be reported and the partial
        // response persisted.
        ConfiguredCancelableAsyncEnumerable<StreamChunk>.Enumerator enumerator =
            stream!.WithCancellation(ct).GetAsyncEnumerator();

        while (true)
        {
            StreamChunk chunk;

            try
            {
                if (!await enumerator.MoveNextAsync())
                {
                    break;
                }

                chunk = enumerator.Current;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // The caller disconnected or cancelled. Keep what was generated; do not
                // report it as a provider failure.
                streamError = null;
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Stream from provider {ProviderName} faulted after {TokenCount} tokens",
                    instance.Name,
                    tokenCount);
                streamError = $"Inference stream failed after {tokenCount} tokens: {ex.Message}";
                break;
            }

            if (chunk.IsFirst)
            {
                ttftMs = stopwatch.ElapsedMilliseconds;
            }

            ChatTokenReceived? tokenEvent = null;

            if (!string.IsNullOrEmpty(chunk.Content))
            {
                contentBuilder.Append(chunk.Content);
                tokenCount++;

                TokenLogprobInfo? logprobInfo = null;
                if (chunk.LogprobsEntry is not null)
                {
                    tokenLogprobs.Add(chunk.LogprobsEntry);

                    List<TokenAlternative> alternatives = chunk.LogprobsEntry.TopLogprobs
                        .Select(tl => new TokenAlternative(tl.Token, tl.Logprob, tl.Probability))
                        .ToList();

                    logprobInfo = new TokenLogprobInfo(
                        chunk.LogprobsEntry.Token,
                        chunk.LogprobsEntry.Logprob,
                        chunk.LogprobsEntry.Probability,
                        alternatives);
                }

                tokenEvent = new ChatTokenReceived(chunk.Content, logprobInfo);
            }

            if (chunk.FinishReason is not null)
            {
                finishReason = chunk.FinishReason;
            }

            if (chunk.Usage is not null)
            {
                usage = chunk.Usage;
            }

            if (tokenEvent is not null)
            {
                yield return tokenEvent;
            }
        }

        await enumerator.DisposeAsync();

        stopwatch.Stop();

        if (streamError is not null)
        {
            // Report the failure, then continue to persistence: a partial response is
            // evidence about model behaviour and is exactly what a researcher needs to
            // inspect after a stream dies.
            yield return new ChatError(streamError);
        }

        // Calculate metrics
        long latencyMs = stopwatch.ElapsedMilliseconds;
        double? tokensPerSecond = latencyMs > 0 && tokenCount > 0
            ? tokenCount / (latencyMs / 1000.0)
            : null;

        double? perplexity = null;
        string? logprobsJson = null;

        if (tokenLogprobs.Count > 0)
        {
            var logprobsData = new LogprobsData { Tokens = tokenLogprobs };
            perplexity = LogprobsCalculator.CalculatePerplexity(logprobsData);
            logprobsJson = JsonSerializer.Serialize(logprobsData);
        }

        // Populate and save assistant message
        assistantMessage.Content = contentBuilder.ToString();
        assistantMessage.TokenCount = usage?.CompletionTokens ?? tokenCount;
        assistantMessage.LogprobsJson = logprobsJson;
        assistantMessage.Perplexity = perplexity;
        assistantMessage.LatencyMs = (int)latencyMs;
        assistantMessage.TtftMs = ttftMs.HasValue ? (int)ttftMs.Value : null;
        assistantMessage.TokensPerSecond = tokensPerSecond;
        assistantMessage.FinishReason = finishReason;

        conversation.Messages.Add(assistantMessage);
        _db.Set<Message>().Add(assistantMessage);

        // Update conversation totals
        int totalTokensUsed = usage?.TotalTokens ?? tokenCount;
        conversation.TotalTokens += totalTokensUsed;
        conversation.LastMessageAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Completed streaming chat for conversation {ConversationId} with {TokenCount} tokens in {LatencyMs}ms",
            conversation.Id, tokenCount, latencyMs);

        // Emit completed event
        MessageDto messageDto = MessageDto.FromEntity(assistantMessage);
        ConversationDto conversationDto = ConversationDto.FromEntity(conversation);

        yield return new ChatCompleted(messageDto, conversationDto);
    }
}
