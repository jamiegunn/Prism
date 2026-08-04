using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Features.Playground.Application.StreamChat;
using Prism.Features.Playground.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Covers what happens when an inference stream dies partway through a response.
/// </summary>
/// <remarks>
/// Written against the pre-fix code, where <c>streamError</c> was declared and checked but
/// never assigned. A C# iterator cannot wrap <c>yield return</c> in a try/catch, so the
/// provider's exception escaped the handler, aborted the SSE response, and discarded the
/// partial assistant message — losing both the error and the tokens already generated.
/// </remarks>
[Collection("Database")]
public sealed class StreamChatFaultTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamChatFaultTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public StreamChatFaultTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// A mid-stream transport failure must surface as a reported error rather than an
    /// exception escaping the handler and killing the response.
    /// </summary>
    [Fact]
    public async Task Stream_Failing_Midway_Reports_An_Error_Instead_Of_Throwing()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db);

        StreamChatHandler handler = CreateHandler(db, FaultingTransport());

        List<StreamChatEvent> events = await CollectAsync(handler, instanceId);

        Assert.Contains(events, e => e is ChatError);
    }

    /// <summary>
    /// Tokens delivered before the failure must be persisted. A truncated generation is
    /// evidence about model behaviour, and discarding it destroys the thing a researcher
    /// most wants to inspect after a stream dies.
    /// </summary>
    [Fact]
    public async Task Stream_Failing_Midway_Persists_The_Partial_Response()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid instanceId = await SeedInstanceAsync(db);

        StreamChatHandler handler = CreateHandler(db, FaultingTransport());

        List<StreamChatEvent> events = await CollectAsync(handler, instanceId);

        Guid conversationId = events.OfType<ChatStarted>().Single().ConversationId;

        await using AppDbContext verify = _fixture.CreateContext();
        List<Message> assistantMessages = await verify.Set<Message>()
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId && m.Role == MessageRole.Assistant)
            .ToListAsync();

        Message assistant = Assert.Single(assistantMessages);
        Assert.False(
            string.IsNullOrEmpty(assistant.Content),
            "The tokens received before the stream failed were discarded.");
        Assert.Contains("Hello", assistant.Content);
    }

    private static FakeHttpTransport FaultingTransport() =>
        FakeHttpTransport.SseThatFailsMidStream(
        [
            """data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}""",
            """data: {"id":"1","object":"chat.completion.chunk","choices":[{"index":0,"delta":{"content":" world"},"finish_reason":null}]}""",
        ]);

    private static StreamChatHandler CreateHandler(AppDbContext db, FakeHttpTransport transport)
        => new(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            new StreamChatValidator(),
            NullLogger<StreamChatHandler>.Instance);

    private static async Task<List<StreamChatEvent>> CollectAsync(
        StreamChatHandler handler, Guid instanceId)
    {
        var command = new StreamChatCommand(
            ConversationId: null,
            InstanceId: instanceId,
            SystemPrompt: null,
            UserMessage: "hello",
            Parameters: new ConversationParameters());

        List<StreamChatEvent> events = [];

        // The handler must not throw. Any exception escaping here is the bug.
        await foreach (StreamChatEvent evt in handler.HandleAsync(command, CancellationToken.None))
        {
            events.Add(evt);
        }

        return events;
    }

    private static async Task<Guid> SeedInstanceAsync(AppDbContext db)
    {
        var instance = new InferenceInstance
        {
            Name = $"fake-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            ModelId = "test-model",
            SupportsStreaming = true,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }
}
