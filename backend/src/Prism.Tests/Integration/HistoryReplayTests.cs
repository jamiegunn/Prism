using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Common.Results;
using Prism.Features.History.Application.Dtos;
using Prism.Features.History.Application.ReplaySingle;
using Prism.Features.History.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// Proofs for replay — re-running a recorded request against a chosen instance.
/// </summary>
/// <remarks>
/// Written against a replay that no screen could display: the result carried the original as a
/// structured record where the client's type declared a string, so the comparison threw on
/// every successful replay. Alongside it, the overrides were unvalidated (a temperature of 99
/// and a negative token budget both reached the provider) and the model was resolved from the
/// target instance before the record, so replaying a run could silently change the model the
/// comparison was about.
/// </remarks>
[Collection("Database")]
public sealed class HistoryReplayTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryReplayTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public HistoryReplayTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// The replay result carries the original response as text, so the two sides of the
    /// comparison are the same kind of thing and a client can put them side by side.
    /// </summary>
    [Fact]
    public async Task Result_Carries_The_Original_Response_As_Text()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer");
        Guid instanceId = await SeedInstanceAsync(db);

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("replay answer"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("original answer", result.Value.OriginalResponseContent);
        Assert.Equal("replay answer", result.Value.ReplayResponseContent);
    }

    /// <summary>
    /// A record whose call failed has no original text, and the result says so with null rather
    /// than an empty string that would read as "the model answered with nothing".
    /// </summary>
    [Fact]
    public async Task A_Failed_Original_Reports_No_Response_Rather_Than_An_Empty_One()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, originalContent: null);
        Guid instanceId = await SeedInstanceAsync(db);

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("replay answer"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Null(result.Value.OriginalResponseContent);
        Assert.Contains("no response", result.Value.DiffSummary);
    }

    /// <summary>
    /// The model on the wire is the one the record was made with, not the one the target
    /// instance happens to report. Substituting it silently would make the two responses
    /// unequal for a reason the comparison never states.
    /// </summary>
    [Fact]
    public async Task Replay_Runs_The_Model_The_Record_Names_Not_The_Instances()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", model: "recorded-model");
        Guid instanceId = await SeedInstanceAsync(db, instanceModel: "instance-model");

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("recorded-model", result.Value.ReplayModel);
        Assert.Contains("recorded-model", Assert.Single(transport.RequestBodies));
    }

    /// <summary>
    /// An explicit model override still wins — that is how a replay deliberately compares one
    /// model against another.
    /// </summary>
    [Fact]
    public async Task An_Explicit_Model_Override_Wins()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", model: "recorded-model");
        Guid instanceId = await SeedInstanceAsync(db, instanceModel: "instance-model");

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId, OverrideModel: "chosen-model"),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("chosen-model", result.Value.ReplayModel);
    }

    /// <summary>
    /// Overrides that are given travel to the provider; ones that are not are taken from the
    /// recorded request unchanged. A replay that quietly dropped an override would be reported
    /// as a comparison of settings it never ran.
    /// </summary>
    [Fact]
    public async Task Given_Overrides_Reach_The_Provider_And_Absent_Ones_Keep_The_Original()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", temperature: 0.1, maxTokens: 64);
        Guid instanceId = await SeedInstanceAsync(db);

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId, OverrideTemperature: 1.5),
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        using JsonDocument sent = JsonDocument.Parse(Assert.Single(transport.RequestBodies));
        Assert.Equal(1.5, sent.RootElement.GetProperty("temperature").GetDouble());
        Assert.Equal(64, sent.RootElement.GetProperty("max_tokens").GetInt32());
    }

    /// <summary>
    /// Out-of-range overrides are rejected before any provider is contacted. Sent onward they
    /// were either silently ignored — a replay that did not do what was asked — or refused by
    /// the provider, which the API then reported as a 503 as though inference were down.
    /// </summary>
    /// <param name="temperature">The temperature override under test.</param>
    /// <param name="topP">The top-P override under test.</param>
    /// <param name="maxTokens">The max-tokens override under test.</param>
    [Theory]
    [InlineData(99.0, null, null)]
    [InlineData(-1.0, null, null)]
    [InlineData(null, 50.0, null)]
    [InlineData(null, null, 0)]
    [InlineData(null, null, -5)]
    public async Task Out_Of_Range_Overrides_Are_Rejected_Without_Calling_The_Provider(
        double? temperature, double? topP, int? maxTokens)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer");
        Guid instanceId = await SeedInstanceAsync(db);

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(
                recordId,
                instanceId,
                OverrideTemperature: temperature,
                OverrideMaxTokens: maxTokens,
                OverrideTopP: topP),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(transport.RequestBodies);
    }

    /// <summary>
    /// A missing target instance is a bad request rather than a missing replay: the empty GUID
    /// arrives when a client omits the field, and reporting it as "instance 00000000-… was not
    /// found" sends the reader looking for a record that never existed.
    /// </summary>
    [Fact]
    public async Task An_Omitted_Instance_Is_Reported_As_A_Missing_Field()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer");

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("x"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, Guid.Empty), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
    }

    /// <summary>
    /// A record with no recorded messages cannot be replayed, and saying so beats sending an
    /// empty conversation to the provider and relaying whatever it says about it.
    /// </summary>
    [Fact]
    public async Task A_Record_With_No_Messages_Cannot_Be_Replayed()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", requestJson: "{}");
        Guid instanceId = await SeedInstanceAsync(db);

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(transport.RequestBodies);
    }

    /// <summary>
    /// A stored request whose message list is the JSON literal <c>null</c> is refused like any
    /// other record with nothing to replay.
    /// </summary>
    /// <remarks>
    /// Found by attacking the fix: the guard read <c>Messages.Count</c>, and deserializing
    /// <c>"messages": null</c> replaces the initialised list with null rather than leaving it
    /// empty. The result was a null reference escaping to the middleware as a 500 — the server
    /// reporting its own fault for data it had stored.
    /// </remarks>
    [Fact]
    public async Task A_Record_Whose_Messages_Are_Null_Is_Refused_Not_Crashed()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(
            db, "original answer", requestJson: """{"model":"recorded-model","messages":null}""");
        Guid instanceId = await SeedInstanceAsync(db);

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Empty(transport.RequestBodies);
    }

    /// <summary>
    /// When the provider refuses the call, the failure names the model and the instance — the
    /// two facts that identify the usual cause, a record whose model that instance does not serve.
    /// </summary>
    [Fact]
    public async Task A_Provider_Failure_Names_The_Model_And_The_Instance()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", model: "recorded-model");
        Guid instanceId = await SeedInstanceAsync(db, name: "target-instance");

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ServerError());

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("recorded-model", result.Error.Message);
        Assert.Contains("target-instance", result.Error.Message);
    }

    /// <summary>
    /// The reported usage is the replay's, not the original's. Echoing the original back would
    /// make every comparison agree with itself and hide exactly the drift replay exists to find.
    /// </summary>
    [Fact]
    public async Task Usage_And_Latency_Describe_The_Replay_Not_The_Original()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer", promptTokens: 999, completionTokens: 888);
        Guid instanceId = await SeedInstanceAsync(db);

        // FakeHttpTransport.ChatCompletion reports 5 prompt and 3 completion tokens.
        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("replay answer"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal(5, result.Value.ReplayPromptTokens);
        Assert.Equal(3, result.Value.ReplayCompletionTokens);
    }

    /// <summary>
    /// A recorded streaming call replays as a single non-streamed request, because the comparison
    /// needs one complete response rather than a sequence of fragments.
    /// </summary>
    [Fact]
    public async Task A_Streaming_Record_Replays_Without_Streaming()
    {
        await using AppDbContext db = _fixture.CreateContext();
        string streamingRequest = JsonSerializer.Serialize(new
        {
            model = "recorded-model",
            messages = new[] { new { role = "user", content = "hello" } },
            stream = true,
        });
        Guid recordId = await SeedRecordAsync(db, "original answer", requestJson: streamingRequest);
        Guid instanceId = await SeedInstanceAsync(db);

        var transport = FakeHttpTransport.ChatCompletion("replay answer");
        ReplaySingleHandler handler = CreateHandler(db, transport);

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);

        using JsonDocument sent = JsonDocument.Parse(Assert.Single(transport.RequestBodies));
        Assert.False(sent.RootElement.GetProperty("stream").GetBoolean());
    }

    /// <summary>
    /// A provider that answers 200 with a body the client cannot read is a failure, not an empty
    /// success. Reporting it as a replay that returned nothing would invent a result.
    /// </summary>
    /// <param name="body">The unusable body the provider returns.</param>
    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    public async Task An_Unreadable_Provider_Response_Is_A_Failure(string body)
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "original answer");
        Guid instanceId = await SeedInstanceAsync(db);

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.Json(body));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsFailure, "An unreadable provider response was reported as a replay.");
    }

    /// <summary>
    /// The character counts in the summary are counts of text, not of bytes or UTF-16 units gone
    /// wrong. A researcher reads that number as "how much did the answer change".
    /// </summary>
    [Fact]
    public async Task The_Summary_Counts_Unicode_Content_As_Text()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "Παρίσι");
        Guid instanceId = await SeedInstanceAsync(db);

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("Παρίσι, γεια"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("Παρίσι", result.Value.OriginalResponseContent);
        Assert.Contains("6 vs 12", result.Value.DiffSummary);
    }

    /// <summary>
    /// Two identical responses are reported as identical rather than as a zero-length difference.
    /// </summary>
    [Fact]
    public async Task Identical_Responses_Are_Reported_As_Identical()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid recordId = await SeedRecordAsync(db, "the same answer");
        Guid instanceId = await SeedInstanceAsync(db);

        ReplaySingleHandler handler = CreateHandler(db, FakeHttpTransport.ChatCompletion("the same answer"));

        Result<ReplayResultDto> result = await handler.HandleAsync(
            new ReplaySingleCommand(recordId, instanceId), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Message : null);
        Assert.Equal("Responses are identical", result.Value.DiffSummary);
    }

    private static ReplaySingleHandler CreateHandler(AppDbContext db, FakeHttpTransport transport)
        => new(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            new ReplaySingleValidator(),
            NullLogger<ReplaySingleHandler>.Instance);

    private static async Task<Guid> SeedRecordAsync(
        AppDbContext db,
        string? originalContent,
        string model = "recorded-model",
        double? temperature = null,
        int? maxTokens = null,
        string? requestJson = null,
        int promptTokens = 0,
        int completionTokens = 0)
    {
        var record = new InferenceRecord
        {
            SourceModule = "replay-test",
            ProviderName = "p",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            ProviderEndpoint = "http://localhost:9999",
            Model = model,
            RequestJson = requestJson ?? JsonSerializer.Serialize(new
            {
                model,
                messages = new[] { new { role = "user", content = "what is the capital of France?" } },
                temperature,
                maxTokens,
            }),
            ResponseJson = originalContent is null
                ? null
                : JsonSerializer.Serialize(new { content = originalContent }),
            PromptTokens = promptTokens,
            CompletionTokens = completionTokens,
            TotalTokens = promptTokens + completionTokens,
            IsSuccess = originalContent is not null,
            ErrorMessage = originalContent is null ? "the call failed" : null,
            Tags = [],
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };

        db.Add(record);
        await db.SaveChangesAsync();
        return record.Id;
    }

    private static async Task<Guid> SeedInstanceAsync(
        AppDbContext db, string? instanceModel = "instance-model", string? name = null)
    {
        var instance = new InferenceInstance
        {
            Name = name ?? $"replay-target-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            ModelId = instanceModel,
        };

        db.Set<InferenceInstance>().Add(instance);
        await db.SaveChangesAsync();
        return instance.Id;
    }
}
