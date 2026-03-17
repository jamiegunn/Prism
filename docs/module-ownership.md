# Prism — Module Ownership Map

This document maps each Prism module to its backend feature slice, frontend feature directory, primary domain entities, and key dependencies.

## Core Infrastructure

| Module | Backend Location | Key Interfaces | Owner |
|--------|-----------------|----------------|-------|
| Inference Runtime | `Prism.Common/Inference/Runtime/` | `IInferenceRuntime`, `IInferenceRecorder`, `ITokenAnalysisService`, `IReplayService` | Shared |
| Provider Capabilities | `Prism.Common/Inference/Capabilities/` | `IProviderCapabilityRegistry` | Shared |
| Artifact Store | `Prism.Common/Storage/` | `IArtifactStore` | Shared |
| Job System | `Prism.Common/Jobs/` | `IJobQueue`, `IJobRunner`, `IJobStore` | Shared |
| Results | `Prism.Common/Results/` | `Result<T>`, `Error` | Shared |
| Database | `Prism.Common/Database/` | `AppDbContext` | Shared |
| Search | `Prism.Common/Search/` | `IGlobalSearch` | Shared |
| Auth | `Prism.Common/Auth/` | `IAuthProvider`, `ICurrentUser` | Shared |
| Cache | `Prism.Common/Cache/` | `ICacheService` | Shared |
| Storage | `Prism.Common/Storage/` | `IFileStorage` | Shared |
| Vector Store | `Prism.Common/VectorStore/` | `IVectorStore` | Shared |

## Feature Modules

| Module | Phase | Backend Slice | Frontend Feature | Primary Entities | Runtime Deps |
|--------|-------|--------------|-----------------|-----------------|--------------|
| **Playground** | 1 | `Prism.Features/Playground/` | `frontend/src/features/playground/` | `PlaygroundSession`, `PlaygroundMessage` | `IInferenceRuntime`, `IInferenceRecorder` |
| **Token Explorer** | 1 | `Prism.Features/TokenExplorer/` | `frontend/src/features/token-explorer/` | `TokenExplorerSession`, `TokenBranch`, `TokenStep` | `IInferenceRuntime`, `ITokenAnalysisService` |
| **Tokenizer Explorer** | 1 | (uses Models/Inference common) | `frontend/src/features/tokenizer/` | (no persisted entities) | `IProviderCapabilityRegistry` |
| **Models** | 1 | `Prism.Features/Models/` | `frontend/src/features/models/` | `ProviderInstance`, `ModelSnapshot` | `IProviderCapabilityRegistry` |
| **History & Replay** | 1 | `Prism.Features/History/` | `frontend/src/features/history/` | `InferenceRun`, `InferenceTrace`, `TokenEvent`, `ReplayRun` | `IInferenceRuntime`, `IReplayService` |
| **Prompt Lab** | 2 | `Prism.Features/PromptLab/` | `frontend/src/features/prompt-lab/` | `PromptTemplate`, `PromptVersion` | `IInferenceRuntime` |
| **Experiments** | 2 | `Prism.Features/Experiments/` | `frontend/src/features/experiments/` | `Experiment`, `ExperimentRun` | `IInferenceRuntime`, `IJobQueue` |
| **Datasets** | 3 | `Prism.Features/Datasets/` | `frontend/src/features/datasets/` | `Dataset`, `DatasetVersion`, `DatasetRecord` | `IArtifactStore` |
| **Evaluation** | 3 | `Prism.Features/Evaluation/` | `frontend/src/features/evaluation/` | `EvaluationSuite`, `EvaluationRun`, `MetricResult` | `IJobQueue`, `IInferenceRuntime` |
| **Batch Inference** | 3 | `Prism.Features/BatchInference/` | `frontend/src/features/batch-inference/` | `BatchJob` | `IJobQueue`, `IJobRunner`, `IInferenceRuntime` |
| **Analytics** | 3 | `Prism.Features/Analytics/` | `frontend/src/features/analytics/` | (reads from other entities) | `AppDbContext` (read-only queries) |
| **RAG Workbench** | 4 | `Prism.Features/Rag/` | `frontend/src/features/rag/` | `KnowledgeCollection`, `KnowledgeDocument`, `KnowledgeChunk`, `RetrievalTrace` | `IVectorStore`, `IJobQueue`, `IInferenceRuntime` |
| **Structured Output** | 4 | `Prism.Features/StructuredOutput/` | `frontend/src/features/structured-output/` | `SchemaDefinition`, `StructuredRun` | `IInferenceRuntime`, `IProviderCapabilityRegistry` |
| **Agents** | 5 | `Prism.Features/Agents/` | `frontend/src/features/agents/` | `AgentDefinition`, `AgentRun`, `AgentStep` | `IInferenceRuntime`, `IJobQueue` |
| **Notebooks** | 5 | `Prism.Features/Notebooks/` | `frontend/src/features/notebooks/` | `NotebookAsset` | `IArtifactStore` |
| **Fine-Tuning** | 5 | `Prism.Features/FineTuning/` | `frontend/src/features/fine-tuning/` | `FineTuneJob` | `IJobQueue`, `IArtifactStore` |

## Cross-Cutting Entities

| Entity | Scope | Used By |
|--------|-------|---------|
| `Workspace` | Organization root | All modules (Phase 2+) |
| `Project` | Work grouping | All modules (Phase 2+) |
| `Artifact` | Output tracking | All modules that produce exportable output |
| `Annotation` | Inline labeling | Playground, History, Datasets, Evaluation |
| `Job` | Background work | Batch, Evaluation, RAG Ingest, Agents, Fine-Tuning |

## Dependency Rules

1. Features depend on `Prism.Common` — never on each other.
2. All inference goes through `IInferenceRuntime` — never directly to `IInferenceProvider`.
3. All long-running work goes through `IJobQueue` — never fire-and-forget.
4. All exportable outputs go through `IArtifactStore` — never direct file writes.
5. All provider capability checks go through `IProviderCapabilityRegistry` — never name-based branching.
