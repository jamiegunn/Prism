using Microsoft.EntityFrameworkCore;
using Prism.Common.Database;
using Prism.Common.Database.Seeders;
using Prism.Common.Inference.Models;
using Prism.Features.Playground.Domain;

namespace Prism.Features.Playground.Infrastructure;

/// <summary>
/// Seeds sample playground data on first launch, including system prompt templates,
/// a logprobs demonstration conversation, and built-in use case guides.
/// </summary>
public sealed class PlaygroundSeeder : IDataSeeder
{
    /// <summary>
    /// Gets the execution order. Playground seeds run at order 100.
    /// </summary>
    public int Order => 100;

    /// <summary>
    /// Seeds sample playground conversations if none exist.
    /// Creates system prompt templates, a logprobs demo, and use case guides.
    /// </summary>
    /// <param name="context">The application database context.</param>
    /// <param name="ct">A token to cancel the seeding operation.</param>
    /// <returns>A task representing the asynchronous seeding operation.</returns>
    public async Task SeedAsync(AppDbContext context, CancellationToken ct)
    {
        bool hasConversations = await context.Set<Conversation>().AnyAsync(ct);

        if (hasConversations)
        {
            return;
        }

        List<Conversation> conversations =
        [
            CreateSystemPromptTemplate(
                "General Assistant",
                "You are a helpful, harmless, and honest AI assistant. Answer questions clearly and concisely. " +
                "If you are unsure about something, say so rather than guessing. Provide balanced perspectives " +
                "on controversial topics. Format your responses with markdown when it improves readability."),

            CreateSystemPromptTemplate(
                "Code Helper",
                "You are an expert programming assistant. When given code, analyze it carefully before responding. " +
                "Explain bugs clearly with the root cause and fix. When writing code, follow best practices and " +
                "include brief comments for complex logic. If the language is ambiguous, ask for clarification. " +
                "Always consider edge cases and error handling in your suggestions."),

            CreateSystemPromptTemplate(
                "JSON Extractor",
                "You are a structured data extraction assistant. Given unstructured text, extract the relevant " +
                "information and return it as valid JSON. Follow these rules strictly:\n" +
                "1. Output ONLY valid JSON — no markdown fences, no explanation.\n" +
                "2. Use camelCase for property names.\n" +
                "3. Use null for missing values, never empty strings.\n" +
                "4. Use ISO 8601 for dates (e.g., \"2024-01-15T00:00:00Z\").\n" +
                "5. Wrap arrays even if there is only one item.\n" +
                "6. If the input is ambiguous, prefer the most specific interpretation."),

            CreateLogprobsDemoConversation(),

            CreateUseCaseGuide(
                "Model Comparison Guide",
                BuildModelComparisonSystemPrompt(),
                "Compare how two models answer: \"Explain the difference between concurrency and parallelism in simple terms.\"",
                "Concurrency is about dealing with multiple things at once — like a single chef switching between " +
                "chopping vegetables and stirring a pot. The chef can only do one thing at a time, but they " +
                "manage multiple tasks by interleaving.\n\n" +
                "Parallelism is about doing multiple things at once — like having two chefs, each working on " +
                "a different dish simultaneously.\n\n" +
                "In programming:\n" +
                "- **Concurrency**: Multiple tasks make progress by sharing CPU time (e.g., async/await, coroutines)\n" +
                "- **Parallelism**: Multiple tasks literally execute at the same time on different CPU cores (e.g., threads, processes)\n\n" +
                "A system can be concurrent without being parallel (single core, time-slicing), and parallel " +
                "without being concurrent (GPU SIMD operations on independent data)."),

            CreateUseCaseGuide(
                "Hallucination Detection Guide",
                BuildHallucinationDetectionSystemPrompt(),
                "Who won the Nobel Prize in Literature in 2028?",
                "I don't have verified information about the 2028 Nobel Prize in Literature, as my training " +
                "data may not cover events that recent. I would recommend checking the official Nobel Prize " +
                "website at nobelprize.org for the most accurate and up-to-date information.\n\n" +
                "I can tell you about previous Nobel Prize in Literature winners if that would be helpful.")
        ];

        context.Set<Conversation>().AddRange(conversations);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Creates a conversation that serves as a reusable system prompt template.
    /// Contains only a single system message with the prompt text.
    /// </summary>
    /// <param name="title">The template title shown in the conversation list.</param>
    /// <param name="systemPrompt">The system prompt content.</param>
    /// <returns>A conversation entity representing the template.</returns>
    private static Conversation CreateSystemPromptTemplate(string title, string systemPrompt)
    {
        Guid conversationId = Guid.NewGuid();

        return new Conversation
        {
            Id = conversationId,
            Title = title,
            SystemPrompt = systemPrompt,
            ModelId = "sample",
            InstanceId = Guid.Empty,
            IsPinned = false,
            Parameters = new ConversationParameters
            {
                Temperature = 0.7,
                MaxTokens = 2048,
                Logprobs = false
            },
            TotalTokens = 0,
            Messages =
            [
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.System,
                    Content = systemPrompt,
                    SortOrder = 0
                }
            ]
        };
    }

    /// <summary>
    /// Creates the logprobs demonstration conversation with realistic token-level probability data.
    /// Shows how logprobs appear on assistant messages with geography Q&amp;A.
    /// </summary>
    /// <returns>A conversation entity with logprobs-annotated assistant messages.</returns>
    private static Conversation CreateLogprobsDemoConversation()
    {
        Guid conversationId = Guid.NewGuid();
        string systemPrompt = "You are a helpful geography assistant.";

        LogprobsData parisLogprobs = BuildParisLogprobs();
        LogprobsData berlinLogprobs = BuildBerlinLogprobs();

        return new Conversation
        {
            Id = conversationId,
            Title = "Logprobs Demo: Capital Cities",
            SystemPrompt = systemPrompt,
            ModelId = "sample",
            InstanceId = Guid.Empty,
            IsPinned = false,
            Parameters = new ConversationParameters
            {
                Temperature = 0.7,
                MaxTokens = 256,
                Logprobs = true,
                TopLogprobs = 5
            },
            TotalTokens = 52,
            LastMessageAt = DateTime.UtcNow,
            Messages =
            [
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.System,
                    Content = systemPrompt,
                    SortOrder = 0
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.User,
                    Content = "What is the capital of France?",
                    TokenCount = 8,
                    SortOrder = 1
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.Assistant,
                    Content = "The capital of France is Paris.",
                    TokenCount = 7,
                    LogprobsJson = JsonSerializer.Serialize(parisLogprobs),
                    Perplexity = 1.12,
                    LatencyMs = 245,
                    TtftMs = 42,
                    TokensPerSecond = 28.6,
                    FinishReason = "stop",
                    SortOrder = 2
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.User,
                    Content = "And what about Germany?",
                    TokenCount = 6,
                    SortOrder = 3
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.Assistant,
                    Content = "The capital of Germany is Berlin.",
                    TokenCount = 7,
                    LogprobsJson = JsonSerializer.Serialize(berlinLogprobs),
                    Perplexity = 1.18,
                    LatencyMs = 231,
                    TtftMs = 38,
                    TokensPerSecond = 30.3,
                    FinishReason = "stop",
                    SortOrder = 4
                }
            ]
        };
    }

    /// <summary>
    /// Creates a pinned use case guide conversation with a system explanation,
    /// an example user prompt, and an example assistant response.
    /// </summary>
    /// <param name="title">The guide title.</param>
    /// <param name="guideSystemPrompt">Detailed instructions in the system message.</param>
    /// <param name="exampleUserMessage">An example prompt the user might try.</param>
    /// <param name="exampleAssistantMessage">An example response demonstrating the use case.</param>
    /// <returns>A pinned conversation entity representing the use case guide.</returns>
    private static Conversation CreateUseCaseGuide(
        string title,
        string guideSystemPrompt,
        string exampleUserMessage,
        string exampleAssistantMessage)
    {
        Guid conversationId = Guid.NewGuid();

        return new Conversation
        {
            Id = conversationId,
            Title = title,
            SystemPrompt = guideSystemPrompt,
            ModelId = "sample",
            InstanceId = Guid.Empty,
            IsPinned = true,
            Parameters = new ConversationParameters
            {
                Temperature = 0.7,
                MaxTokens = 2048,
                Logprobs = true,
                TopLogprobs = 5
            },
            TotalTokens = 0,
            LastMessageAt = DateTime.UtcNow,
            Messages =
            [
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.System,
                    Content = guideSystemPrompt,
                    SortOrder = 0
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.User,
                    Content = exampleUserMessage,
                    SortOrder = 1
                },
                new Message
                {
                    Id = Guid.NewGuid(),
                    ConversationId = conversationId,
                    Role = MessageRole.Assistant,
                    Content = exampleAssistantMessage,
                    FinishReason = "stop",
                    SortOrder = 2
                }
            ]
        };
    }

    /// <summary>
    /// Builds the system prompt for the Model Comparison use case guide.
    /// </summary>
    /// <returns>A detailed system prompt explaining how to compare models in Prism.</returns>
    private static string BuildModelComparisonSystemPrompt()
    {
        return """
            ## Model Comparison Guide

            This guide demonstrates how to compare different language models side by side in Prism.

            ### Steps to Compare Models

            1. **Open two Playground tabs** — Use the multi-pane layout to open two conversations side by side.
            2. **Set the same system prompt** — Copy the system prompt to both conversations so the only variable is the model.
            3. **Select different models** — Choose a different model for each conversation from the model selector.
            4. **Send the same prompt** — Type the same user message in both conversations.
            5. **Compare the results** — Look at:
               - **Response quality**: Which answer is more accurate and complete?
               - **Logprobs**: Which model is more confident? Lower perplexity = higher confidence.
               - **Latency**: Which model responds faster (check TTFT and total latency)?
               - **Token efficiency**: Which model uses fewer tokens for a comparable answer?

            ### What to Look For

            - **Perplexity differences** indicate how "surprised" each model is by its own output.
              A significantly higher perplexity may signal the model is less certain or hallucinating.
            - **Token probability patterns** in the logprobs panel reveal where models diverge.
              Look for tokens where one model assigns high probability but the other does not.
            - **Consistency**: Send the same prompt multiple times with temperature > 0 to check
              how consistent each model's answers are.

            ### Tips

            - Start with factual questions where you know the correct answer.
            - Try both simple and complex prompts to see where models differ.
            - Use the Experiment Tracker (Phase 2) for systematic comparisons.
            """;
    }

    /// <summary>
    /// Builds the system prompt for the Hallucination Detection use case guide.
    /// </summary>
    /// <returns>A detailed system prompt explaining how to detect hallucinations using logprobs.</returns>
    private static string BuildHallucinationDetectionSystemPrompt()
    {
        return """
            ## Hallucination Detection Guide

            This guide shows how to use logprobs and token probabilities to detect potential hallucinations
            in language model outputs.

            ### What Are Hallucinations?

            Hallucinations occur when a model generates confident-sounding text that is factually incorrect,
            fabricated, or unsupported by its training data. They are a key challenge in AI research.

            ### How Logprobs Help

            Log probabilities reveal the model's internal confidence for each generated token:

            - **High confidence (logprob close to 0)**: The model strongly predicts this token.
              Example: "The capital of France is" → "Paris" (logprob ~ -0.02)
            - **Low confidence (very negative logprob)**: The model is uncertain.
              Example: "The inventor of the telephone was" → "Bell" (logprob ~ -1.5)
              This uncertainty may indicate the model is guessing.

            ### Detection Strategies

            1. **Watch for perplexity spikes** — If a response has abnormally high perplexity compared
               to similar factual questions, the model may be confabulating.
            2. **Inspect token-level logprobs** — Click on individual tokens in the logprobs panel.
               Look for named entities (people, dates, numbers) with low probability.
            3. **Check alternative tokens** — The top-K alternatives panel shows what other tokens
               the model considered. If the top alternatives are very different (e.g., different names
               or numbers), the model is uncertain about the fact.
            4. **Compare across models** — If two models give different answers with similar confidence,
               at least one is likely hallucinating.
            5. **Ask for sources** — Follow up by asking the model to cite sources. A model that
               hallucinated often cannot provide valid references.

            ### Red Flags

            - Specific dates, statistics, or quotes with low token probability
            - Named entities (people, places, organizations) with high top-K entropy
            - Perplexity that is 2x or more above the conversation average
            - Confident tone paired with low logprob values

            ### Exercise

            Try asking the model about obscure or very recent topics. Compare the logprobs for
            well-known facts vs. obscure claims. You should see clear differences in confidence.
            """;
    }

    /// <summary>
    /// Builds realistic logprobs data for the response "The capital of France is Paris."
    /// </summary>
    /// <returns>Logprobs data with per-token probabilities and top-K alternatives.</returns>
    private static LogprobsData BuildParisLogprobs()
    {
        return new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob
                {
                    Token = "The",
                    Logprob = -0.05,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = "The", Logprob = -0.05 },
                        new TopLogprob { Token = "Paris", Logprob = -3.21 },
                        new TopLogprob { Token = "France", Logprob = -4.10 },
                        new TopLogprob { Token = "Well", Logprob = -4.85 },
                        new TopLogprob { Token = "Sure", Logprob = -5.12 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " capital",
                    Logprob = -0.12,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " capital", Logprob = -0.12 },
                        new TopLogprob { Token = " city", Logprob = -2.85 },
                        new TopLogprob { Token = " answer", Logprob = -4.32 },
                        new TopLogprob { Token = " official", Logprob = -5.01 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " of",
                    Logprob = -0.03,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " of", Logprob = -0.03 },
                        new TopLogprob { Token = " city", Logprob = -4.50 },
                        new TopLogprob { Token = " and", Logprob = -5.80 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " France",
                    Logprob = -0.08,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " France", Logprob = -0.08 },
                        new TopLogprob { Token = " the", Logprob = -3.10 },
                        new TopLogprob { Token = " that", Logprob = -4.95 },
                        new TopLogprob { Token = " this", Logprob = -5.22 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " is",
                    Logprob = -0.04,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " is", Logprob = -0.04 },
                        new TopLogprob { Token = ",", Logprob = -3.80 },
                        new TopLogprob { Token = " has", Logprob = -5.40 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " Paris",
                    Logprob = -0.02,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " Paris", Logprob = -0.02 },
                        new TopLogprob { Token = " Lyon", Logprob = -5.80 },
                        new TopLogprob { Token = " Marseille", Logprob = -6.32 },
                        new TopLogprob { Token = " Strasbourg", Logprob = -7.15 },
                        new TopLogprob { Token = " Nice", Logprob = -7.40 }
                    ]
                },
                new TokenLogprob
                {
                    Token = ".",
                    Logprob = -0.15,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = ".", Logprob = -0.15 },
                        new TopLogprob { Token = ",", Logprob = -2.10 },
                        new TopLogprob { Token = "!", Logprob = -4.50 }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Builds realistic logprobs data for the response "The capital of Germany is Berlin."
    /// </summary>
    /// <returns>Logprobs data with per-token probabilities and top-K alternatives.</returns>
    private static LogprobsData BuildBerlinLogprobs()
    {
        return new LogprobsData
        {
            Tokens =
            [
                new TokenLogprob
                {
                    Token = "The",
                    Logprob = -0.06,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = "The", Logprob = -0.06 },
                        new TopLogprob { Token = "Berlin", Logprob = -3.45 },
                        new TopLogprob { Token = "Germany", Logprob = -4.20 },
                        new TopLogprob { Token = "Sure", Logprob = -4.90 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " capital",
                    Logprob = -0.10,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " capital", Logprob = -0.10 },
                        new TopLogprob { Token = " city", Logprob = -2.92 },
                        new TopLogprob { Token = " answer", Logprob = -4.48 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " of",
                    Logprob = -0.03,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " of", Logprob = -0.03 },
                        new TopLogprob { Token = " city", Logprob = -4.62 },
                        new TopLogprob { Token = " and", Logprob = -5.70 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " Germany",
                    Logprob = -0.07,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " Germany", Logprob = -0.07 },
                        new TopLogprob { Token = " the", Logprob = -3.25 },
                        new TopLogprob { Token = " that", Logprob = -5.10 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " is",
                    Logprob = -0.05,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " is", Logprob = -0.05 },
                        new TopLogprob { Token = ",", Logprob = -3.65 },
                        new TopLogprob { Token = " has", Logprob = -5.30 }
                    ]
                },
                new TokenLogprob
                {
                    Token = " Berlin",
                    Logprob = -0.04,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = " Berlin", Logprob = -0.04 },
                        new TopLogprob { Token = " Munich", Logprob = -4.85 },
                        new TopLogprob { Token = " Hamburg", Logprob = -5.60 },
                        new TopLogprob { Token = " Frankfurt", Logprob = -6.10 },
                        new TopLogprob { Token = " Bonn", Logprob = -6.45 }
                    ]
                },
                new TokenLogprob
                {
                    Token = ".",
                    Logprob = -0.13,
                    TopLogprobs =
                    [
                        new TopLogprob { Token = ".", Logprob = -0.13 },
                        new TopLogprob { Token = ",", Logprob = -2.25 },
                        new TopLogprob { Token = "!", Logprob = -4.70 }
                    ]
                }
            ]
        };
    }
}
