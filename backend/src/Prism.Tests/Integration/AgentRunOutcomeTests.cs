using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prism.Common.Database;
using Prism.Common.Inference;
using Prism.Features.Agents.Application.RunAgent;
using Prism.Features.Agents.Domain;
using Prism.Features.Models.Application;
using Prism.Features.Models.Domain;
using Prism.Tests.Support;

namespace Prism.Tests.Integration;

/// <summary>
/// What an agent run records about how it ended.
/// </summary>
/// <remarks>
/// A run whose first step could not reach the inference server was stored as Completed, with no
/// output and no error message: the executor sets the step's Error and stops, and the run only
/// adopted a step's error when that step was also a final answer. The Runs list therefore showed
/// a green Completed for a run that did nothing at all.
/// </remarks>
[Collection("Database")]
public sealed class AgentRunOutcomeTests
{
    private readonly DatabaseFixture _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentRunOutcomeTests"/> class.
    /// </summary>
    /// <param name="fixture">The shared database fixture.</param>
    public AgentRunOutcomeTests(DatabaseFixture fixture) => _fixture = fixture;

    /// <summary>
    /// A run that never reached the model is a failed run, and says why.
    /// </summary>
    [Fact]
    public async Task A_Run_That_Could_Not_Reach_The_Model_Is_Recorded_As_Failed()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid workflowId = await SeedWorkflowAsync(db);

        // A server that refuses every call, which is what an instance that is not running is.
        List<AgentRunEvent> events = await RunAsync(db, workflowId, FakeHttpTransport.ServerError());

        // The finished event is what the page reads to decide what to show, so the verdict has
        // to be right there and not only in the database.
        AgentRunFinished finished = Assert.Single(events.OfType<AgentRunFinished>());
        Assert.Equal(nameof(AgentRunStatus.Failed), finished.Run.Status);
        Assert.Contains("Inference failed", finished.Run.ErrorMessage ?? "");

        await using AppDbContext verify = _fixture.CreateContext();
        AgentRun run = await verify.Set<AgentRun>()
            .AsNoTracking()
            .FirstAsync(r => r.WorkflowId == workflowId);

        Assert.Equal(AgentRunStatus.Failed, run.Status);
        Assert.False(string.IsNullOrWhiteSpace(run.ErrorMessage), "The run recorded no reason for failing.");
        Assert.Null(run.Output);
    }

    /// <summary>
    /// A run that reaches a final answer is completed, and carries the answer.
    /// </summary>
    [Fact]
    public async Task A_Run_That_Answers_Is_Recorded_As_Completed()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid workflowId = await SeedWorkflowAsync(db);

        var transport = FakeHttpTransport.ChatCompletion(
            "Thought: this is simple arithmetic.\nFinal Answer: 4");

        await RunAsync(db, workflowId, transport);

        await using AppDbContext verify = _fixture.CreateContext();
        AgentRun run = await verify.Set<AgentRun>()
            .AsNoTracking()
            .FirstAsync(r => r.WorkflowId == workflowId);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal("4", run.Output);
        Assert.Null(run.ErrorMessage);
    }

    /// <summary>
    /// The loop the whole pattern rests on: the model names a tool, the tool runs, its result
    /// is fed back as an observation, and the next turn answers from it.
    /// </summary>
    /// <remarks>
    /// Not exercisable against a real local model — a 7B asked to multiply announced it would
    /// use the calculator, skipped it, and answered 1061 for 47 × 23. That is the model being
    /// weak, and it means a live run proves nothing about whether the tool loop works.
    /// </remarks>
    [Fact]
    public async Task A_Tool_Named_By_The_Model_Runs_And_Its_Result_Comes_Back_As_An_Observation()
    {
        await using AppDbContext db = _fixture.CreateContext();
        Guid workflowId = await SeedWorkflowAsync(db, tools: ["calculator"]);

        var transport = FakeHttpTransport.ChatCompletions(
            "Thought: I should multiply these.\nAction: calculator\nAction Input: 47 * 23",
            "Thought: the tool gave me the product.\nFinal Answer: 1081");

        var registry = new Features.Agents.Domain.Tools.AgentToolRegistry();
        registry.Register(new Features.Agents.Domain.Tools.CalculatorTool());

        await RunAsync(db, workflowId, transport, registry);

        await using AppDbContext verify = _fixture.CreateContext();
        AgentRun run = await verify.Set<AgentRun>()
            .AsNoTracking()
            .FirstAsync(r => r.WorkflowId == workflowId);

        Assert.Equal(AgentRunStatus.Completed, run.Status);
        Assert.Equal("1081", run.Output);

        List<AgentStep> steps = JsonSerializer.Deserialize<List<AgentStep>>(run.StepsJson)!;
        Assert.Equal("calculator", steps[0].Action);
        Assert.Equal("47 * 23", steps[0].ActionInput);
        Assert.Contains("1081", steps[0].Observation ?? "");

        // The observation has to reach the model, or the second turn is answering from nothing.
        Assert.Contains("1081", transport.RequestBodies[1]);
    }

    private static async Task<List<AgentRunEvent>> RunAsync(
        AppDbContext db,
        Guid workflowId,
        FakeHttpTransport transport,
        Features.Agents.Domain.Tools.AgentToolRegistry? registry = null)
    {
        var handler = new RunAgentHandler(
            db,
            new InferenceProviderFactory(transport, NullLoggerFactory.Instance),
            new Features.Agents.Application.ReActExecutor(
                NullLogger<Features.Agents.Application.ReActExecutor>.Instance),
            registry ?? new Features.Agents.Domain.Tools.AgentToolRegistry(),
            NullLogger<RunAgentHandler>.Instance);

        List<AgentRunEvent> events = [];

        await foreach (AgentRunEvent evt in handler.HandleAsync(
            new RunAgentCommand(workflowId, "What is 2+2?"), CancellationToken.None))
        {
            events.Add(evt);
        }

        return events;
    }

    private static async Task<Guid> SeedWorkflowAsync(AppDbContext db, string[]? tools = null)
    {
        var instance = new InferenceInstance
        {
            Name = $"agent-target-{Guid.NewGuid():N}",
            Endpoint = "http://localhost:9999",
            ProviderType = InferenceProviderType.OpenAiCompatible,
            ModelId = "test-model",
        };
        db.Set<InferenceInstance>().Add(instance);

        var workflow = new AgentWorkflow
        {
            Name = $"test-agent-{Guid.NewGuid():N}",
            SystemPrompt = "You are a test agent.",
            Model = "test-model",
            InstanceId = instance.Id,
            Pattern = AgentPatternType.ReAct,
            MaxSteps = 3,
            TokenBudget = 1000,
            Temperature = 0.0,
            EnabledTools = tools is null ? [] : [.. tools],
        };
        db.Set<AgentWorkflow>().Add(workflow);

        await db.SaveChangesAsync();
        return workflow.Id;
    }
}
