# Phase 3 (Run) — Implementation Plan

## Overview

Phase 3 adds four modules to Prism:

1. **Dataset Manager** — Upload, browse, split, export datasets for evaluation and fine-tuning
2. **Evaluation Suite** — Score model outputs against datasets with pluggable metrics
3. **Batch Inference** — Large-scale processing with progress tracking and retries
4. **Analytics Dashboard** — Usage tracking, cost breakdown, performance metrics

**Infrastructure change:** Add Redis for job queue (swap InMemoryJobQueue → RedisJobQueue).

## Execution Order

| Step | Module | Scope | Status |
|------|--------|-------|--------|
| 1 | Datasets Backend | Domain, EF config, migration, CRUD handlers, upload/parse, stats, split, export | COMPLETED |
| 2 | Evaluation Backend | Domain, scoring methods (IScoringMethod), orchestrator, results endpoints | COMPLETED |
| 3 | Batch Inference Backend | Domain, job orchestrator, background worker, API endpoints | COMPLETED |
| 4 | Analytics Backend | UsageLog entity, middleware, aggregation endpoints | COMPLETED |
| 5 | Redis Integration | RedisCacheService, RedisJobQueue, docker-compose, config-driven swap | COMPLETED |
| 6 | Frontend — Datasets | Upload, browse, schema, stats, split, export | COMPLETED |
| 7 | Frontend — Evaluation | Setup, progress, results dashboard, leaderboard | COMPLETED |
| 8 | Frontend — Batch Inference | Job creation, monitoring dashboard | COMPLETED |
| 9 | Frontend — Analytics | Summary cards, time series, cost/latency charts | COMPLETED |
| 10 | Integration & Polish | Cross-feature navigation, sidebar updates, build verification | COMPLETED |

## Domain Models

### Datasets Feature (`Features/Datasets/`)

**Dataset** (aggregate root):
- Id, ProjectId (nullable FK), Name (200), Description (2000 nullable)
- Format: `DatasetFormat` enum (Csv, Json, Jsonl, Parquet)
- Schema (JSONB — column definitions), RecordCount (int), SizeBytes (long), Version (int)
- Table: `datasets_datasets`, Index: ProjectId, Name

**DatasetRecord**:
- Id, DatasetId (FK), Data (JSONB), SplitLabel (string nullable — "train"/"test"/"val"), OrderIndex (int)
- Table: `datasets_records`, GIN index on Data, composite index on (DatasetId, SplitLabel)

**DatasetSplit**:
- Id, DatasetId (FK), Name (100), RecordCount (int)
- Table: `datasets_splits`, unique index on (DatasetId, Name)

**DatasetFormat**: Csv, Json, Jsonl, Parquet

**ColumnSchema** (value object): Name, Type ("string"/"number"/"boolean"/"array"/"object"), Purpose ("input"/"output"/"label"/"metadata"/null)

### Evaluation Feature (`Features/Evaluation/`)

**Evaluation** (aggregate root):
- Id, ProjectId (nullable FK), DatasetId (FK), SplitLabel (nullable), Name (200)
- Models (JSONB string[]), PromptVersionId (nullable FK)
- ScoringMethods (JSONB string[]), Config (JSONB), Status (`EvaluationStatus`), Progress (double)
- Table: `evaluation_evaluations`, Index: ProjectId, DatasetId

**EvaluationResult**:
- Id, EvaluationId (FK), Model (200), RecordId (FK to DatasetRecord)
- Input (text), ExpectedOutput (text nullable), ActualOutput (text nullable)
- Scores (JSONB Dict<string,double>), LogprobsData (text nullable), Perplexity (double nullable), LatencyMs (long)
- Table: `evaluation_results`, composite index (EvaluationId, Model), GIN index on Scores

**EvaluationStatus**: Pending, Running, Paused, Completed, Failed, Cancelled

**IScoringMethod** interface:
```csharp
public interface IScoringMethod
{
    string Name { get; }
    Task<double> ScoreAsync(string input, string expected, string actual, CancellationToken ct);
}
```

Implementations: ExactMatchScorer, RougeLScorer, BleuScorer, PerplexityScorer, LlmJudgeScorer

### Batch Inference Feature (`Features/BatchInference/`)

**BatchJob** (aggregate root):
- Id, DatasetId (FK), SplitLabel (nullable), Model (200), PromptVersionId (nullable FK)
- Parameters (JSONB RunParameters), Concurrency (int), MaxRetries (int), CaptureLogprobs (bool)
- Status (`BatchJobStatus`), Progress (double), TotalRecords, CompletedRecords, FailedRecords
- TokensUsed (long), Cost (decimal nullable), StartedAt (nullable), FinishedAt (nullable), OutputPath (nullable)
- Table: `batch_jobs`, Index: Status

**BatchResult**:
- Id, BatchJobId (FK), RecordId (FK to DatasetRecord)
- Input (text), Output (text nullable), LogprobsData (text nullable), Perplexity (double nullable)
- TokensUsed (int), LatencyMs (long), Status (`BatchResultStatus`), Error (text nullable), Attempt (int)
- Table: `batch_results`, composite index (BatchJobId, Status)

**BatchJobStatus**: Queued, Running, Paused, Completed, Failed, Cancelled
**BatchResultStatus**: Pending, Success, Failed, Retry

### Analytics Feature (`Features/Analytics/`)

**UsageLog**:
- Id, Model (200), PromptTokens (int), CompletionTokens (int)
- LatencyMs (long), TtftMs (int nullable), TokensPerSecond (double nullable)
- SourceModule (string — "playground"/"prompt-lab"/"evaluation"/"batch"/"agent")
- ProjectId (nullable), Cost (decimal nullable)
- Table: `analytics_usage_logs`, Index: CreatedAt, SourceModule, Model

## API Endpoints

### Datasets (`/api/v1/datasets`)

| Endpoint | Method | Handler |
|----------|--------|---------|
| `/` | POST (multipart) | UploadDatasetHandler |
| `/` | GET | ListDatasetsHandler |
| `/{id}` | GET | GetDatasetHandler |
| `/{id}` | PUT | UpdateDatasetHandler |
| `/{id}` | DELETE | DeleteDatasetHandler |
| `/{id}/records` | GET | ListRecordsHandler |
| `/{id}/records/{recordId}` | PUT | UpdateRecordHandler |
| `/{id}/split` | POST | SplitDatasetHandler |
| `/{id}/stats` | GET | GetDatasetStatsHandler |
| `/{id}/export` | POST | ExportDatasetHandler |

### Evaluation (`/api/v1/evaluation`)

| Endpoint | Method | Handler |
|----------|--------|---------|
| `/` | POST | StartEvaluationHandler |
| `/` | GET | ListEvaluationsHandler |
| `/{id}` | GET | GetEvaluationHandler |
| `/{id}/cancel` | POST | CancelEvaluationHandler |
| `/{id}/results` | GET | GetEvaluationResultsHandler |
| `/{id}/results/records` | GET | GetResultRecordsHandler |
| `/{id}/results/export` | GET | ExportResultsHandler |
| `/leaderboard` | GET | GetLeaderboardHandler |

### Batch Inference (`/api/v1/batch`)

| Endpoint | Method | Handler |
|----------|--------|---------|
| `/` | POST | CreateBatchJobHandler |
| `/` | GET | ListBatchJobsHandler |
| `/{id}` | GET | GetBatchJobHandler |
| `/{id}/pause` | POST | PauseBatchJobHandler |
| `/{id}/resume` | POST | ResumeBatchJobHandler |
| `/{id}/cancel` | POST | CancelBatchJobHandler |
| `/{id}/results` | GET | GetBatchResultsHandler |
| `/{id}/download` | GET | DownloadBatchResultsHandler |
| `/{id}/retry-failed` | POST | RetryFailedHandler |
| `/estimate` | POST | EstimateBatchCostHandler |

### Analytics (`/api/v1/analytics`)

| Endpoint | Method | Handler |
|----------|--------|---------|
| `/usage` | GET | GetUsageHandler |
| `/costs` | GET | GetCostBreakdownHandler |
| `/performance` | GET | GetPerformanceHandler |

## Database Changes

**4 migrations expected:**
1. `AddDatasetsFeature` — datasets_datasets, datasets_records, datasets_splits
2. `AddEvaluationFeature` — evaluation_evaluations, evaluation_results
3. `AddBatchInferenceFeature` — batch_jobs, batch_results
4. `AddAnalyticsFeature` — analytics_usage_logs

(May combine into fewer migrations for efficiency.)

## New Dependencies

| Package | Layer | Purpose |
|---------|-------|---------|
| `StackExchange.Redis` | Backend | Redis client for cache + job queue |
| None | Frontend | All chart packages already installed (recharts) |

## Key Patterns

- **IFileStorage** for dataset upload storage — files go to local storage, entities to DB
- **JSONB + GIN indexes** for flexible dataset record data and evaluation scores
- **IJobQueue** for evaluation and batch orchestration — swap to Redis in Step 5
- **IScoringMethod** pluggable interface — each scorer is a separate class
- **BackgroundService** for batch worker — dequeues from IJobQueue, processes with concurrency limit
- **UsageLogging middleware** — lightweight INSERT on every inference call
