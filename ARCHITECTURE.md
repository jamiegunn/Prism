# AI Research Workbench — Architecture

**Target Framework:** .NET 9 | **API Style:** Minimal API | **Decisions Log:** See `/docs/ADR/`

## Guiding Principles

1. **Vertical Slice Architecture** — Code is organized by feature, not by technical layer. Each feature is a self-contained slice that owns its endpoints, use cases, domain models, and data access.
2. **Clean Architecture per Slice** — Within each feature slice, code follows Clean Architecture conventions: Domain (entities, value objects) -> Application (use cases, DTOs, validators) -> Infrastructure (data access, external services) -> Api (endpoints, request/response contracts).
3. **Result Pattern** — All application-layer operations return `Result<T>` instead of throwing exceptions. Errors are values, not control flow.
4. **Provider Abstraction** — Every external dependency is behind an interface: inference (`IInferenceProvider`), database (`DbContext` via EF Core), vector store (`IVectorStore`), cache (`ICacheService`), storage (`IFileStorage`), auth (`IAuthProvider`), logging (`ILogger` via Serilog). Swap any implementation without touching feature code.
5. **XML Documentation** — All public types, methods, and interfaces carry `<summary>`, `<param>`, `<returns>`, and `<example>` XML doc comments. No exceptions.
6. **Observable by Default** — Structured logging, correlation IDs, and performance metrics on every request. You should never have to guess what happened.

---

## Why .NET 9 Minimal API (Not Controllers)

.NET 9 Minimal APIs have **full parity** with MVC controllers. There is no capability gap:

| Capability | Minimal API (.NET 9) | How |
|------------|---------------------|-----|
| **Middleware** | Full pipeline support | `app.UseMiddleware<T>()` — same as MVC |
| **Endpoint filters** | Equivalent to action filters | `AddEndpointFilter<T>()` on route groups or individual endpoints |
| **Authorization** | Full policy-based auth | `.RequireAuthorization("PolicyName")` per endpoint or group |
| **Rate limiting** | Built-in | `.RequireRateLimiting("PolicyName")` |
| **Output caching** | Built-in | `.CacheOutput("PolicyName")` |
| **Validation** | Via endpoint filters | FluentValidation + custom `ValidationFilter<T>` |
| **OpenAPI / Swagger** | Native (.NET 9) | Built-in OpenAPI document generation, no Swashbuckle needed |
| **Route groups** | Equivalent to controller grouping | `app.MapGroup("/api/v1/playground")` with shared filters, auth, rate limiting |
| **Model binding** | Explicit parameters | `[FromBody]`, `[FromQuery]`, `[FromRoute]`, `[FromServices]` — more explicit, less magic |
| **Dependency injection** | Full support | Parameters resolved from DI automatically |
| **Response types** | `TypedResults` | Compile-time safe: `TypedResults.Ok(dto)`, `TypedResults.NotFound()` |

**Why Minimal API over Controllers:**
- Less ceremony (no controller class, no `[ApiController]` attribute, no `[HttpGet]` decorators)
- Route groups replace controller base routes — with the same filter/auth/caching features
- Better alignment with vertical slices (each feature registers its own route group)
- `TypedResults` gives compile-time safety on response types
- Native OpenAPI in .NET 9 eliminates the Swashbuckle dependency

### Endpoint Filter Example (equivalent to Action Filter)

```csharp
/// <summary>
/// Validates the request body using FluentValidation before the handler executes.
/// Equivalent to a validation action filter in MVC controllers.
/// Returns 400 Bad Request with validation errors if validation fails.
/// </summary>
public sealed class ValidationFilter<T> : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null) return await next(context);

        var arg = context.Arguments.OfType<T>().FirstOrDefault();
        if (arg is null) return await next(context);

        var result = await validator.ValidateAsync(arg);
        if (!result.IsValid)
            return TypedResults.ValidationProblem(result.ToDictionary());

        return await next(context);
    }
}

// Usage on a route group — all endpoints in the group get validation
var playground = app.MapGroup("/api/v1/playground")
    .AddEndpointFilter<ValidationFilter<SendMessageRequest>>()
    .RequireAuthorization()
    .WithTags("Playground");
```

---

## Backend Project Structure

```
backend/
├── src/
│   ├── AiResearch.Api/                        # Startup, middleware, DI composition root
│   │   ├── Program.cs                         # Host builder, service registration, pipeline
│   │   ├── Middleware/
│   │   │   ├── CorrelationIdMiddleware.cs      # Attaches X-Correlation-Id to every request
│   │   │   ├── RequestLoggingMiddleware.cs     # Structured log: method, path, status, duration
│   │   │   ├── GlobalExceptionMiddleware.cs    # Catches unhandled exceptions -> ProblemDetails
│   │   │   └── ResultToResponseMiddleware.cs   # Maps Result<T> to HTTP status codes
│   │   ├── Extensions/
│   │   │   ├── ServiceCollectionExtensions.cs  # DI registration helpers per layer
│   │   │   └── WebApplicationExtensions.cs     # Middleware pipeline builder
│   │   └── appsettings*.json
│   │
│   ├── AiResearch.Common/                     # Shared kernel — no feature dependencies
│   │   ├── Results/
│   │   │   ├── Result.cs                       # Result<T> and Result (non-generic)
│   │   │   ├── Error.cs                        # Error value type (code, message, type)
│   │   │   └── ResultExtensions.cs             # Map, Bind, Match, ToActionResult helpers
│   │   ├── Abstractions/
│   │   │   ├── IRepository.cs                  # Generic repository interface
│   │   │   ├── IUnitOfWork.cs                  # Transaction boundary
│   │   │   ├── IAuditableEntity.cs             # CreatedAt, UpdatedAt, UserId?
│   │   │   ├── IPagedRequest.cs                # Page, PageSize, SortBy, SortOrder
│   │   │   └── PagedResult.cs                  # Items, TotalCount, Page, PageSize
│   │   ├── Auth/                                # Authentication abstraction (ADR-005)
│   │   │   ├── IAuthProvider.cs                 # Core auth interface
│   │   │   ├── ICurrentUser.cs                  # Scoped service: who is the current user?
│   │   │   ├── AuthResult.cs                    # Auth-specific Result wrapper
│   │   │   ├── UserInfo.cs                      # Canonical user representation
│   │   │   ├── Providers/
│   │   │   │   ├── LocalAuthProvider.cs         # Username/password, JWT (Phase 1)
│   │   │   │   ├── EntraAuthProvider.cs         # Microsoft Entra ID / Azure AD (future)
│   │   │   │   ├── OidcAuthProvider.cs          # Generic OpenID Connect (future)
│   │   │   │   └── NoAuthProvider.cs            # Bypass: all requests are "local-user" (dev mode)
│   │   │   └── Middleware/
│   │   │       └── AuthenticationMiddleware.cs  # Resolves ICurrentUser from token/session
│   │   ├── Database/                             # Database abstraction (ADR-008)
│   │   │   ├── AppDbContext.cs                  # EF Core DbContext — single context, assembly-scanned configs
│   │   │   ├── BaseEntity.cs                    # Id, CreatedAt, UpdatedAt
│   │   │   ├── Interceptors/
│   │   │   │   ├── AuditInterceptor.cs          # Auto-set CreatedAt/UpdatedAt
│   │   │   │   └── SoftDeleteInterceptor.cs     # IsDeleted flag instead of hard delete
│   │   │   ├── Conventions/
│   │   │   │   └── CommonConventions.cs         # snake_case naming, UTC dates
│   │   │   ├── Configurations/                  # Shared EF configurations (e.g., JSON columns)
│   │   │   ├── Seeders/
│   │   │   │   ├── IDataSeeder.cs               # Seeder interface
│   │   │   │   ├── UseCaseSeeder.cs             # Built-in use cases + sample data
│   │   │   │   └── SeedDataRunner.cs            # Runs all seeders on first launch
│   │   │   └── Migrations/
│   │   ├── Cache/                               # Cache abstraction (ADR-003)
│   │   │   ├── ICacheService.cs                 # Get, Set, Remove, GetOrSet, GetOrSetAsync
│   │   │   ├── CacheOptions.cs                  # TTL, sliding expiration, tags, region
│   │   │   ├── Providers/
│   │   │   │   ├── InMemoryCacheService.cs      # IMemoryCache implementation (Phase 1-2)
│   │   │   │   ├── RedisCacheService.cs         # StackExchange.Redis (Phase 3+)
│   │   │   │   └── NullCacheService.cs          # No-op for testing / disabled caching
│   │   │   └── CacheKeyBuilder.cs               # Consistent key generation with namespacing
│   │   ├── Storage/                             # File storage abstraction (ADR-004)
│   │   │   ├── IFileStorage.cs                  # Store, Retrieve, Delete, List, Exists, GetMetadata
│   │   │   ├── StoragePath.cs                   # Value object: container/path normalization
│   │   │   ├── FileMetadata.cs                  # Size, content type, created, modified, etag
│   │   │   ├── Providers/
│   │   │   │   ├── LocalFileStorage.cs          # Local filesystem (Phase 1)
│   │   │   │   ├── AzureBlobStorage.cs          # Azure Blob Storage (future)
│   │   │   │   ├── S3Storage.cs                 # AWS S3 / MinIO (future)
│   │   │   │   └── NullFileStorage.cs           # No-op for testing
│   │   │   └── StorageConfiguration.cs          # Base path, max file size, allowed types
│   │   ├── VectorStore/                         # Vector store abstraction (ADR-009)
│   │   │   ├── IVectorStore.cs                  # Search, upsert, delete, collection management
│   │   │   ├── VectorRecord.cs                  # Id + embedding + metadata
│   │   │   ├── VectorSearchResult.cs            # Id + score + metadata
│   │   │   ├── VectorFilter.cs                  # Metadata filter for search
│   │   │   ├── Providers/
│   │   │   │   ├── PgVectorStore.cs             # pgvector on existing Postgres (default)
│   │   │   │   ├── QdrantVectorStore.cs         # Qdrant (future)
│   │   │   │   └── PineconeVectorStore.cs       # Pinecone (future)
│   │   │   └── VectorStoreConfiguration.cs      # Provider selection, HNSW params
│   │   ├── Search/
│   │   │   ├── IGlobalSearch.cs                 # Full-text search across all entities
│   │   │   ├── ISearchable.cs                   # Entities opt-in to search indexing
│   │   │   └── PostgresGlobalSearch.cs          # tsvector-based implementation
│   │   ├── Annotations/
│   │   │   ├── Annotation.cs                    # Label, rating, notes, tags on any entity
│   │   │   └── AnnotationService.cs             # CRUD + aggregation
│   │   ├── Export/
│   │   │   ├── IExportService.cs                # Export to CSV, JSON, LaTeX, HTML
│   │   │   ├── ExportFormat.cs                  # Enum: Csv, Json, Jsonl, LaTeX, Svg, Png, Html
│   │   │   └── StaticReportGenerator.cs         # Single-file HTML report builder
│   │   ├── UseCases/
│   │   │   ├── UseCase.cs                       # Guided research workflow
│   │   │   ├── UseCaseStep.cs                   # Individual step in a use case
│   │   │   └── UseCaseService.cs                # CRUD + progress tracking
│   │   ├── Inference/
│   │   │   ├── IInferenceProvider.cs            # THE core abstraction (see below)
│   │   │   ├── IHotReloadableProvider.cs        # Extended interface for model hot-swap
│   │   │   ├── IInferenceProviderFactory.cs     # Resolve provider by instance ID or type
│   │   │   ├── RateLimitedInferenceProvider.cs  # Concurrency limiter decorator
│   │   │   ├── EnvironmentSnapshot.cs           # Provider version, GPU, quantization metadata
│   │   │   ├── InferenceProviderType.cs         # Enum: Vllm, Ollama, LmStudio, OpenAiCompatible
│   │   │   ├── Models/
│   │   │   │   ├── ChatRequest.cs               # Provider-agnostic chat request
│   │   │   │   ├── ChatResponse.cs              # Provider-agnostic chat response
│   │   │   │   ├── StreamChunk.cs               # Single SSE token chunk
│   │   │   │   ├── LogprobsData.cs              # Per-token logprobs structure
│   │   │   │   ├── TokenPrediction.cs           # Next-token prediction result
│   │   │   │   ├── ModelInfo.cs                 # Model metadata
│   │   │   │   └── ProviderCapabilities.cs      # What this provider supports
│   │   │   ├── Providers/
│   │   │   │   ├── VllmProvider.cs              # vLLM OpenAI-compatible API
│   │   │   │   ├── OllamaProvider.cs            # Ollama REST API
│   │   │   │   ├── LmStudioProvider.cs          # LM Studio OpenAI-compatible API
│   │   │   │   └── OpenAiCompatibleProvider.cs  # Generic OpenAI-compatible base
│   │   │   └── Metrics/
│   │   │       ├── LogprobsCalculator.cs        # Perplexity, entropy, surprise from logprobs
│   │   │       └── CostCalculator.cs            # Token -> cost estimation per model
│   │   ├── Jobs/
│   │   │   ├── IJobQueue.cs                     # Enqueue, Dequeue, Status
│   │   │   ├── InMemoryJobQueue.cs              # Channel<T> implementation (Phase 1-2)
│   │   │   └── RedisJobQueue.cs                 # Redis implementation (Phase 3+)
│   │   ├── Logging/
│   │   │   ├── LoggingConstants.cs              # Structured log property names
│   │   │   └── SerilogConfiguration.cs          # Console + file sinks, enrichers
│   │   └── Extensions/
│   │       ├── JsonExtensions.cs                # Serialize/deserialize helpers
│   │       └── StringExtensions.cs              # Common string utilities
│   │
│   ├── AiResearch.Features/                   # ALL feature slices live here
│   │   │
│   │   ├── Playground/                         # Feature: Inference Playground
│   │   │   ├── Domain/
│   │   │   │   ├── Conversation.cs             # Aggregate root
│   │   │   │   ├── Message.cs                  # Entity
│   │   │   │   └── ConversationParameters.cs   # Value object
│   │   │   ├── Application/
│   │   │   │   ├── SendMessage/
│   │   │   │   │   ├── SendMessageCommand.cs       # CQRS command
│   │   │   │   │   ├── SendMessageHandler.cs       # Use case logic
│   │   │   │   │   └── SendMessageValidator.cs     # FluentValidation
│   │   │   │   ├── GetConversation/
│   │   │   │   │   ├── GetConversationQuery.cs
│   │   │   │   │   └── GetConversationHandler.cs
│   │   │   │   ├── ListConversations/
│   │   │   │   │   ├── ListConversationsQuery.cs
│   │   │   │   │   └── ListConversationsHandler.cs
│   │   │   │   ├── StreamChat/
│   │   │   │   │   ├── StreamChatCommand.cs
│   │   │   │   │   └── StreamChatHandler.cs
│   │   │   │   └── Dtos/
│   │   │   │       ├── ConversationDto.cs
│   │   │   │       └── MessageDto.cs
│   │   │   ├── Infrastructure/
│   │   │   │   └── PlaygroundRepository.cs
│   │   │   └── Api/
│   │   │       ├── PlaygroundEndpoints.cs      # MapGroup("/api/v1/playground")
│   │   │       ├── Requests/
│   │   │       │   └── SendMessageRequest.cs
│   │   │       └── Responses/
│   │   │           └── ChatStreamResponse.cs
│   │   │
│   │   ├── TokenExplorer/                      # Feature: Next-Token Prediction
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── PredictNextToken/
│   │   │   │   ├── StepThroughGeneration/
│   │   │   │   └── ExploreBranch/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── TokenExplorerEndpoints.cs
│   │   │
│   │   ├── Prompts/                            # Feature: Prompt Engineering Lab
│   │   │   ├── Domain/
│   │   │   │   ├── PromptTemplate.cs
│   │   │   │   ├── PromptVersion.cs
│   │   │   │   └── TemplateVariable.cs
│   │   │   ├── Application/
│   │   │   │   ├── CreatePrompt/
│   │   │   │   ├── CreateVersion/
│   │   │   │   ├── RenderTemplate/
│   │   │   │   ├── RunAbTest/
│   │   │   │   └── DiffVersions/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── PromptEndpoints.cs
│   │   │
│   │   ├── Experiments/                        # Feature: Experiment Tracker
│   │   │   ├── Domain/
│   │   │   │   ├── Project.cs
│   │   │   │   ├── Experiment.cs
│   │   │   │   └── Run.cs
│   │   │   ├── Application/
│   │   │   │   ├── CreateRun/
│   │   │   │   ├── CompareRuns/
│   │   │   │   ├── SearchRuns/
│   │   │   │   └── GetRunStatistics/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── ExperimentEndpoints.cs
│   │   │
│   │   ├── Datasets/                           # Feature: Dataset Manager
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── UploadDataset/
│   │   │   │   ├── BrowseRecords/
│   │   │   │   ├── SplitDataset/
│   │   │   │   ├── ComputeStatistics/
│   │   │   │   └── ExportDataset/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── DatasetEndpoints.cs
│   │   │
│   │   ├── Evaluation/                         # Feature: Evaluation Suite
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── StartEvaluation/
│   │   │   │   ├── ScoreMethods/              # Each scorer as a strategy
│   │   │   │   │   ├── IScoringMethod.cs
│   │   │   │   │   ├── ExactMatchScorer.cs
│   │   │   │   │   ├── RougeLScorer.cs
│   │   │   │   │   ├── LlmJudgeScorer.cs
│   │   │   │   │   └── PerplexityScorer.cs
│   │   │   │   └── GetLeaderboard/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── EvaluationEndpoints.cs
│   │   │
│   │   ├── Rag/                                # Feature: RAG Workbench
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── IngestDocuments/
│   │   │   │   ├── ChunkStrategies/           # Each strategy as a strategy pattern
│   │   │   │   │   ├── IChunkingStrategy.cs
│   │   │   │   │   ├── FixedChunker.cs
│   │   │   │   │   ├── SentenceChunker.cs
│   │   │   │   │   ├── RecursiveChunker.cs
│   │   │   │   │   └── SemanticChunker.cs
│   │   │   │   ├── QueryCollection/
│   │   │   │   └── RunRagPipeline/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── RagEndpoints.cs
│   │   │
│   │   ├── Agents/                             # Feature: Agent Builder
│   │   │   ├── Domain/
│   │   │   │   ├── AgentWorkflow.cs
│   │   │   │   ├── AgentRun.cs
│   │   │   │   └── ExecutionStep.cs
│   │   │   ├── Application/
│   │   │   │   ├── Patterns/                   # Agent reasoning patterns
│   │   │   │   │   ├── IAgentPattern.cs
│   │   │   │   │   ├── ReActPattern.cs
│   │   │   │   │   ├── PlanAndExecutePattern.cs
│   │   │   │   │   ├── CritiqueRevisePattern.cs
│   │   │   │   │   └── ObserveHypothesizeTestPattern.cs
│   │   │   │   ├── ExecuteAgent/
│   │   │   │   └── GetTrace/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── AgentEndpoints.cs
│   │   │
│   │   ├── History/                             # Feature: Inference History & Replay
│   │   │   ├── Domain/
│   │   │   │   ├── InferenceRecord.cs          # Immutable record of every inference call
│   │   │   │   ├── ReplaySession.cs            # A replay run (single, batch, or group)
│   │   │   │   └── ReplayResult.cs             # Original vs replay comparison
│   │   │   ├── Application/
│   │   │   │   ├── RecordInference/            # Auto-capture middleware hook
│   │   │   │   ├── SearchHistory/              # Browse, filter, tag history
│   │   │   │   ├── ReplaySingle/               # Replay one record
│   │   │   │   ├── ReplayBatch/                # Replay all or a filtered set
│   │   │   │   ├── ReplayGroup/                # Replay a tagged/selected group
│   │   │   │   └── CompareReplay/              # Diff original vs replayed results
│   │   │   ├── Infrastructure/
│   │   │   │   └── InferenceRecordingMiddleware.cs  # Auto-records every inference call
│   │   │   └── Api/
│   │   │       └── HistoryEndpoints.cs
│   │   │
│   │   ├── Models/                             # Feature: Model Management
│   │   │   ├── Domain/
│   │   │   │   └── InferenceInstance.cs        # Registered provider instance
│   │   │   ├── Application/
│   │   │   │   ├── RegisterInstance/
│   │   │   │   ├── GetInstanceMetrics/
│   │   │   │   ├── HealthCheck/
│   │   │   │   ├── SwapModel/                  # Hot-reload model on provider
│   │   │   │   └── SwapProvider/               # Change provider type at runtime
│   │   │   ├── Infrastructure/
│   │   │   │   ├── HealthCheckBackgroundService.cs
│   │   │   │   ├── ProviderConfigWatcher.cs    # Watches config file for provider changes
│   │   │   │   └── ProviderRegistry.cs         # In-memory mutable registry of active providers
│   │   │   └── Api/
│   │   │       └── ModelEndpoints.cs
│   │   │
│   │   ├── BatchInference/                     # Feature: Batch Inference
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── StartBatchJob/
│   │   │   │   └── BatchJobWorker.cs           # BackgroundService
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── BatchEndpoints.cs
│   │   │
│   │   ├── Analytics/                          # Feature: Analytics Dashboard
│   │   │   ├── Domain/
│   │   │   │   └── UsageLog.cs
│   │   │   ├── Application/
│   │   │   │   ├── TrackUsage/
│   │   │   │   ├── GetUsageAnalytics/
│   │   │   │   └── GetCostBreakdown/
│   │   │   ├── Infrastructure/
│   │   │   │   └── UsageTrackingMiddleware.cs  # Auto-logs inference requests
│   │   │   └── Api/
│   │   │       └── AnalyticsEndpoints.cs
│   │   │
│   │   ├── Notebooks/                          # Feature: JupyterLite Integration
│   │   │   ├── Application/
│   │   │   ├── Infrastructure/
│   │   │   └── Api/
│   │   │       └── NotebookEndpoints.cs
│   │   │
│   │   ├── FineTuning/                         # Feature: Fine-Tuning Support
│   │   │   ├── Application/
│   │   │   │   ├── ExportForTraining/
│   │   │   │   └── ValidateTrainingData/
│   │   │   └── Api/
│   │   │       └── FineTuningEndpoints.cs
│   │   │
│   │   ├── StructuredOutput/                   # Feature: Structured Output
│   │   │   ├── Domain/
│   │   │   ├── Application/
│   │   │   │   ├── RunGuidedDecoding/
│   │   │   │   └── ValidateOutput/
│   │   │   └── Api/
│   │   │       └── StructuredOutputEndpoints.cs
│   │   │
│   │   └── Skills/                             # Skill system (cross-cutting)
│   │       ├── ISkill.cs                       # Core skill interface
│   │       ├── SkillResult.cs                  # Wraps Result<T> + metrics
│   │       ├── SkillContext.cs                 # Execution context
│   │       ├── SkillRegistry.cs                # Discover, register, resolve skills
│   │       ├── SkillExecutor.cs                # Execute with tracing + guardrails
│   │       └── Implementations/               # Built-in skills (thin wrappers over feature handlers)
│   │           ├── InferenceSkills.cs
│   │           ├── LogprobsSkills.cs
│   │           ├── TokenizerSkills.cs
│   │           ├── ExperimentSkills.cs
│   │           ├── PromptSkills.cs
│   │           ├── DatasetSkills.cs
│   │           ├── EvaluationSkills.cs
│   │           ├── RagSkills.cs
│   │           ├── AnalyticsSkills.cs
│   │           └── UtilitySkills.cs
│   │
│   └── AiResearch.Tests/
│       ├── Unit/
│       │   ├── Common/
│       │   │   ├── ResultTests.cs
│       │   │   ├── LogprobsCalculatorTests.cs
│       │   │   └── CostCalculatorTests.cs
│       │   └── Features/
│       │       ├── Playground/
│       │       ├── TokenExplorer/
│       │       └── ...
│       ├── Integration/
│       │   ├── Api/                            # Endpoint tests with WebApplicationFactory
│       │   └── Infrastructure/                 # DB, cache, provider tests
│       └── TestHelpers/
│           ├── FakeInferenceProvider.cs
│           └── TestDbContextFactory.cs
│
├── backend.sln
```

---

## Frontend Project Structure

```
frontend/
├── src/
│   ├── app/
│   │   ├── App.tsx                        # Root component, router, providers
│   │   ├── routes.tsx                     # Route definitions
│   │   └── providers/
│   │       ├── QueryProvider.tsx           # TanStack Query setup
│   │       ├── ThemeProvider.tsx           # Dark/light mode
│   │       └── ToastProvider.tsx           # Sonner toast notifications
│   │
│   ├── components/                        # Shared UI components
│   │   ├── ui/                            # shadcn/ui primitives (Button, Input, Card, etc.)
│   │   ├── layout/
│   │   │   ├── AppShell.tsx               # Sidebar + header + content area
│   │   │   ├── Sidebar.tsx                # Module navigation
│   │   │   └── StatusBar.tsx              # Bottom bar: instance status, GPU, tokens
│   │   ├── feedback/
│   │   │   ├── ErrorBoundary.tsx          # Catch React errors gracefully
│   │   │   ├── LoadingSkeleton.tsx        # Content placeholder during loading
│   │   │   └── EmptyState.tsx             # "No data yet" with call-to-action
│   │   ├── data/
│   │   │   ├── DataTable.tsx              # Sortable, filterable, paginated table
│   │   │   ├── PaginationControls.tsx
│   │   │   └── FilterBar.tsx
│   │   ├── logprobs/                      # Reusable logprobs visualizations
│   │   │   ├── TokenHeatmap.tsx           # Colored token display by confidence
│   │   │   ├── AlternativeTokensPanel.tsx # Click-to-expand top-K alternatives
│   │   │   ├── EntropyChart.tsx           # Per-token entropy bar chart
│   │   │   ├── ProbabilityDistribution.tsx # Horizontal bar chart of token probs
│   │   │   └── PerplexityBadge.tsx        # Color-coded perplexity display
│   │   ├── charts/
│   │   │   ├── TimeSeriesChart.tsx        # Recharts line chart wrapper
│   │   │   ├── BarChart.tsx
│   │   │   ├── ScatterPlot.tsx
│   │   │   └── GaugeChart.tsx             # For GPU/cache utilization
│   │   └── chat/
│   │       ├── ChatMessage.tsx            # Single message bubble
│   │       ├── ChatInput.tsx              # Textarea with send button
│   │       ├── StreamingMessage.tsx        # Token-by-token rendering
│   │       └── SystemPromptEditor.tsx     # Collapsible system prompt area
│   │
│   ├── features/                          # Feature modules (mirrors backend slices)
│   │   ├── playground/
│   │   │   ├── PlaygroundPage.tsx         # Main page component
│   │   │   ├── ParameterSidebar.tsx       # Model selection, sliders, logprobs toggle
│   │   │   ├── ChatPane.tsx              # Single chat pane (reused for multi-pane)
│   │   │   ├── MultiPaneLayout.tsx        # 1-4 pane comparison layout
│   │   │   ├── ConversationHistory.tsx    # Saved conversations list
│   │   │   ├── hooks/
│   │   │   │   ├── useStreamChat.ts       # SSE streaming hook
│   │   │   │   ├── useConversations.ts    # CRUD + TanStack Query
│   │   │   │   └── useInferenceParams.ts  # Parameter state management
│   │   │   └── api/
│   │   │       └── playgroundApi.ts       # Typed API client for playground endpoints
│   │   │
│   │   ├── token-explorer/
│   │   │   ├── TokenExplorerPage.tsx
│   │   │   ├── PredictionList.tsx         # Ranked next-token predictions
│   │   │   ├── StepThroughView.tsx        # Step-by-step generation
│   │   │   ├── BranchTreeView.tsx         # Explored branches visualization
│   │   │   ├── SamplingVisualization.tsx   # top-p/top-k cutoff overlay
│   │   │   ├── hooks/
│   │   │   └── api/
│   │   │
│   │   ├── history/
│   │   │   ├── HistoryPage.tsx           # Browse all inference history
│   │   │   ├── HistoryTimeline.tsx       # Chronological timeline view
│   │   │   ├── ReplayPanel.tsx           # Configure and launch replay
│   │   │   ├── ReplayDiffView.tsx        # Side-by-side original vs replay
│   │   │   ├── hooks/
│   │   │   │   ├── useHistory.ts         # History search + filter
│   │   │   │   └── useReplay.ts          # Replay execution + progress
│   │   │   └── api/
│   │   │       └── historyApi.ts
│   │   │
│   │   ├── prompts/
│   │   ├── experiments/
│   │   ├── datasets/
│   │   ├── evaluation/
│   │   ├── rag/
│   │   ├── agents/
│   │   ├── models/
│   │   ├── batch/
│   │   ├── analytics/
│   │   ├── notebooks/
│   │   ├── fine-tuning/
│   │   └── structured-output/
│   │
│   ├── hooks/                             # Shared hooks
│   │   ├── useSSE.ts                      # Server-Sent Events connection
│   │   ├── useDebounce.ts
│   │   └── useLocalStorage.ts
│   │
│   ├── services/                          # Shared API layer
│   │   ├── apiClient.ts                   # Base fetch wrapper with error handling
│   │   ├── sseClient.ts                   # SSE connection manager
│   │   └── types/                         # Shared TypeScript types matching backend DTOs
│   │       ├── result.ts                  # Result<T> client-side representation
│   │       ├── inference.ts
│   │       ├── logprobs.ts
│   │       └── common.ts                  # PagedResult, Error, etc.
│   │
│   └── lib/
│       ├── utils.ts                       # cn(), formatters, constants
│       └── logprobs.ts                    # Client-side logprobs calculations (color mapping, etc.)
│
├── public/
├── index.html
├── tailwind.config.ts
├── tsconfig.json
├── vite.config.ts
└── package.json
```

---

## Result Pattern

All application-layer operations return `Result<T>`. No exceptions for expected failure cases.

### Core Types

```csharp
/// <summary>
/// Represents the outcome of an operation that can succeed with a value or fail with an error.
/// Use this as the return type for all application-layer use cases and service methods.
/// </summary>
/// <typeparam name="T">The type of the success value.</typeparam>
/// <example>
/// <code>
/// Result&lt;ConversationDto&gt; result = await handler.HandleAsync(query);
/// return result.Match(
///     onSuccess: dto => Results.Ok(dto),
///     onFailure: error => error.ToHttpResult()
/// );
/// </code>
/// </example>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T Value { get; }         // Throws if IsFailure
    public Error Error { get; }     // Throws if IsSuccess

    private Result(T value) { IsSuccess = true; Value = value; }
    private Result(Error error) { IsSuccess = false; Error = error; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Pattern match on success or failure. Exhaustive — both branches must be handled.
    /// </summary>
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
        => IsSuccess ? onSuccess(Value) : onFailure(Error);

    /// <summary>
    /// Transform the success value. Error passes through unchanged.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
        => IsSuccess ? Result<TNew>.Success(mapper(Value)) : Result<TNew>.Failure(Error);

    /// <summary>
    /// Chain operations that themselves return Result. Flattens nested Results.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
        => IsSuccess ? binder(Value) : Result<TNew>.Failure(Error);
}

/// <summary>
/// Represents a non-generic operation outcome (no return value on success).
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(Error error) => new() { IsSuccess = false, Error = error };
}

/// <summary>
/// Strongly-typed error value with a code, human-readable message, and error category.
/// Errors are values, not exceptions. They flow through the Result pattern.
/// </summary>
/// <example>
/// <code>
/// public static class DomainErrors
/// {
///     public static Error NotFound(string entity, string id)
///         => new($"{entity}.NotFound", $"{entity} with ID '{id}' was not found.", ErrorType.NotFound);
///     public static Error Validation(string message)
///         => new("Validation", message, ErrorType.Validation);
/// }
/// </code>
/// </example>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error NotFound(string message) => new("NotFound", message, ErrorType.NotFound);
    public static Error Validation(string message) => new("Validation", message, ErrorType.Validation);
    public static Error Conflict(string message) => new("Conflict", message, ErrorType.Conflict);
    public static Error Internal(string message) => new("Internal", message, ErrorType.Internal);
    public static Error Unavailable(string message) => new("Unavailable", message, ErrorType.Unavailable);
}

public enum ErrorType
{
    Validation,     // 400
    NotFound,       // 404
    Conflict,       // 409
    Internal,       // 500
    Unavailable     // 503
}
```

### HTTP Mapping

The `ResultToResponseMiddleware` (or endpoint extension method) maps errors to HTTP responses:

```csharp
/// <summary>
/// Extension methods for mapping Result&lt;T&gt; to Minimal API IResult responses.
/// Converts error types to appropriate HTTP status codes with ProblemDetails format.
/// </summary>
public static class ResultEndpointExtensions
{
    /// <summary>
    /// Maps a Result to an HTTP response. Success -> 200 OK. Failure -> appropriate error code.
    /// </summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
        => result.Match(
            onSuccess: value => Results.Ok(value),
            onFailure: error => error.Type switch
            {
                ErrorType.Validation  => Results.BadRequest(error.ToProblemDetails()),
                ErrorType.NotFound    => Results.NotFound(error.ToProblemDetails()),
                ErrorType.Conflict    => Results.Conflict(error.ToProblemDetails()),
                ErrorType.Unavailable => Results.Problem(error.ToProblemDetails(), statusCode: 503),
                _                     => Results.Problem(error.ToProblemDetails(), statusCode: 500)
            });
}
```

### Usage in Feature Slices

```csharp
// Application layer — returns Result<T>
public sealed class GetConversationHandler
{
    /// <summary>
    /// Retrieves a conversation by ID with all messages and optional logprobs data.
    /// </summary>
    /// <param name="query">The query containing the conversation ID and options.</param>
    /// <param name="ct">Cancellation token for request cancellation.</param>
    /// <returns>
    /// A <see cref="Result{ConversationDto}"/> containing the conversation on success,
    /// or a NotFound error if no conversation exists with the given ID.
    /// </returns>
    public async Task<Result<ConversationDto>> HandleAsync(
        GetConversationQuery query, CancellationToken ct)
    {
        var conversation = await _repository.GetByIdAsync(query.Id, ct);
        if (conversation is null)
            return Error.NotFound($"Conversation '{query.Id}' not found.");

        return conversation.ToDto(query.IncludeLogprobs);
    }
}

// API layer — maps Result to HTTP
app.MapGet("/api/v1/playground/conversations/{id}", async (
    Guid id,
    [FromQuery] bool includeLogprobs,
    GetConversationHandler handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(
        new GetConversationQuery(id, includeLogprobs), ct);
    return result.ToHttpResult();
})
.WithName("GetConversation")
.WithDescription("Retrieves a conversation with all messages.")
.Produces<ConversationDto>(200)
.ProducesProblem(404);
```

---

## Inference Provider Abstraction

The most critical abstraction. All AI inference goes through `IInferenceProvider`, regardless of backend.

### Interface

```csharp
/// <summary>
/// Abstracts AI inference backends (vLLM, Ollama, LM Studio, OpenAI-compatible).
/// All model interaction in the platform goes through this interface.
/// Implementations handle protocol differences, authentication, and provider-specific features.
/// </summary>
public interface IInferenceProvider
{
    /// <summary>The display name of this provider (e.g., "vLLM", "Ollama").</summary>
    string ProviderName { get; }

    /// <summary>The base endpoint URL for this provider instance.</summary>
    string Endpoint { get; }

    /// <summary>
    /// Describes what this provider supports. Not all providers support logprobs,
    /// guided decoding, or streaming. Check before calling those features.
    /// </summary>
    ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Send a chat completion request and receive the full response.
    /// </summary>
    /// <param name="request">The chat request with model, messages, and parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="Result{ChatResponse}"/> containing the model response with usage metrics,
    /// or an error if the provider is unavailable or the request is invalid.
    /// </returns>
    Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct);

    /// <summary>
    /// Stream a chat completion response token-by-token via SSE.
    /// Each yielded <see cref="StreamChunk"/> contains one token and optional logprobs.
    /// </summary>
    /// <param name="request">The chat request. Stream flag is set automatically.</param>
    /// <param name="ct">Cancellation token. Cancelling stops the stream.</param>
    /// <returns>An async stream of token chunks.</returns>
    IAsyncEnumerable<StreamChunk> StreamChatAsync(
        ChatRequest request, CancellationToken ct);

    /// <summary>
    /// Get information about the loaded model (name, context length, capabilities).
    /// </summary>
    Task<Result<ModelInfo>> GetModelInfoAsync(CancellationToken ct);

    /// <summary>
    /// Check if the provider is healthy and responsive.
    /// </summary>
    Task<Result<HealthStatus>> CheckHealthAsync(CancellationToken ct);

    /// <summary>
    /// Get runtime metrics (throughput, queue depth, GPU/cache utilization).
    /// Not all providers expose metrics — check <see cref="Capabilities.SupportsMetrics"/>.
    /// </summary>
    Task<Result<ProviderMetrics>> GetMetricsAsync(CancellationToken ct);

    /// <summary>
    /// Tokenize text using the provider's tokenizer. Returns token IDs and strings.
    /// Not all providers support this — check <see cref="Capabilities.SupportsTokenize"/>.
    /// </summary>
    Task<Result<TokenizeResponse>> TokenizeAsync(string text, CancellationToken ct);
}

/// <summary>
/// Describes what features a specific inference provider supports.
/// Check these before calling optional features to avoid runtime errors.
/// </summary>
/// <example>
/// <code>
/// if (provider.Capabilities.SupportsLogprobs)
///     request.Logprobs = true;
/// </code>
/// </example>
public sealed record ProviderCapabilities
{
    /// <summary>Can return per-token log probabilities.</summary>
    public bool SupportsLogprobs { get; init; }

    /// <summary>Maximum top_logprobs value supported (typically 5 or 20).</summary>
    public int MaxTopLogprobs { get; init; }

    /// <summary>Can constrain output to a JSON schema (guided decoding).</summary>
    public bool SupportsGuidedDecoding { get; init; }

    /// <summary>Can stream responses via SSE.</summary>
    public bool SupportsStreaming { get; init; }

    /// <summary>Exposes runtime metrics (throughput, GPU, cache).</summary>
    public bool SupportsMetrics { get; init; }

    /// <summary>Can tokenize text via API.</summary>
    public bool SupportsTokenize { get; init; }

    /// <summary>Supports image/multimodal inputs.</summary>
    public bool SupportsMultimodal { get; init; }

    /// <summary>Can load/unload LoRA adapters dynamically.</summary>
    public bool SupportsLoraAdapters { get; init; }

    /// <summary>Supports prefix caching for shared prompt prefixes.</summary>
    public bool SupportsPrefixCaching { get; init; }

    /// <summary>Can load/unload different models at runtime without restarting.</summary>
    public bool SupportsModelSwap { get; init; }
}
```

### Model Hot-Reload

Some providers support loading a different model without restarting the service.

```csharp
/// <summary>
/// Extended interface for providers that support model hot-reload.
/// Not all providers support this — check <see cref="ProviderCapabilities.SupportsModelSwap"/>.
/// vLLM: does not support hot-swap (requires restart).
/// Ollama: supports loading/unloading models dynamically.
/// LM Studio: supports model switching via UI and API.
/// </summary>
public interface IHotReloadableProvider : IInferenceProvider
{
    /// <summary>
    /// List models available to load on this provider (downloaded/cached).
    /// </summary>
    Task<Result<IReadOnlyList<AvailableModel>>> ListAvailableModelsAsync(CancellationToken ct);

    /// <summary>
    /// Load a different model on this provider instance.
    /// The current model is unloaded. In-flight requests may fail.
    /// </summary>
    /// <param name="modelId">The model identifier to load (e.g., "llama3.1:70b" for Ollama).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated model info on success, or error if the model is not available or load fails.</returns>
    Task<Result<ModelInfo>> LoadModelAsync(string modelId, CancellationToken ct);

    /// <summary>
    /// Unload the current model, freeing GPU/RAM resources.
    /// </summary>
    Task<Result> UnloadModelAsync(CancellationToken ct);
}
```

### Provider Factory & Runtime Registry

The provider factory is backed by a **mutable in-memory registry** that can be updated at runtime via API or config file changes. No restart required.

```csharp
/// <summary>
/// Resolves the correct <see cref="IInferenceProvider"/> for a registered instance.
/// The registry is mutable — providers can be added, removed, or reconfigured at runtime
/// via the API or by editing the config file (watched with <see cref="ProviderConfigWatcher"/>).
/// </summary>
public interface IInferenceProviderFactory
{
    /// <summary>
    /// Get a provider for a registered instance by ID.
    /// </summary>
    /// <param name="instanceId">The registered instance ID from the Models feature.</param>
    /// <returns>The configured provider, or an error if the instance is not found.</returns>
    Result<IInferenceProvider> GetProvider(Guid instanceId);

    /// <summary>
    /// Get a provider by direct endpoint and type. Used for ad-hoc connections.
    /// </summary>
    /// <param name="endpoint">The base URL of the inference server.</param>
    /// <param name="providerType">The type of inference backend.</param>
    /// <returns>A configured provider for the given endpoint.</returns>
    Result<IInferenceProvider> GetProvider(string endpoint, InferenceProviderType providerType);

    /// <summary>
    /// Get all currently registered and healthy providers.
    /// </summary>
    IReadOnlyList<IInferenceProvider> GetAllProviders();

    /// <summary>
    /// Register a new provider instance at runtime. Immediately available for inference.
    /// Persisted to both the in-memory registry and the database.
    /// </summary>
    /// <param name="config">Provider configuration (endpoint, type, display name).</param>
    /// <returns>The instance ID for the newly registered provider.</returns>
    Task<Result<Guid>> RegisterProviderAsync(ProviderConfig config, CancellationToken ct);

    /// <summary>
    /// Remove a provider instance at runtime. In-flight requests to this provider
    /// will complete, but no new requests will be routed to it.
    /// </summary>
    Task<Result> UnregisterProviderAsync(Guid instanceId, CancellationToken ct);

    /// <summary>
    /// Update a provider's configuration at runtime (e.g., change endpoint, change type).
    /// The old provider is drained and replaced with the new configuration.
    /// </summary>
    Task<Result> UpdateProviderAsync(Guid instanceId, ProviderConfig config, CancellationToken ct);

    /// <summary>
    /// Reload all providers from the config file. Called by <see cref="ProviderConfigWatcher"/>
    /// when the file changes, or manually via API.
    /// </summary>
    Task<Result> ReloadFromConfigAsync(CancellationToken ct);
}
```

### Config-Driven Provider Registration

Providers can be defined in `appsettings.json` (or a dedicated `providers.json`). The file is watched for changes — edit it and providers update without restart.

```json
// appsettings.json (or providers.json — watched by ProviderConfigWatcher)
{
  "InferenceProviders": [
    {
      "Name": "Local vLLM (llama-70b)",
      "Type": "Vllm",
      "Endpoint": "http://localhost:8000",
      "IsDefault": true,
      "Tags": ["production", "large"]
    },
    {
      "Name": "Ollama (mistral)",
      "Type": "Ollama",
      "Endpoint": "http://localhost:11434",
      "DefaultModel": "mistral:latest",
      "Tags": ["dev", "fast"]
    },
    {
      "Name": "LM Studio (local)",
      "Type": "LmStudio",
      "Endpoint": "http://localhost:1234",
      "Tags": ["dev"]
    }
  ]
}
```

```csharp
/// <summary>
/// Watches the provider configuration file for changes using <see cref="IOptionsMonitor{T}"/>
/// or <see cref="FileSystemWatcher"/>. When the file changes:
/// 1. Parse new provider list
/// 2. Diff against current registry (add new, remove deleted, update changed)
/// 3. Call <see cref="IInferenceProviderFactory.ReloadFromConfigAsync"/>
/// 4. Log changes with structured logging
/// No restart required. In-flight requests to removed providers complete gracefully.
/// </summary>
public sealed class ProviderConfigWatcher : BackgroundService { ... }
```

### Provider Swap via API

Providers can also be swapped entirely through the API (no config file edit needed):

```
POST   /api/v1/models/instances                      # Register new provider
PUT    /api/v1/models/instances/{id}                  # Update endpoint, type, or config
DELETE /api/v1/models/instances/{id}                  # Remove provider
POST   /api/v1/models/instances/{id}/swap-model       # Hot-reload model (if provider supports)
POST   /api/v1/models/instances/reload-config          # Force reload from config file
GET    /api/v1/models/instances/{id}/available-models  # List models available on this provider
```

Changes via API are also persisted to the database so they survive restarts. Config file takes precedence on startup, but API changes override at runtime.

### Provider Capability Matrix

| Capability | vLLM | Ollama | LM Studio | OpenAI-Compatible |
|------------|------|--------|-----------|-------------------|
| Chat completion | Yes | Yes | Yes | Yes |
| Streaming (SSE) | Yes | Yes | Yes | Yes |
| Logprobs | Yes (top 20) | Partial (top 1-5) | Yes (top 20) | Varies |
| Guided decoding | Yes | No | No | Varies |
| Metrics endpoint | Yes (Prometheus) | No | No | No |
| Tokenize API | Yes | No | No | No |
| Multimodal | Model-dependent | Model-dependent | Model-dependent | Model-dependent |
| LoRA adapters | Yes | No | No | No |
| Prefix caching | Yes | No | No | No |
| Model hot-swap | No (restart) | Yes | Yes | Varies |

When a provider doesn't support a capability, the platform:
1. Checks `Capabilities` before attempting
2. Returns `Error.Unavailable($"{ProviderName} does not support {feature}")` if the user explicitly requests it
3. Gracefully degrades in the UI (e.g., logprobs toggle disabled, metrics panel hidden)

---

## Inference History & Replay

Every inference call is automatically recorded. The history is browsable, searchable, and — critically — **replayable** against different models, providers, or parameters.

### Why This Matters for Research

- **Regression testing:** Replay your entire session against a new model version. Did anything get worse?
- **Model comparison:** Replay 100 historical prompts against a different model. Compare all results.
- **Prompt iteration:** Replay a history group with a new prompt template. See if your changes helped.
- **Provider swap validation:** Switched from vLLM to Ollama? Replay and compare.
- **Reproducibility:** Every inference is captured with full config. Replay produces identical conditions.

### Data Model

```csharp
/// <summary>
/// Immutable record of a single inference call. Automatically captured by
/// <see cref="InferenceRecordingMiddleware"/> on every inference request.
/// Contains everything needed to exactly reproduce the call.
/// </summary>
public sealed class InferenceRecord : BaseEntity
{
    /// <summary>Which module originated this call (playground, prompt_lab, evaluation, agent, etc.).</summary>
    public string SourceModule { get; init; }

    /// <summary>The provider instance used (ID + type + endpoint at time of call).</summary>
    public Guid ProviderInstanceId { get; init; }
    public string ProviderType { get; init; }    // "Vllm", "Ollama", etc.
    public string ProviderEndpoint { get; init; }

    /// <summary>The model identifier used for this call.</summary>
    public string Model { get; init; }

    /// <summary>The complete request: messages, system prompt, parameters, logprobs settings.</summary>
    public JsonDocument Request { get; init; }

    /// <summary>The complete response: content, finish reason, usage, logprobs data.</summary>
    public JsonDocument Response { get; init; }

    /// <summary>Performance metrics captured at call time.</summary>
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int LatencyMs { get; init; }
    public int? TtftMs { get; init; }
    public double? Perplexity { get; init; }
    public decimal Cost { get; init; }

    /// <summary>Optional prompt template version if the call was template-driven.</summary>
    public Guid? PromptVersionId { get; init; }

    /// <summary>Optional experiment run ID if the call was saved as an experiment.</summary>
    public Guid? RunId { get; init; }

    /// <summary>User-assigned tags for grouping and filtering (e.g., "session-1", "ner-tests").</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>Correlation ID for tracing related calls.</summary>
    public string CorrelationId { get; init; }
}

/// <summary>
/// A replay session that re-executes a set of historical inference records
/// against a (potentially different) model, provider, or parameter set.
/// </summary>
public sealed class ReplaySession : BaseEntity
{
    public string Name { get; init; }
    public ReplayMode Mode { get; init; }          // Single, Batch, Group
    public ReplayStatus Status { get; init; }       // Pending, Running, Paused, Complete, Failed

    /// <summary>What to change from the originals during replay.</summary>
    public string? OverrideModel { get; init; }
    public Guid? OverrideProviderInstanceId { get; init; }
    public JsonDocument? OverrideParameters { get; init; }
    public Guid? OverridePromptVersionId { get; init; }

    /// <summary>The source records being replayed.</summary>
    public List<Guid> SourceRecordIds { get; init; } = [];

    public int TotalRecords { get; init; }
    public int CompletedRecords { get; init; }
    public int FailedRecords { get; init; }

    public List<ReplayResult> Results { get; init; } = [];
}

/// <summary>
/// Comparison between an original inference record and its replayed result.
/// </summary>
public sealed class ReplayResult : BaseEntity
{
    public Guid ReplaySessionId { get; init; }
    public Guid OriginalRecordId { get; init; }

    /// <summary>The replayed response.</summary>
    public JsonDocument ReplayedResponse { get; init; }

    /// <summary>Metrics comparison.</summary>
    public int ReplayedTokens { get; init; }
    public int ReplayedLatencyMs { get; init; }
    public double? ReplayedPerplexity { get; init; }
    public decimal ReplayedCost { get; init; }

    /// <summary>Did the output change? Simple text equality check.</summary>
    public bool OutputChanged { get; init; }

    /// <summary>Similarity score between original and replayed output (0.0-1.0).</summary>
    public double? OutputSimilarity { get; init; }
}

public enum ReplayMode { Single, Batch, Group }
public enum ReplayStatus { Pending, Running, Paused, Complete, Failed, Cancelled }
```

### API Endpoints

```
# History
GET    /api/v1/history                          # Browse inference history (paginated, filterable)
GET    /api/v1/history/{id}                     # Full detail of one inference record
PUT    /api/v1/history/{id}/tags                # Add/remove tags
DELETE /api/v1/history/{id}                     # Delete a record
GET    /api/v1/history/search                   # Full-text search across prompts/responses
POST   /api/v1/history/tag-batch                # Tag multiple records at once

# Replay
POST   /api/v1/history/replay                   # Start a replay session
GET    /api/v1/history/replay/{id}              # Get replay session status + results
POST   /api/v1/history/replay/{id}/pause        # Pause a running replay
POST   /api/v1/history/replay/{id}/resume       # Resume a paused replay
POST   /api/v1/history/replay/{id}/cancel       # Cancel a replay
GET    /api/v1/history/replay/{id}/diff         # Get diff view between originals and replays
```

### Replay Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| **Single** | Replay one record | "Re-run this exact prompt against Ollama instead of vLLM" |
| **Batch** | Replay all records matching a filter | "Replay everything from today against the new model" |
| **Group** | Replay records with a specific tag | "Replay all 'ner-tests' against prompt v4" |

### Replay Overrides

When starting a replay, you can override any combination:

```json
{
  "source": {
    "record_ids": [/* explicit IDs */],
    "filter": { "tags": ["ner-tests"], "date_from": "2026-03-01" },
    "mode": "group"
  },
  "overrides": {
    "model": "mistral:latest",
    "provider_instance_id": "uuid-of-ollama-instance",
    "parameters": { "temperature": 0.1, "top_p": 0.95 },
    "prompt_version_id": "uuid-of-ner-v4"
  },
  "options": {
    "capture_logprobs": true,
    "concurrency": 4,
    "compute_similarity": true,
    "step_mode": false
  }
}
```

- **`step_mode: true`** — Pause after each record, show result, wait for "Continue" or "Skip". For careful one-at-a-time review.
- **`step_mode: false`** — Run all records with concurrency. Progress bar, ETA, live results.

### Recording Middleware

```csharp
/// <summary>
/// Middleware that automatically records every inference call to the history table.
/// Wraps <see cref="IInferenceProvider"/> calls via a decorator pattern.
/// Records are written asynchronously (fire-and-forget to a Channel) so they don't
/// add latency to the inference response.
///
/// Recording can be disabled globally or per-request via X-Skip-Recording header
/// (useful for replay calls to avoid recording replays of replays).
/// </summary>
public sealed class InferenceRecordingMiddleware { ... }
```

The recording decorator wraps `IInferenceProvider`:

```csharp
/// <summary>
/// Decorator that records every inference call before returning the result.
/// Applied via DI decoration — all consumers of IInferenceProvider automatically
/// get the recording behavior without any code changes.
/// </summary>
public sealed class RecordingInferenceProvider : IInferenceProvider
{
    private readonly IInferenceProvider _inner;
    private readonly ChannelWriter<InferenceRecord> _recordChannel;

    /// <summary>
    /// Delegates to the inner provider, then writes an InferenceRecord to the
    /// async channel for background persistence. Does not block the response.
    /// </summary>
    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await _inner.ChatAsync(request, ct);
        stopwatch.Stop();

        // Fire-and-forget to background writer — does not affect response latency
        _recordChannel.TryWrite(BuildRecord(request, result, stopwatch.Elapsed));

        return result;
    }
}
```

---

## Cache Abstraction

All caching goes through `ICacheService`. Features never touch `IMemoryCache` or Redis directly. See ADR-003.

```csharp
/// <summary>
/// Provider-agnostic cache service. Implementations include in-memory (Phase 1-2),
/// Redis (Phase 3+), and a no-op for testing. Features code against this interface
/// and the implementation is swapped via DI configuration.
/// </summary>
public interface ICacheService
{
    /// <summary>Get a cached value by key. Returns null if not found or expired.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);

    /// <summary>Set a value with optional cache options (TTL, sliding expiration).</summary>
    Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default);

    /// <summary>Remove a cached value by key.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Get a value if cached, otherwise execute the factory, cache the result, and return it.
    /// This is the primary method features should use — it handles cache-aside automatically.
    /// </summary>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        CacheOptions? options = null, CancellationToken ct = default);

    /// <summary>Remove all keys matching a pattern (e.g., "experiments:*"). Provider-dependent support.</summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}

/// <summary>
/// Cache entry configuration. All fields optional — sensible defaults are applied.
/// </summary>
public sealed record CacheOptions
{
    /// <summary>Absolute expiration from now. Default: 5 minutes.</summary>
    public TimeSpan? AbsoluteExpiration { get; init; }

    /// <summary>Sliding expiration — resets on each access. Default: null (disabled).</summary>
    public TimeSpan? SlidingExpiration { get; init; }

    /// <summary>Logical region/namespace for cache partitioning (e.g., "experiments", "models").</summary>
    public string? Region { get; init; }

    /// <summary>Tags for bulk invalidation (e.g., invalidate all cache entries tagged "project:abc").</summary>
    public string[]? Tags { get; init; }
}
```

### Implementation Swap

```csharp
// In ServiceCollectionExtensions — switch via config
services.AddCommonCache(config);

// Reads "Cache:Provider" from appsettings.json: "InMemory" | "Redis" | "None"
// InMemory -> InMemoryCacheService (IMemoryCache wrapper)
// Redis    -> RedisCacheService (StackExchange.Redis)
// None     -> NullCacheService (no-op, for testing)
```

---

## File Storage Abstraction

All file operations go through `IFileStorage`. Features never use `System.IO.File` or `Directory` directly. See ADR-004.

```csharp
/// <summary>
/// Provider-agnostic file storage. Implementations include local filesystem (Phase 1),
/// Azure Blob Storage, and AWS S3/MinIO (future). Features code against this interface
/// so storage backend can be swapped without touching any feature code.
///
/// All paths use forward slashes and are relative to the storage root.
/// The implementation handles mapping to actual storage locations.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Store a file. Creates directories/containers as needed.
    /// </summary>
    /// <param name="path">Relative path (e.g., "datasets/customer_support_v2.jsonl").</param>
    /// <param name="content">File content stream.</param>
    /// <param name="contentType">MIME type (e.g., "application/json").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Metadata of the stored file.</returns>
    Task<Result<FileMetadata>> StoreAsync(
        StoragePath path, Stream content, string contentType, CancellationToken ct);

    /// <summary>Retrieve a file as a stream. Caller is responsible for disposing the stream.</summary>
    Task<Result<Stream>> RetrieveAsync(StoragePath path, CancellationToken ct);

    /// <summary>Delete a file. Returns success even if the file doesn't exist (idempotent).</summary>
    Task<Result> DeleteAsync(StoragePath path, CancellationToken ct);

    /// <summary>Check if a file exists at the given path.</summary>
    Task<Result<bool>> ExistsAsync(StoragePath path, CancellationToken ct);

    /// <summary>Get metadata without downloading the file content.</summary>
    Task<Result<FileMetadata>> GetMetadataAsync(StoragePath path, CancellationToken ct);

    /// <summary>List files under a prefix (like a directory listing).</summary>
    Task<Result<IReadOnlyList<FileMetadata>>> ListAsync(
        StoragePath prefix, CancellationToken ct);

    /// <summary>
    /// Get a pre-signed URL for direct browser download (if supported by provider).
    /// Local filesystem returns a file:// URL or serves through a controller.
    /// Blob storage returns a time-limited SAS/pre-signed URL.
    /// </summary>
    Task<Result<string>> GetDownloadUrlAsync(
        StoragePath path, TimeSpan expiry, CancellationToken ct);
}

/// <summary>
/// Normalized storage path. Enforces forward slashes, no leading slash, no path traversal.
/// Value object that prevents accidental path injection.
/// </summary>
/// <example>
/// <code>
/// var path = StoragePath.From("datasets/customer_support_v2.jsonl");
/// var path = StoragePath.From("exports", $"batch_{jobId}.jsonl");  // Combines segments
/// </code>
/// </example>
public sealed record StoragePath
{
    public string Value { get; }
    public static StoragePath From(params string[] segments) { ... }
}

/// <summary>
/// Metadata for a stored file. Provider-agnostic — all implementations populate these fields.
/// </summary>
public sealed record FileMetadata(
    StoragePath Path,
    long SizeBytes,
    string ContentType,
    DateTime CreatedAt,
    DateTime ModifiedAt,
    string? ETag
);
```

### Implementation Swap

```csharp
// Reads "Storage:Provider" from appsettings.json: "Local" | "AzureBlob" | "S3"
// Local     -> LocalFileStorage (filesystem, base path from config)
// AzureBlob -> AzureBlobStorage (connection string from config)
// S3        -> S3Storage (endpoint, bucket, credentials from config)
```

---

## Authentication Abstraction

Auth is abstracted from day one so it can be swapped from local auth to Entra/OIDC without touching feature code. See ADR-005.

### Design

```
Phase 1:  NoAuthProvider (default) — every request is "local-user", no login required
Phase 1+: LocalAuthProvider — username/password, JWT tokens, stored in PostgreSQL
Future:   EntraAuthProvider — Microsoft Entra ID (Azure AD) federated auth
Future:   OidcAuthProvider — Generic OpenID Connect (Keycloak, Auth0, Okta, etc.)
```

Features never check auth directly. They use `ICurrentUser` (a scoped service) to get the authenticated user, and `[RequireAuthorization]` on endpoints for access control.

### Interfaces

```csharp
/// <summary>
/// Abstracts authentication and user management. Implementations handle
/// credential validation, token issuance, and user lifecycle.
/// The active provider is configured via "Auth:Provider" in appsettings.json.
/// </summary>
public interface IAuthProvider
{
    /// <summary>The provider name (e.g., "Local", "Entra", "OIDC").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Authenticate a user with provider-specific credentials.
    /// Local: username + password. Entra/OIDC: handles redirect flow.
    /// </summary>
    Task<Result<AuthResult>> AuthenticateAsync(AuthRequest request, CancellationToken ct);

    /// <summary>Validate an existing token/session and return the user info.</summary>
    Task<Result<UserInfo>> ValidateTokenAsync(string token, CancellationToken ct);

    /// <summary>Refresh an expired token (if provider supports it).</summary>
    Task<Result<AuthResult>> RefreshTokenAsync(string refreshToken, CancellationToken ct);

    /// <summary>Revoke a token/session (logout).</summary>
    Task<Result> RevokeTokenAsync(string token, CancellationToken ct);
}

/// <summary>
/// Scoped service that represents the currently authenticated user for this request.
/// Injected into handlers that need to know who is calling.
/// Populated by AuthenticationMiddleware from the request token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Whether this request is authenticated.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The user's unique ID. Null if not authenticated.</summary>
    string? UserId { get; }

    /// <summary>Display name.</summary>
    string? DisplayName { get; }

    /// <summary>Email address (if available from auth provider).</summary>
    string? Email { get; }

    /// <summary>Roles assigned to this user (e.g., "admin", "researcher").</summary>
    IReadOnlySet<string> Roles { get; }

    /// <summary>Check if the user has a specific role.</summary>
    bool IsInRole(string role);
}

/// <summary>
/// Canonical user representation that works across all auth providers.
/// Entra returns claims, OIDC returns claims, local returns DB fields —
/// all map to this single type.
/// </summary>
public sealed record UserInfo(
    string UserId,
    string DisplayName,
    string? Email,
    IReadOnlySet<string> Roles
);

/// <summary>
/// Successful authentication result. Contains tokens and user info.
/// </summary>
public sealed record AuthResult(
    string AccessToken,
    string? RefreshToken,
    DateTime ExpiresAt,
    UserInfo User
);
```

### NoAuthProvider (Default — Phase 1)

```csharp
/// <summary>
/// Default auth provider that authenticates every request as "local-user".
/// No login required. All endpoints are accessible. Use this for single-user
/// local development. Switch to LocalAuthProvider or EntraAuthProvider via config.
/// </summary>
public sealed class NoAuthProvider : IAuthProvider
{
    public string ProviderName => "None";

    public Task<Result<AuthResult>> AuthenticateAsync(AuthRequest request, CancellationToken ct)
        => Task.FromResult(Result<AuthResult>.Success(new AuthResult(
            AccessToken: "no-auth",
            RefreshToken: null,
            ExpiresAt: DateTime.MaxValue,
            User: new UserInfo("local-user", "Local User", null, new HashSet<string> { "admin" })
        )));

    public Task<Result<UserInfo>> ValidateTokenAsync(string token, CancellationToken ct)
        => Task.FromResult(Result<UserInfo>.Success(
            new UserInfo("local-user", "Local User", null, new HashSet<string> { "admin" })
        ));

    // RefreshToken and RevokeToken are no-ops
}
```

### Config-Driven Provider Swap

```json
// appsettings.json
{
  "Auth": {
    "Provider": "None",        // "None" | "Local" | "Entra" | "OIDC"

    "Local": {                  // Only used when Provider == "Local"
      "JwtSecret": "...",
      "TokenExpirationMinutes": 60,
      "AllowRegistration": true
    },

    "Entra": {                  // Only used when Provider == "Entra"
      "TenantId": "...",
      "ClientId": "...",
      "ClientSecret": "...",
      "Authority": "https://login.microsoftonline.com/{tenant}"
    },

    "OIDC": {                   // Only used when Provider == "OIDC"
      "Authority": "https://keycloak.example.com/realms/research",
      "ClientId": "...",
      "ClientSecret": "...",
      "Scopes": ["openid", "profile", "email"]
    }
  }
}
```

```csharp
// DI registration — reads config and registers the right implementation
services.AddCommonAuth(config);
// Reads "Auth:Provider", registers the matching IAuthProvider + ICurrentUser
```

### Feature Usage

```csharp
// Features use ICurrentUser — they never know which auth provider is active
public sealed class CreateExperimentHandler
{
    private readonly ICurrentUser _currentUser;

    public async Task<Result<ExperimentDto>> HandleAsync(CreateExperimentCommand cmd, CancellationToken ct)
    {
        var experiment = new Experiment
        {
            Name = cmd.Name,
            CreatedBy = _currentUser.UserId,  // "local-user" in NoAuth, real ID in Entra
            // ...
        };
        // ...
    }
}

// Endpoints use standard .NET authorization
app.MapGroup("/api/v1/experiments")
    .RequireAuthorization()              // Requires any authenticated user
    .WithTags("Experiments");

app.MapDelete("/api/v1/experiments/{id}",
    async (Guid id, DeleteExperimentHandler handler, CancellationToken ct) => { ... })
    .RequireAuthorization("AdminOnly");  // Requires "admin" role
```

---

## Database Abstraction

EF Core IS the database abstraction. No custom `IDatabase` interface — EF Core already handles provider swap. See ADR-008.

### Key Rules

1. **No raw SQL or Postgres-specific features in feature code.** All queries use LINQ. Postgres-specific column types (`jsonb`, `citext`) are configured in `IEntityTypeConfiguration<T>` only.
2. **Single `AppDbContext`** in `Common/Database/`. Multiple DbContexts fragment the connection pool.
3. **Features register entity configs** via `IEntityTypeConfiguration<T>` in their own `Infrastructure/` folder. Auto-discovered by assembly scanning.
4. **Feature tables use a prefix:** `playground_sessions`, `experiments_runs`, `history_records`.
5. **Migrations live in `Common/Database/Migrations/`** — single linear history.

### AppDbContext

```csharp
/// <summary>
/// Central database context. Each feature registers its entity configurations
/// via IEntityTypeConfiguration. Discovered by scanning the Features assembly.
/// </summary>
public sealed class AppDbContext : DbContext
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyCommonConventions();  // snake_case naming, UTC dates
    }
}
```

### Migration Strategy

| Environment | Strategy | How |
|-------------|----------|-----|
| Development | Auto-migrate on startup | `context.Database.MigrateAsync()` in `Program.cs` (dev only) |
| CI/Testing | Fresh database per test | Testcontainers spins up Postgres, applies all migrations |
| Production | Explicit migration scripts | `dotnet ef migrations script --idempotent` applied by deployment pipeline |

### Provider Swap

```json
{
  "Database": {
    "Provider": "Postgres",
    "ConnectionString": "Host=localhost;Database=ai_research;Username=postgres;Password=..."
  }
}
```

```csharp
// Reads "Database:Provider": "Postgres" | "SqlServer" | "Sqlite"
services.AddCommonDatabase(config);
```

---

## Vector Store Abstraction

RAG vector search is behind `IVectorStore`. Default implementation uses pgvector (no extra infrastructure). See ADR-009.

```csharp
/// <summary>
/// Abstracts vector similarity search. Implementations: pgvector (default), Qdrant, Pinecone.
/// </summary>
public interface IVectorStore
{
    /// <summary>Create a named collection with fixed dimensions and distance metric.</summary>
    Task<Result<VectorCollection>> CreateCollectionAsync(
        string name, int dimensions, DistanceMetric metric, CancellationToken ct);

    /// <summary>Delete a collection and all its vectors.</summary>
    Task<Result> DeleteCollectionAsync(string name, CancellationToken ct);

    /// <summary>Upsert vectors with metadata. Idempotent — existing IDs are overwritten.</summary>
    Task<Result> UpsertAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct);

    /// <summary>Find the K nearest vectors to the query vector.</summary>
    Task<Result<IReadOnlyList<VectorSearchResult>>> SearchAsync(
        string collection, ReadOnlyMemory<float> queryVector, int topK,
        VectorFilter? filter, CancellationToken ct);

    /// <summary>Delete vectors by ID.</summary>
    Task<Result> DeleteAsync(string collection, IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>Collection statistics: vector count, index status, storage size.</summary>
    Task<Result<CollectionStats>> GetStatsAsync(string name, CancellationToken ct);
}
```

### Provider Swap

```json
{
  "VectorStore": {
    "Provider": "PgVector",
    "PgVector": { "Schema": "vectors", "DefaultIndexType": "hnsw" },
    "Qdrant": { "Endpoint": "http://localhost:6333" },
    "Pinecone": { "ApiKey": "...", "Environment": "us-east-1" }
  }
}
```

| Provider | Config Value | Notes |
|----------|-------------|-------|
| pgvector | `"PgVector"` | Default. Uses existing PostgreSQL. Collections = tables in `vectors` schema. |
| Qdrant | `"Qdrant"` | Open-source, local Docker. Purpose-built for vectors. |
| Pinecone | `"Pinecone"` | Managed cloud. For production scale beyond pgvector. |

---

## Observability — Aspire ServiceDefaults + OpenTelemetry

Use **.NET Aspire ServiceDefaults** (the lightweight package) for observability. Not the full Aspire AppHost orchestration — that conflicts with our config-driven provider model.

### What ServiceDefaults Provides

```csharp
/// <summary>
/// Registers Aspire ServiceDefaults: OpenTelemetry (traces + metrics + logs),
/// health checks, resilience (Polly), and service discovery defaults.
/// This is a NuGet package, not the Aspire orchestrator.
/// </summary>
builder.AddServiceDefaults();

// This single line gives us:
// - OpenTelemetry traces (HTTP, EF Core, custom activities)
// - OpenTelemetry metrics (request duration, active connections, custom counters)
// - OpenTelemetry logs (Serilog -> OTel exporter)
// - Health check endpoints (/health, /alive)
// - Resilience policies via Polly (retries, circuit breakers)
// - Aspire Dashboard in dev (traces, logs, metrics in a local UI)
```

### Custom Traces for Inference

```csharp
/// <summary>
/// Activity source for inference-related traces. Creates spans for:
/// - Full inference call (chat completion, streaming)
/// - Provider health checks
/// - History recording
/// - Replay sessions
/// </summary>
public static class InferenceTracing
{
    public static readonly ActivitySource Source = new("AiResearch.Inference");

    public static Activity? StartInference(string provider, string model)
        => Source.StartActivity("inference.chat")
            ?.SetTag("inference.provider", provider)
            ?.SetTag("inference.model", model);
}
```

### Export Targets

| Environment | Export To | How |
|-------------|----------|-----|
| Dev | Aspire Dashboard | Auto-configured by ServiceDefaults |
| Dev (alt) | Jaeger | `AddOtlpExporter(o => o.Endpoint = "http://localhost:4317")` |
| Production | Grafana/Tempo | OTLP exporter to Grafana Cloud or self-hosted |

Serilog remains for structured logging — it feeds into the OTel log pipeline via the Serilog OpenTelemetry sink.

---

## Inference Rate Limiting

Outbound concurrency control for inference backends. Prevents overwhelming a local GPU with batch inference, replay-all, or multi-pane playground.

```csharp
/// <summary>
/// Wraps an IInferenceProvider with concurrency limiting.
/// Uses SemaphoreSlim to cap concurrent requests to a provider.
/// Configurable per-provider via appsettings or runtime API.
/// </summary>
public sealed class RateLimitedInferenceProvider : IInferenceProvider
{
    private readonly IInferenceProvider _inner;
    private readonly SemaphoreSlim _semaphore;

    public RateLimitedInferenceProvider(IInferenceProvider inner, int maxConcurrency)
    {
        _inner = inner;
        _semaphore = new SemaphoreSlim(maxConcurrency);
    }

    public async Task<Result<ChatResponse>> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try { return await _inner.ChatAsync(request, ct); }
        finally { _semaphore.Release(); }
    }
}
```

### Configuration

```json
{
  "InferenceProviders": [
    {
      "Name": "Local vLLM",
      "Type": "Vllm",
      "Endpoint": "http://localhost:8000",
      "MaxConcurrency": 4,
      "QueueTimeout": "00:00:30"
    }
  ]
}
```

- `MaxConcurrency` — max simultaneous requests to this provider (default: 4)
- `QueueTimeout` — how long to wait in the semaphore queue before returning `Error.Unavailable` (default: 30s)
- Configurable at runtime via the provider management API

### Decorator Stack

Providers are wrapped in a decorator chain via DI:

```
IInferenceProvider
  └─ RateLimitedInferenceProvider    (concurrency control)
      └─ RecordingInferenceProvider  (history capture)
          └─ VllmProvider            (actual backend)
```

---

## Reproducibility Metadata

Every `InferenceRecord` captures full environment context — not just request/response.

```csharp
/// <summary>
/// Environment snapshot captured alongside every inference record.
/// Ensures reproducibility: two runs with "the same parameters" on different
/// vLLM versions or GPU types can produce different results.
/// </summary>
public sealed record EnvironmentSnapshot
{
    /// <summary>Provider version string from /health or /version endpoint.</summary>
    public string? ProviderVersion { get; init; }

    /// <summary>Inference engine type and version (e.g., "vLLM 0.4.2").</summary>
    public string? EngineVersion { get; init; }

    /// <summary>Model quantization format (e.g., "GPTQ-4bit", "AWQ", "FP16").</summary>
    public string? Quantization { get; init; }

    /// <summary>GPU type if reported by provider (e.g., "NVIDIA A100 80GB").</summary>
    public string? GpuType { get; init; }

    /// <summary>Number of GPUs / tensor parallel degree.</summary>
    public int? GpuCount { get; init; }

    /// <summary>Maximum context length configured on the provider.</summary>
    public int? MaxContextLength { get; init; }

    /// <summary>Hash of the system prompt used (for quick comparison).</summary>
    public string? SystemPromptHash { get; init; }

    /// <summary>Platform version string.</summary>
    public string PlatformVersion { get; init; }
}
```

Captured by `RecordingInferenceProvider` on each call. The provider's `/health` or `/version` response is cached (per `ICacheService`) and refreshed every 5 minutes.

---

## Generated TypeScript API Client

The frontend API client is auto-generated from the backend's OpenAPI spec. No hand-written fetch calls.

### Build Pipeline

```
Backend (.NET 9 native OpenAPI) → openapi.json → orval → typed TypeScript client
```

### Setup

```json
// orval.config.ts
{
  "ai-research": {
    "input": { "target": "../backend/src/AiResearch.Api/openapi.json" },
    "output": {
      "target": "./src/services/generated/api.ts",
      "client": "react-query",
      "mode": "tags-split"
    }
  }
}
```

### Developer Workflow

```bash
# In frontend/ — regenerate client after backend API changes
npm run api:generate   # runs: orval --config orval.config.ts

# In backend/ — export OpenAPI spec
dotnet run --project src/AiResearch.Api -- --export-openapi openapi.json
```

- `orval` generates typed hooks (TanStack Query) and client functions per API tag
- Tags match feature groups: `Playground`, `Experiments`, `History`, etc.
- Generated code lives in `src/services/generated/` — never edited manually
- `api:generate` is part of the build pipeline and CI checks for drift

### Feature API Usage (Generated)

```typescript
// Auto-generated by orval — no manual API code needed
import { useGetConversation, useStreamChat } from '@/services/generated/playground';

function PlaygroundPage() {
  const { data, isLoading } = useGetConversation(conversationId);
  // ...
}
```

Hand-written `*Api.ts` files in features are replaced by generated code. The `apiClient.ts` base wrapper remains for shared configuration (base URL, error interceptors, auth headers).

---

## Global Search

Full-text search across all entities using PostgreSQL's built-in `tsvector`.

### Implementation

```csharp
/// <summary>
/// Global search across all indexed entities: prompts, experiments, history records,
/// datasets, agents. Uses PostgreSQL tsvector for full-text search with ranking.
/// </summary>
public interface IGlobalSearch
{
    /// <summary>
    /// Search across all entity types. Results are ranked by relevance.
    /// </summary>
    Task<Result<GlobalSearchResults>> SearchAsync(
        string query, SearchOptions? options, CancellationToken ct);
}

public sealed record GlobalSearchResults(
    IReadOnlyList<SearchHit> Hits,
    int TotalCount,
    Dictionary<string, int> FacetCounts  // counts per entity type
);

public sealed record SearchHit(
    string EntityType,     // "prompt", "experiment", "history", "dataset"
    string EntityId,
    string Title,
    string Snippet,        // highlighted match context
    float Score,
    DateTime CreatedAt
);
```

- Search index is maintained via EF Core `HasGeneratedTsVectorColumn()` on indexed entities
- Entities opt-in to search by implementing `ISearchable` (defines which fields are indexed)
- A single `/api/v1/search?q=...&types=prompt,experiment` endpoint serves all search

---

## Annotation & Labeling

Inline annotations on inference results for evaluation and dataset curation.

```csharp
/// <summary>
/// An annotation on an inference record or dataset item.
/// Supports quality ratings, correctness labels, free-text notes, and custom tags.
/// Used by evaluation workflows and dataset curation.
/// </summary>
public sealed class Annotation : BaseEntity
{
    public Guid TargetId { get; init; }              // InferenceRecord ID or DatasetItem ID
    public string TargetType { get; init; }          // "inference_record" | "dataset_item"
    public string? Label { get; init; }              // "correct" | "incorrect" | "partial" | custom
    public int? Rating { get; init; }                // 1-5 quality rating
    public string? Notes { get; init; }              // Free-text annotation
    public List<string> Tags { get; init; } = [];    // Custom tags
    public string CreatedBy { get; init; }           // ICurrentUser.UserId
}
```

### API

```
POST   /api/v1/annotations                    # Create annotation
GET    /api/v1/annotations?targetId={id}       # Get annotations for a target
PUT    /api/v1/annotations/{id}                # Update annotation
DELETE /api/v1/annotations/{id}                # Delete annotation
GET    /api/v1/annotations/summary?targetType=inference_record  # Aggregate stats
```

Annotations feed into:
- **Evaluation**: labeled outputs become ground truth for scoring
- **Dataset curation**: annotated outputs can be exported as training data
- **Analytics**: annotation distributions show model quality trends

---

## Export System

Export research results in shareable formats for papers, presentations, and collaboration.

### Supported Formats

| Export Type | Formats | Source |
|------------|---------|--------|
| Experiment comparisons | LaTeX table, CSV, JSON | Experiment runs with metrics |
| Logprob heatmaps | SVG, PNG | Token probability visualizations |
| Evaluation results | CSV, JSON, LaTeX | Scorer outputs and leaderboards |
| Inference history | JSON, JSONL | Filtered history records |
| Static report | Single HTML file | Self-contained, no server needed |

### Static HTML Report

A single-file HTML export containing embedded CSS, JS, and data. Can be opened in any browser without the workbench running. Includes:
- Experiment summary with parameter table
- Results comparison (side-by-side outputs)
- Logprob heatmap visualizations (inline SVG)
- Metric charts (embedded Recharts or static SVG)
- Metadata: model, provider, timestamp, parameters

### API

```
POST /api/v1/export/experiment/{id}     # Export experiment in requested format
POST /api/v1/export/evaluation/{id}     # Export evaluation results
POST /api/v1/export/history             # Export filtered history records
POST /api/v1/export/report              # Generate static HTML report
```

---

## Use Case Management

Pre-built research workflows that guide users through common tasks. Also supports user-created use cases.

### Built-in Use Cases (Seed Data)

| Use Case | Description | Modules Used |
|----------|-------------|-------------|
| **Model Comparison** | Compare two models on the same prompts, analyze differences | Playground, History, Replay |
| **Prompt Optimization** | Iterate on a prompt template, A/B test variants, measure improvement | Prompt Lab, Experiments |
| **Hallucination Detection** | Use logprobs to identify low-confidence outputs, annotate for accuracy | Playground, Logprobs, Annotations |
| **RAG Pipeline Tuning** | Ingest docs, try chunking strategies, evaluate retrieval quality | RAG, Evaluation, Datasets |
| **Dataset Quality Audit** | Browse dataset, annotate samples, compute quality metrics | Datasets, Annotations, Analytics |

### Data Model

```csharp
/// <summary>
/// A research use case — a guided workflow with steps, prerequisites, and expected outcomes.
/// Built-in use cases are seeded on first launch. Users can create and share custom use cases.
/// </summary>
public sealed class UseCase : BaseEntity
{
    public string Title { get; init; }
    public string Description { get; init; }
    public string Category { get; init; }        // "comparison", "optimization", "analysis", etc.
    public bool IsBuiltIn { get; init; }         // Seeded use cases cannot be deleted
    public List<UseCaseStep> Steps { get; init; } = [];
    public List<string> RequiredModules { get; init; } = [];
    public string? SeedDataKey { get; init; }    // Key to load sample data for this use case
}

public sealed class UseCaseStep
{
    public int Order { get; init; }
    public string Title { get; init; }
    public string Instructions { get; init; }    // Markdown
    public string TargetModule { get; init; }    // Which module to navigate to
    public string? TargetAction { get; init; }   // Optional: pre-fill action in that module
    public bool IsCompleted { get; set; }
}
```

### Seed Data

On first launch (empty database), the platform seeds:
- 5 built-in use cases with step-by-step instructions
- Sample prompts for each use case
- A small dataset (100 records) for evaluation/annotation use cases
- Pre-configured experiment templates

Seed data is applied via `IDataSeeder` in `Common/Database/Seeders/`.

---

## Middleware Pipeline

### Request Pipeline Order

```csharp
/// <summary>
/// Configures the HTTP request pipeline with all middleware in the correct order.
/// Order matters: correlation ID must be first, exception handler wraps everything,
/// logging captures the final status code.
/// </summary>
public static WebApplication ConfigureMiddleware(this WebApplication app)
{
    // 1. Correlation ID — first, so all downstream logs include it
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 2. Global exception handler — catches anything unhandled
    app.UseMiddleware<GlobalExceptionMiddleware>();

    // 3. Request logging — structured log per request
    app.UseMiddleware<RequestLoggingMiddleware>();

    // 4. CORS
    app.UseCors("AllowFrontend");

    // 5. Feature endpoints
    app.MapFeatureEndpoints();

    return app;
}
```

### Correlation ID

```csharp
/// <summary>
/// Ensures every request has a correlation ID for distributed tracing.
/// If the request includes X-Correlation-Id header, it is used.
/// Otherwise, a new GUID is generated. The ID is added to the response
/// headers and to the Serilog log context for all downstream logs.
/// </summary>
public sealed class CorrelationIdMiddleware { ... }
```

### Request Logging

```csharp
/// <summary>
/// Logs every HTTP request with structured properties:
/// Method, Path, StatusCode, Duration, CorrelationId, ContentLength.
/// Uses Serilog's request logging with enriched properties.
/// Excludes health check endpoints from logging to reduce noise.
/// </summary>
public sealed class RequestLoggingMiddleware { ... }
```

### Global Exception Handler

```csharp
/// <summary>
/// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
/// Logs the full exception with stack trace at Error level.
/// Returns 500 Internal Server Error with a sanitized message (no stack traces in production).
/// </summary>
public sealed class GlobalExceptionMiddleware { ... }
```

---

## Logging

Serilog with structured logging throughout.

### Configuration

```csharp
/// <summary>
/// Configures Serilog for the application:
/// - Console sink with compact JSON format (development)
/// - File sink with rolling daily logs (always)
/// - Enriched with: CorrelationId, MachineName, Environment, ThreadId
/// - Minimum level: Information (Debug in development)
/// - Override: Microsoft.AspNetCore = Warning (suppress framework noise)
/// </summary>
public static class SerilogConfiguration
{
    public static LoggerConfiguration Configure(
        LoggerConfiguration config, IConfiguration appConfig) { ... }
}
```

### Conventions

```csharp
// DO: Structured log with named properties
Log.Information("Inference completed for {Model} in {LatencyMs}ms ({TokensPerSecond} tok/s)",
    response.Model, response.LatencyMs, response.TokensPerSecond);

// DO: Include correlation context for tracing
Log.ForContext("RunId", run.Id)
   .Information("Experiment run saved with {MetricCount} metrics", run.Metrics.Count);

// DON'T: String interpolation (loses structured properties)
Log.Information($"Inference completed for {model} in {latency}ms");  // NO
```

---

## XML Documentation Requirements

Every public member must have XML documentation. This is enforced by `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<DocumentationFile>` in the .csproj.

### Standards

```csharp
/// <summary>
/// [WHAT] One sentence describing what this type/method does.
/// [WHY] Optional second sentence on why it exists or when to use it.
/// </summary>
/// <param name="name">[WHAT] What this parameter represents. Include valid ranges/formats.</param>
/// <returns>[WHAT] What the return value contains on success and failure.</returns>
/// <exception cref="T">[WHEN] Only for unexpected exceptions (not Result failures).</exception>
/// <example>
/// <code>
/// // Concise usage example
/// var result = await handler.HandleAsync(new Query(id), ct);
/// </code>
/// </example>
/// <remarks>
/// [Optional] Implementation notes, performance characteristics, or caveats.
/// </remarks>
```

### Examples

```csharp
/// <summary>
/// Computes perplexity from a sequence of token log probabilities.
/// Lower perplexity indicates higher model confidence in its output.
/// </summary>
/// <param name="logprobs">
/// Per-token log probabilities (base e). Must contain at least one token.
/// Values are typically negative (e.g., -0.1 for high confidence, -5.0 for low).
/// </param>
/// <returns>
/// The perplexity score as exp(mean(-logprob)). Returns <see cref="Result{T}.Failure"/>
/// with a Validation error if <paramref name="logprobs"/> is empty.
/// </returns>
/// <example>
/// <code>
/// var logprobs = new[] { -0.1, -0.5, -0.2, -0.3 };
/// var result = LogprobsCalculator.ComputePerplexity(logprobs);
/// // result.Value == ~1.32 (low perplexity = high confidence)
/// </code>
/// </example>
public static Result<double> ComputePerplexity(ReadOnlySpan<double> logprobs) { ... }
```

---

## Frontend UX Standards

### Design System

| Principle | Implementation |
|-----------|---------------|
| **Consistent spacing** | Tailwind spacing scale only (space-1 through space-12). No arbitrary pixel values. |
| **Keyboard navigable** | All interactive elements focusable. Tab order logical. Escape closes modals/panels. |
| **Loading states** | Every async operation shows: skeleton on initial load, spinner on subsequent loads, optimistic updates where safe. |
| **Error states** | Toast for transient errors (network, timeout). Inline for validation errors. Full-page for fatal (lost connection). |
| **Empty states** | Never a blank screen. Always a message + call-to-action ("No experiments yet. Create your first one.") |
| **Responsive** | Min width 1280px (research tool, not mobile). Sidebar collapsible for more content space. Panes resize with drag handles. |
| **Dark mode** | Default. Light mode available. All custom components support both via CSS variables. |
| **Accessibility** | ARIA labels on all icon buttons. Screen reader text for status indicators. Sufficient contrast ratios. |

### Interaction Patterns

| Pattern | Standard |
|---------|----------|
| **Data tables** | Sortable columns (click header), filterable (filter bar), paginated (25/50/100 per page), row selection with checkbox. |
| **Forms** | Validate on blur, show error inline, disable submit until valid. Use React Hook Form + Zod schemas. |
| **Modals** | For destructive confirmations and short forms only. Never for browsing data. |
| **Panels** | For detail views. Slide in from right or expand below. Don't block the parent view. |
| **Streaming text** | Token-by-token append. Auto-scroll to bottom. "Stop" button visible during stream. Smooth cursor animation. |
| **Charts** | Recharts. Consistent color palette. Tooltips on hover. Legend when >2 series. Responsive width. |

### State Management

| Concern | Tool |
|---------|------|
| Server state (API data) | TanStack Query — caching, refetching, optimistic updates |
| Global client state | Zustand — selected model, active project, UI preferences |
| Form state | React Hook Form + Zod validation |
| URL state | React Router search params — filters, pagination, selected tabs |
| Local preferences | localStorage — sidebar collapsed, dark mode, default parameters |

### API Client Pattern

```typescript
/**
 * Typed API client for a feature. All methods return Result<T> matching backend pattern.
 * Error responses are parsed into structured Error objects.
 *
 * @example
 * ```ts
 * const result = await playgroundApi.getConversation(id);
 * if (result.isSuccess) {
 *   setConversation(result.value);
 * } else {
 *   toast.error(result.error.message);
 * }
 * ```
 */

// services/types/result.ts
export type Result<T> =
  | { isSuccess: true; value: T }
  | { isSuccess: false; error: ApiError };

export type ApiError = {
  code: string;
  message: string;
  type: 'Validation' | 'NotFound' | 'Conflict' | 'Internal' | 'Unavailable';
};

// Each feature has a typed API client
// features/playground/api/playgroundApi.ts
export const playgroundApi = {
  /** Retrieves a conversation by ID with all messages. */
  getConversation: (id: string, includeLogprobs?: boolean): Promise<Result<ConversationDto>> =>
    apiClient.get(`/api/v1/playground/conversations/${id}`, { includeLogprobs }),

  /** Sends a message and returns an SSE stream of response tokens. */
  streamChat: (request: SendMessageRequest): EventSource =>
    sseClient.post('/api/v1/inference/chat', request),
};
```

---

## Dependency Injection Registration

Each feature registers itself via an extension method. The composition root (Program.cs) calls them all.

```csharp
/// <summary>
/// Registers all services for the application. Each feature slice registers independently.
/// Common infrastructure is registered first, then features layer on top.
/// </summary>
public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration config)
{
    // Common infrastructure
    services.AddCommonDatabase(config);       // EF Core + provider swap (ADR-008)
    services.AddCommonCache(config);          // ICacheService (ADR-003)
    services.AddCommonStorage(config);        // IFileStorage (ADR-004)
    services.AddCommonAuth(config);           // IAuthProvider + ICurrentUser (ADR-005)
    services.AddCommonInference(config);      // IInferenceProvider + decorators (ADR-006)
    services.AddCommonVectorStore(config);    // IVectorStore (ADR-009)
    services.AddCommonSearch();               // IGlobalSearch (tsvector)
    services.AddCommonExport();               // IExportService
    services.AddCommonLogging(config);        // Serilog + OTel
    services.AddCommonJobs();

    // Feature slices — each registers its own handlers, repositories, validators
    services.AddPlaygroundFeature();
    services.AddTokenExplorerFeature();
    services.AddPromptsFeature();
    services.AddExperimentsFeature();
    services.AddDatasetsFeature();
    services.AddEvaluationFeature();
    services.AddRagFeature();
    services.AddAgentsFeature();
    services.AddModelsFeature();
    services.AddBatchInferenceFeature();
    services.AddAnalyticsFeature();
    services.AddSkillsFeature();

    return services;
}

/// <summary>
/// Maps all feature endpoint groups. Each feature defines its own route group.
/// </summary>
public static WebApplication MapFeatureEndpoints(this WebApplication app)
{
    app.MapPlaygroundEndpoints();       // /api/v1/playground/*
    app.MapTokenExplorerEndpoints();    // /api/v1/token-explorer/*
    app.MapPromptEndpoints();           // /api/v1/prompts/*
    app.MapExperimentEndpoints();       // /api/v1/experiments/*
    app.MapDatasetEndpoints();          // /api/v1/datasets/*
    app.MapEvaluationEndpoints();       // /api/v1/evaluation/*
    app.MapRagEndpoints();              // /api/v1/rag/*
    app.MapAgentEndpoints();            // /api/v1/agents/*
    app.MapModelEndpoints();            // /api/v1/models/*
    app.MapBatchEndpoints();            // /api/v1/batch/*
    app.MapAnalyticsEndpoints();        // /api/v1/analytics/*
    app.MapSkillEndpoints();            // /api/v1/skills/*
    app.MapNotebookEndpoints();         // /api/v1/notebooks/*
    app.MapHistoryEndpoints();          // /api/v1/history/*
    app.MapSearchEndpoints();           // /api/v1/search/*
    app.MapAnnotationEndpoints();       // /api/v1/annotations/*
    app.MapExportEndpoints();           // /api/v1/export/*
    app.MapUseCaseEndpoints();          // /api/v1/use-cases/*

    return app;
}
```

---

## Testing Strategy

| Layer | What | How |
|-------|------|-----|
| **Domain** | Entity invariants, value object validation | xUnit, no dependencies |
| **Application** | Handler logic, Result outcomes, validation | xUnit + NSubstitute mocks for repositories/providers |
| **Infrastructure** | DB queries, provider HTTP calls | Integration tests with Testcontainers (PostgreSQL) + WireMock (vLLM) |
| **API** | Endpoint routing, HTTP status codes, serialization | WebApplicationFactory + HttpClient |
| **Frontend** | Component rendering, user interaction, API integration | Vitest + Testing Library. MSW for API mocking. |

### Fake Inference Provider (for tests)

```csharp
/// <summary>
/// A deterministic inference provider for unit and integration tests.
/// Returns configurable responses without calling any external service.
/// Supports logprobs generation with predictable values.
/// </summary>
public sealed class FakeInferenceProvider : IInferenceProvider
{
    /// <summary>
    /// Queue a response that will be returned on the next ChatAsync call.
    /// </summary>
    public void EnqueueResponse(ChatResponse response) { ... }

    /// <summary>
    /// Queue an error that will be returned on the next ChatAsync call.
    /// </summary>
    public void EnqueueError(Error error) { ... }
}
```
