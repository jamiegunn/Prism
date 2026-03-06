# AI Research Workbench — Design Document

## Vision

A comprehensive AI research platform built around **vLLM** that provides a unified interface for prompt engineering, model evaluation, experiment tracking, dataset management, RAG experimentation, agent building, and inference analytics. Think "Jupyter meets MLflow meets Postman — but purpose-built for LLM research."

**Design philosophy:** Start lean, add infrastructure only when pain is felt. Every feature earns its place by solving a real research workflow friction.

---

## Tech Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Frontend** | React + TypeScript + Vite | Rich interactive UI, Monaco editor for prompts, Recharts for viz |
| **UI Framework** | Tailwind CSS + shadcn/ui | Rapid prototyping, consistent design system |
| **Backend API** | .NET 9 Minimal API | Strong typing, excellent perf, familiar stack |
| **Streaming** | Server-Sent Events (SSE) | Lightweight, matches vLLM's native SSE output. No extra infra. |
| **Database** | PostgreSQL | Experiment tracking, datasets, structured results |
| **Vector Store** | pgvector (PostgreSQL ext) | RAG experiments without extra infra (Phase 4) |
| **Cache/Queue** | Redis | Job queuing, result caching (Phase 3 — not needed initially) |
| **AI Inference** | vLLM, Ollama, LM Studio (abstracted) | Provider-agnostic via `IInferenceProvider`. Swap backends without touching features. |
| **File Storage** | Local filesystem | Datasets, exports, artifacts. Simple and sufficient for single-user. |
| **Containerization** | Docker Compose | One-command local dev environment |
| **Notebooks** | JupyterLite (embedded) | Proven notebook UX — embed rather than rebuild (Phase 5) |

### Infrastructure Progression

| Phase | What's Running |
|-------|---------------|
| **Phase 1** | .NET API + PostgreSQL + React (3 processes, or 2 containers + Vite dev server) |
| **Phase 3** | Add Redis for batch job queuing |
| **Phase 4** | Enable pgvector extension for RAG |
| **Phase 5** | Add SignalR for bidirectional agent communication |

**Intentionally deferred:**
- **MinIO/S3:** Local filesystem is sufficient for a single-user research tool. Revisit only if multi-node deployment becomes a goal.
- **SignalR:** SSE handles token streaming. SignalR is only needed when we need server-initiated pushes (agent step updates, collaborative editing). Added in Phase 5.
- **Redis:** .NET `BackgroundService` + `Channel<T>` handles early async work. Redis added in Phase 3 when batch inference needs a proper job queue.

---

## Authentication & Multi-User

**Single-user by default.** No authentication for the initial build. This is a personal research workstation.

However, all data models include an optional nullable `user_id` field so multi-user support is not a painful retrofit if collaborators are added later.

---

## Application Structure

**Architecture:** Vertical Slice + Clean Architecture per feature. See `ARCHITECTURE.md` for full details.

```
AI_Research/
├── backend/
│   ├── src/
│   │   ├── AiResearch.Api/               # Startup, middleware, DI composition root
│   │   ├── AiResearch.Common/            # Shared kernel: Result<T>, abstractions, DB, cache,
│   │   │                                 #   inference provider abstraction, logging, jobs
│   │   ├── AiResearch.Features/          # Vertical slices — one folder per feature
│   │   │   ├── Playground/               #   Each slice: Domain/ Application/ Infrastructure/ Api/
│   │   │   ├── TokenExplorer/
│   │   │   ├── Prompts/
│   │   │   ├── Experiments/
│   │   │   ├── Datasets/
│   │   │   ├── Evaluation/
│   │   │   ├── Rag/
│   │   │   ├── Agents/
│   │   │   ├── Models/
│   │   │   ├── BatchInference/
│   │   │   ├── Analytics/
│   │   │   ├── Notebooks/
│   │   │   ├── FineTuning/
│   │   │   ├── StructuredOutput/
│   │   │   └── Skills/                   # Skill system (thin wrappers over feature handlers)
│   │   └── AiResearch.Tests/
│   └── backend.sln
├── frontend/                              # React + TypeScript + Vite
│   └── src/
│       ├── app/                           # Shell, routing, providers
│       ├── components/                    # Shared: ui/, layout/, logprobs/, charts/, chat/
│       ├── features/                      # Mirrors backend slices
│       │   └── playground/                #   Each: Page, components, hooks/, api/
│       ├── hooks/                         # Shared hooks (useSSE, useDebounce)
│       ├── services/                      # API client, SSE client, shared types
│       └── lib/                           # Utilities
├── docker-compose.yml
├── DESIGN.md
├── ARCHITECTURE.md                        # Full backend/frontend structure, patterns, standards
├── AGENTS.md                              # Agent system architecture
├── SKILLS.md                              # Skill registry and definitions
└── PROJECT_PLAN.md                        # Phased task breakdown
```

**Key patterns:**
- `Result<T>` return type on all application-layer operations (errors are values, not exceptions)
- `IInferenceProvider` abstraction — swap vLLM/Ollama/LM Studio without touching features
- XML doc comments on all public types and methods (enforced by compiler)
- Structured logging via Serilog with correlation IDs on every request
- `ICacheService` abstraction — InMemoryCache (Phase 1-2), Redis (Phase 3+)
- `IJobQueue<T>` abstraction — Channel<T> (Phase 1-2), Redis (Phase 3+)

---

## Feature Modules

---

### 1. INFERENCE PLAYGROUND

The core interactive workspace for talking to models.

**Capabilities:**
- Chat & completion modes
- Multi-model side-by-side comparison (2-4 panes)
- Streaming token output via SSE
- Full parameter control (temperature, top_p, top_k, max_tokens, stop sequences, frequency/presence penalty, repetition penalty)
- System prompt library with quick-swap
- Token usage display (prompt/completion/total) with cost estimation
- Response time & tokens-per-second metrics
- Save any conversation as an experiment run
- Export conversations (JSON, Markdown, JSONL)
- Fork conversations at any message point
- Image/multimodal input support (if model supports it)

**Token Probability & Logprobs (Research-Critical):**
- Toggle `logprobs` and `top_logprobs` (top-N) per request
- **Token heatmap view:** color each token by its log probability (green = high confidence, red = low confidence)
- **Alternative tokens panel:** click any token to see top-K alternatives with their probabilities
- **Perplexity score** per response (computed from logprobs)
- **Entropy visualization:** per-token entropy showing where the model is uncertain
- **Probability distribution chart:** bar chart of top-N token probabilities at any position
- **Surprise highlighting:** auto-flag tokens where the model's confidence was below a threshold
- **Comparison mode:** view logprobs side-by-side across models or temperature settings to see how confidence shifts
- **Export logprobs data** as JSON/CSV for external analysis

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  AI Research Workbench          [Playground] [Experiments] [Prompts] ...   │
├──────────────┬──────────────────────────────────────────────────────────────┤
│              │  ┌─ Model A: llama-3.1-70b ──┐  ┌─ Model B: mixtral-8x7b ─┐│
│  PARAMETERS  │  │                           │  │                          ││
│              │  │  System: You are a helpful │  │  System: You are a helpf ││
│  Model: [▼]  │  │  assistant...              │  │  ul assistant...         ││
│              │  │                           │  │                          ││
│  Temp: 0.7   │  │  User: Explain quantum    │  │  User: Explain quantum   ││
│  ├──●──────┤ │  │  computing in simple terms │  │  computing in simple ter ││
│              │  │                           │  │  ms                      ││
│  Top-p: 0.9  │  │  Assistant: Quantum comp- │  │  Assistant: Imagine you  ││
│  Top-k: 50   │  │  uting uses qubits...     │  │  have a coin that...     ││
│  Max: 2048   │  │                           │  │                          ││
│              │  │  [Tokens: 847 │ 42 tok/s] │  │  [Tokens: 923 │ 67 tok/s]││
│  ── LOGPROBS │  │  [Perplexity: 3.2]        │  │  [Perplexity: 4.1]       ││
│              │  ├───────────────────────────┤  ├──────────────────────────┤│
│  Enable: [✓] │  │  ▼ Token Probabilities    │  │  ▼ Token Probabilities   ││
│  Top-K: [5]  │  │                           │  │                          ││
│              │  │  Quantum comp-  uting uses │  │  Imagine you  have a     ││
│  Stop Seq:   │  │  ██████░ █████░ ████░░ ██ │  │  █████░░ ████░ █████░░  ││
│  [          ]│  │                           │  │                          ││
│              │  │  Click token for details:  │  │  Click token for details ││
│              │  │  "qubits" (p=0.82)        │  │                          ││
│              │  │   ├─ qubits    0.82       │  │                          ││
│              │  │   ├─ quantum   0.09       │  │                          ││
│              │  │   ├─ special   0.04       │  │                          ││
│              │  │   ├─ super     0.02       │  │                          ││
│              │  │   └─ unique    0.01       │  │                          ││
│              │  └───────────────────────────┘  └──────────────────────────┘│
│  [Save Run]  │                                                             │
│  [Export]    │  [+ Add Pane]  [Link Inputs]  [Compare Outputs]            │
└──────────────┴──────────────────────────────────────────────────────────────┘
```

---

### 2. PROMPT ENGINEERING LAB

Version-controlled prompt development with testing and optimization.

**Capabilities:**
- Monaco-based prompt editor with syntax highlighting for template variables
- Prompt versioning with diff view (like Git for prompts)
- Template variables with `{{variable}}` syntax
- Prompt chains / pipelines (output of one -> input of next)
- A/B testing framework: run N variations x M models x K parameter sets
- Prompt scoring (manual + automated via judge LLM)
- Prompt library with tagging, search, and categories
- Few-shot example management (drag-and-drop ordering)
- Token count preview as you type
- Import/export prompt collections (JSON, YAML)
- **Logprobs integration:** see per-token confidence for prompt outputs, identify where prompts cause model uncertainty

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PROMPT ENGINEERING LAB                                                    │
├────────────────────┬────────────────────────────────────────────────────────┤
│                    │  ┌─ Prompt Editor ────────────────────────────────┐   │
│  PROMPT LIBRARY    │  │                                                │   │
│                    │  │  Name: [Entity Extraction v3        ]          │   │
│  Search...         │  │  Tags: [NER] [extraction] [structured]        │   │
│                    │  │                                                │   │
│  > NER             │  │  System Prompt:                                │   │
│    v3 (current)    │  │  ┌──────────────────────────────────────────┐ │   │
│    v2              │  │  │ You are an entity extraction engine.     │ │   │
│    v1              │  │  │ Extract all named entities from the      │ │   │
│  > Summarization   │  │  │ provided text. Return JSON with keys:   │ │   │
│  > Code Review     │  │  │ people, organizations, locations.       │ │   │
│  > Translation     │  │  └──────────────────────────────────────────┘ │   │
│                    │  │                                                │   │
│  VERSIONS (NER)    │  │  User Template:                               │   │
│  v3 ● current      │  │  ┌──────────────────────────────────────────┐ │   │
│  v2   2 days ago   │  │  │ Extract entities from: {{input_text}}   │ │   │
│  v1   5 days ago   │  │  └──────────────────────────────────────────┘ │   │
│                    │  │                                                │   │
│  [Diff v2<->v3]   │  │  ┌─ Few-Shot Examples (3) ───────────────────┐ │   │
│                    │  │  │ [Example 1] [Example 2] [Example 3] [+]   │ │   │
│                    │  │  └──────────────────────────────────────────┘ │   │
│                    │  │                                                │   │
│                    │  │  [Run]  [A/B Test]  [Run Pipeline]            │   │
│                    │  └────────────────────────────────────────────────┘   │
├────────────────────┴────────────────────────────────────────────────────────┤
│  A/B TEST RESULTS                                                          │
│  ┌──────────────┬──────────┬──────────┬───────┬────────┬─────────────────┐ │
│  │ Variation    │ Model    │ Temp     │ Score │ Tokens │ Avg Latency     │ │
│  ├──────────────┼──────────┼──────────┼───────┼────────┼─────────────────┤ │
│  │ NER v3       │ llama-70b│ 0.1      │ 9.2   │ 145    │ 1.8s            │ │
│  │ NER v3       │ llama-70b│ 0.7      │ 7.8   │ 203    │ 2.1s            │ │
│  │ NER v2       │ llama-70b│ 0.1      │ 8.1   │ 167    │ 1.9s            │ │
│  │ NER v3       │ mixtral  │ 0.1      │ 8.9   │ 132    │ 1.2s            │ │
│  └──────────────┴──────────┴──────────┴───────┴────────┴─────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 3. EXPERIMENT TRACKER

MLflow-inspired experiment tracking purpose-built for LLM research.

**Capabilities:**
- Organize work into Projects -> Experiments -> Runs
- Auto-log: model, parameters, prompt version, tokens, latency, cost
- Custom metrics (accuracy, BLEU, ROUGE, F1, custom scorers)
- Tag and annotate runs
- Compare runs side-by-side (parameters, outputs, metrics)
- Visualize metrics across runs (scatter, bar, parallel coordinates)
- Filter and sort run tables
- Reproduce any run from its saved configuration
- Export results (CSV, JSON)
- **Logprobs metrics:** average perplexity per run, confidence distributions, entropy-based quality signals

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  EXPERIMENT TRACKER                                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  Project: [NER Research ▼]  >  Experiment: [Few-Shot Comparison ▼]        │
│                                                                            │
│  ┌─ Runs ──────────────────────────────────────────────────────────────┐  │
│  │ Select│ Run       │ Model    │ Temp │ F1    │ Tokens│ Cost  │ PPL  │  │
│  ├───────┼───────────┼──────────┼──────┼───────┼───────┼───────┼──────┤  │
│  │  [x]  │ run-047   │ llama-70b│ 0.1  │ 0.94  │ 2.1k  │ $0.03 │ 2.8 │  │
│  │  [x]  │ run-046   │ llama-70b│ 0.3  │ 0.91  │ 2.4k  │ $0.04 │ 3.1 │  │
│  │  [ ]  │ run-045   │ mixtral  │ 0.1  │ 0.88  │ 1.8k  │ $0.02 │ 3.4 │  │
│  │  [x]  │ run-044   │ llama-8b │ 0.1  │ 0.79  │ 1.5k  │ $0.01 │ 4.2 │  │
│  │  [ ]  │ run-043   │ llama-70b│ 0.7  │ 0.72  │ 3.1k  │ $0.05 │ 5.7 │  │
│  └───────┴───────────┴──────────┴──────┴───────┴───────┴───────┴──────┘  │
│                                                                            │
│  [Compare Selected (3)]  [Re-run]  [Export CSV]  [Delete]                  │
│                                                                            │
│  ┌─ Comparison View ────────────────────────────────────────────────────┐  │
│  │                                                                      │  │
│  │  F1 Score by Model & Temperature          Perplexity vs F1          │  │
│  │                                                                      │  │
│  │  0.95 ┤          *                         6 ┤                      │  │
│  │  0.90 ┤      *                             5 ┤  *                   │  │
│  │  0.85 ┤                  *                 4 ┤      *               │  │
│  │  0.80 ┤              *                     3 ┤          * *         │  │
│  │  0.75 ┤                      *             2 ┤                      │  │
│  │       └──────────────────────              └──────────────────      │  │
│  │       llama-70b  mixtral  llama-8b         (lower PPL = better)    │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 4. DATASET MANAGER

Create, curate, and manage datasets for evaluation and fine-tuning.

**Capabilities:**
- Upload datasets (CSV, JSON, JSONL, Parquet, HuggingFace)
- Dataset browser with pagination, search, filtering
- Inline editing of records
- Column mapping / schema definition
- Train/test/validation split tool
- Data annotation interface (label, rate, classify)
- Synthetic data generation (use loaded models to generate examples)
- Dataset versioning
- Statistics & distribution analysis
- Export in multiple formats (including fine-tuning formats: Alpaca, ShareGPT, ChatML)
- Subset creation with smart sampling

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  DATASET MANAGER                                                           │
├──────────────────┬──────────────────────────────────────────────────────────┤
│                  │  Dataset: customer_support_v2.jsonl                      │
│  DATASETS        │  Records: 12,847  |  Size: 24.3 MB  |  Version: 2      │
│                  │                                                          │
│  Search...       │  ┌─ Schema ───────────────────────────────────────────┐  │
│                  │  │ input (string)     -> prompt                        │  │
│  customer_support│  │ output (string)    -> expected_response             │  │
│    v2 *          │  │ category (enum)    -> [billing, tech, general]      │  │
│    v1            │  │ difficulty (int)   -> [1-5]                         │  │
│  code_review     │  └────────────────────────────────────────────────────┘  │
│  medical_qa      │                                                          │
│  legal_extract   │  ┌─ Data Browser ─────────────────────────────────────┐  │
│                  │  │ Filter: category = [billing]  difficulty > [3]      │  │
│  [+ Upload]      │  │                                                     │  │
│  [+ Generate]    │  │ ┌────┬──────────────────┬─────────────┬─────┬────┐ │  │
│                  │  │ │ #  │ input            │ output      │ cat │ dif│ │  │
│  ── TOOLS ────── │  │ ├────┼──────────────────┼─────────────┼─────┼────┤ │  │
│                  │  │ │ 1  │ Why was I charged│ Your account│ bill│ 4  │ │  │
│  Split Train/Test│  │ │ 2  │ I need a refund  │ I understand│ bill│ 3  │ │  │
│  Annotate        │  │ │ 3  │ Double charge on │ Let me look │ bill│ 5  │ │  │
│  Statistics      │  │ └────┴──────────────────┴─────────────┴─────┴────┘ │  │
│  Deduplicate     │  │                                                     │  │
│  Augment         │  │ Showing 1-50 of 4,231 (filtered)   [< 1 2 3 ... >] │  │
│  Export (FT fmt) │  └─────────────────────────────────────────────────────┘  │
│                  │                                                          │
│                  │  ┌─ Stats ─────────────────────────────────────────────┐  │
│                  │  │ Category Distribution    |  Avg Token Length        │  │
│                  │  │ billing ████████ 33%     |  Input:  127 tokens      │  │
│                  │  │ tech    ██████████ 41%   |  Output: 234 tokens      │  │
│                  │  │ general ██████ 26%       |  Total:  361 tokens      │  │
│                  │  └─────────────────────────────────────────────────────┘  │
└──────────────────┴──────────────────────────────────────────────────────────┘
```

---

### 5. EVALUATION & BENCHMARKING SUITE

Systematic model and prompt evaluation.

**Capabilities:**
- Built-in benchmark suites (MMLU, HumanEval, HellaSwag, TruthfulQA, etc.)
- Custom evaluation pipelines
- LLM-as-Judge evaluation (configurable judge prompt + model)
- Human evaluation interface (side-by-side rating, Elo ranking)
- Automated scoring: exact match, BLEU, ROUGE, BERTScore, semantic similarity
- Regression testing (detect quality drops across prompt/model changes)
- Evaluation reports with visualizations
- Batch evaluation with progress tracking
- Cost/quality Pareto frontier analysis
- Leaderboard view across models
- **Logprobs-based metrics:** per-sample perplexity, calibration curves (predicted confidence vs actual correctness), entropy-flagged uncertain outputs for human review

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  EVALUATION SUITE                                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─ New Evaluation ─────────────────────────────────────────────────────┐  │
│  │                                                                      │  │
│  │  Dataset: [customer_support_v2]     Subset: [test]                  │  │
│  │  Models:  [x llama-70b] [x mixtral] [x llama-8b] [ qwen-72b]      │  │
│  │  Prompt:  [NER v3]                                                  │  │
│  │                                                                      │  │
│  │  ┌─ Scoring Methods ──────────────────────────────────────────────┐  │  │
│  │  │ [x] Exact Match    [x] ROUGE-L    [ ] BLEU                    │  │  │
│  │  │ [x] LLM-as-Judge   Judge Model: [llama-70b]                   │  │  │
│  │  │     Judge Prompt: [Factual Accuracy]                           │  │  │
│  │  │ [x] Semantic Similarity (embedding cosine)                     │  │  │
│  │  │ [x] Logprobs Analysis (perplexity, calibration)                │  │  │
│  │  └────────────────────────────────────────────────────────────────┘  │  │
│  │                                                                      │  │
│  │  Parallelism: [4]   Max samples: [500]   [Start Evaluation]         │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Leaderboard ────────────────────────────────────────────────────────┐  │
│  │ Rank│ Model    │ Exact│ ROUGE│ Judge│ Semantic│ Avg PPL │ Avg Lat │  │  │
│  │ 1   │ llama-70b│ 0.82 │ 0.91 │ 8.7  │ 0.94   │ 2.9     │ 3.2s   │  │  │
│  │ 2   │ mixtral  │ 0.78 │ 0.88 │ 8.3  │ 0.91   │ 3.4     │ 1.8s   │  │  │
│  │ 3   │ llama-8b │ 0.64 │ 0.79 │ 7.1  │ 0.83   │ 5.1     │ 0.9s   │  │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Human Eval Queue ──────────────────────────────────────────────────┐   │
│  │                                                                      │  │
│  │  Which response is better?                                           │  │
│  │  Input: "Why was I charged twice for my subscription?"               │  │
│  │                                                                      │  │
│  │  ┌─ Response A ─────────┐    ┌─ Response B ─────────┐              │  │
│  │  │ I apologize for the  │    │ Thank you for        │              │  │
│  │  │ inconvenience. Let me│    │ reaching out! I can  │              │  │
│  │  │ check your billing...│    │ see the duplicate... │              │  │
│  │  └──────────────────────┘    └──────────────────────┘              │  │
│  │                                                                      │  │
│  │  [A is Better]  [Tie]  [B is Better]  [Skip]      12/500 rated     │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 6. RAG WORKBENCH

Experiment with Retrieval-Augmented Generation strategies.

**Capabilities:**
- Document ingestion (PDF, TXT, MD, HTML, DOCX, code files)
- Multiple chunking strategies (fixed, sentence, semantic, recursive)
- Side-by-side chunking preview
- Embedding model selection (local via vLLM or external)
- Vector store management (collections, indexing, deletion)
- Retrieval testing: query -> see retrieved chunks with scores
- Full RAG pipeline testing: query -> retrieve -> generate -> evaluate
- Chunk overlap & size experimentation
- Re-ranking strategies (cross-encoder, LLM-based)
- Hybrid search (vector + keyword BM25)
- Citation/attribution tracking in generated responses
- RAG evaluation metrics (faithfulness, relevance, recall)
- **Logprobs for grounding:** highlight tokens in RAG responses by confidence — low-confidence tokens may indicate hallucination beyond retrieved context

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  RAG WORKBENCH                                                             │
├──────────────────┬──────────────────────────────────────────────────────────┤
│                  │  ┌─ Pipeline Configuration ────────────────────────────┐ │
│  COLLECTIONS     │  │                                                     │ │
│                  │  │  ┌────────┐   ┌─────────┐   ┌──────────┐   ┌─────┐│ │
│  tech-docs       │  │  │ Ingest │-->│  Chunk  │-->│  Embed   │-->│Store││ │
│    142 docs      │  │  └────────┘   └─────────┘   └──────────┘   └─────┘│ │
│    23k chunks    │  │                                                     │ │
│                  │  │  Chunking: [Recursive]  Size: [512]  Overlap: [50]  │ │
│  legal-corpus    │  │  Embedding: [bge-large-en]  Dim: 1024              │ │
│    89 docs       │  │  Search: [x Vector] [x BM25]  Top-K: [5]          │ │
│    15k chunks    │  │  Reranker: [Cross-encoder]   Top-N: [3]            │ │
│                  │  └─────────────────────────────────────────────────────┘ │
│  [+ New]         │                                                         │
│  [+ Ingest Docs] │  ┌─ Retrieval Test ───────────────────────────────────┐ │
│                  │  │  Query: [What are the memory limits for vLLM?    ] │ │
│                  │  │  [Retrieve]  [Retrieve + Generate]                  │ │
│                  │  │                                                     │ │
│                  │  │  Retrieved Chunks:                                  │ │
│                  │  │  ┌─ Chunk #1 ─── score: 0.94 ─── doc: vllm.md ──┐ │ │
│                  │  │  │ "vLLM supports PagedAttention which allows    │ │ │
│                  │  │  │ efficient memory management. The KV cache is  │ │ │
│                  │  │  │ allocated in blocks of 16 tokens..."          │ │ │
│                  │  │  └──────────────────────────────────────────────┘ │ │
│                  │  │  ┌─ Chunk #2 ─── score: 0.89 ─── doc: config.md ┐ │ │
│                  │  │  │ "GPU memory utilization can be set via        │ │ │
│                  │  │  │ --gpu-memory-utilization flag (default 0.9)..." │ │
│                  │  │  └──────────────────────────────────────────────┘ │ │
│                  │  │                                                     │ │
│                  │  │  ┌─ Generated Response ────────────────────────────┐ │
│                  │  │  │ vLLM manages memory through PagedAttention[1]. │ │
│                  │  │  │ The default GPU memory utilization is 90%[2]... │ │
│                  │  │  │                                                  │ │
│                  │  │  │ Confidence: ██████████░░ 83% (logprobs avg)     │ │
│                  │  │  │ Sources: [1] vllm.md  [2] config.md             │ │
│                  │  │  └────────────────────────────────────────────────┘ │ │
│                  │  └─────────────────────────────────────────────────────┘ │
└──────────────────┴──────────────────────────────────────────────────────────┘
```

---

### 7. AGENT BUILDER (Integration-First)

Agent workflow construction and testing. Rather than building a full visual DAG editor from scratch, this module integrates with existing frameworks (Semantic Kernel, AutoGen) and focuses on what's unique to this platform: the testing, tracing, and evaluation layer.

**Phase 5 scope — keep it simple initially:**
- Define agent workflows via YAML/JSON config (not drag-and-drop)
- Built-in tool library (web search, code execution, calculator, file I/O, API call)
- Custom tool definition (OpenAPI spec or function schema)
- ReAct and Plan-and-Execute patterns via Semantic Kernel integration
- Step-by-step execution with state inspection at each node
- Agent conversation history visualization
- Tool call logging and inspection
- Cost and latency tracking per agent run
- Guardrails configuration (max steps, token budget, blocked actions)
- **Logprobs for tool selection:** inspect model confidence when choosing tools or deciding to stop

**Future (if warranted):** Visual drag-and-drop canvas, sub-agents, custom agent patterns

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  AGENT BUILDER                                                             │
├──────────────┬──────────────────────────────────────────────────────────────┤
│              │  ┌─ Agent Config (YAML) ────────────────────────────────┐   │
│  TOOLS       │  │  name: research-assistant                            │   │
│              │  │  pattern: react                                      │   │
│  ┌─────────┐ │  │  model: llama-70b                                    │   │
│  │ LLM Call│ │  │  tools: [web_search, rag_query, calculator]          │   │
│  └─────────┘ │  │  max_steps: 10                                       │   │
│  ┌─────────┐ │  │  token_budget: 8000                                  │   │
│  │  Tool   │ │  │                                                      │   │
│  └─────────┘ │  │  [Run]  [Validate]  [Save Version]                  │   │
│  ┌─────────┐ │  └──────────────────────────────────────────────────────┘   │
│  │  Loop   │ │                                                             │
│  └─────────┘ │  ┌─ Execution Trace ────────────────────────────────────┐   │
│              │  │ Step 1: Think -> "I need to search for..."           │   │
│  web_search  │  │   tool_choice confidence: 0.91                       │   │
│  calculator  │  │ Step 2: Act   -> web_search("vLLM memory limits")   │   │
│  code_exec   │  │   5 results found                                    │   │
│  rag_query   │  │ Step 3: Think -> "Now I have enough info..."         │   │
│  api_call    │  │   stop confidence: 0.87                              │   │
│  file_read   │  │ Step 4: Answer -> "Based on my research..."          │   │
│              │  │                                                       │   │
│  [+ Custom]  │  │ Total: 4 steps | 1,247 tokens | 3.8s | $0.02       │   │
│              │  └──────────────────────────────────────────────────────┘   │
└──────────────┴──────────────────────────────────────────────────────────────┘
```

---

### 8. INFERENCE HISTORY & REPLAY

Every inference call is automatically recorded. Browse, search, tag, and replay your entire history.

**Capabilities:**
- Automatic recording of every inference call (prompt, params, model, provider, response, logprobs, timing)
- Browse history as a searchable, filterable timeline
- Tag records for grouping (e.g., "ner-tests", "session-1", "before-prompt-change")
- Full-text search across prompts and responses
- **Replay modes:**
  - **Single:** Re-run one record against a different model/provider/params
  - **Batch:** Replay all records matching a filter (date range, model, tags)
  - **Group:** Replay all records with a specific tag
  - **Step-through:** Replay one at a time, review each result before continuing
- **Replay overrides:** Change model, provider, parameters, or prompt version
- **Diff view:** Side-by-side comparison of original vs replay (text diff, logprobs diff, metric delta)
- Replay-on-provider-swap: "I just switched to Ollama — replay my last 50 prompts and compare"
- Export history (JSON, CSV) for external analysis

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  INFERENCE HISTORY                                                         │
├──────────────────┬──────────────────────────────────────────────────────────┤
│                  │  ┌─ Timeline ────────────────────────────────────────┐   │
│  FILTERS         │  │                                                   │   │
│                  │  │  14:32  Playground | llama-70b | "Explain quan..." │   │
│  Source: [All]   │  │         847 tok | 2.1s | PPL 3.2                  │   │
│  Model:  [All]   │  │  Tags: [ner-test]                                │   │
│  Provider:[All]  │  │                                                   │   │
│  Date: [Today]   │  │  14:28  Prompt Lab | llama-70b | NER v3 test     │   │
│                  │  │         145 tok | 1.8s | PPL 2.1                  │   │
│  Tags:           │  │                                                   │   │
│  [ner-tests]     │  │  14:15  Evaluation | mixtral | batch sample #12  │   │
│  [session-1]     │  │         923 tok | 1.2s | PPL 4.1                  │   │
│  [before-v4]     │  │                                                   │   │
│                  │  │  Showing 1-50 of 1,247         [< 1 2 3 ... >]   │   │
│  Search: [____]  │  └───────────────────────────────────────────────────┘   │
│                  │                                                          │
│  ── REPLAY ────  │  ┌─ Replay ──────────────────────────────────────────┐  │
│                  │  │  Source: [x] Selected (3)  [ ] Tag: ner-tests     │  │
│  [Select All]    │  │                                                    │  │
│  [Select Tagged] │  │  Override Model:    [ollama/mistral:latest]        │  │
│  [Replay]        │  │  Override Provider: [Ollama - localhost:11434]     │  │
│                  │  │  Override Params:   [* Keep original] [ Custom]   │  │
│                  │  │  Mode: [* Batch] [ Step-through]                  │  │
│                  │  │                                                    │  │
│                  │  │  [Start Replay]                                    │  │
│                  │  └────────────────────────────────────────────────────┘  │
│                  │                                                          │
│                  │  ┌─ Replay Diff ─────────────────────────────────────┐  │
│                  │  │  Record #1: "Explain quantum computing"           │  │
│                  │  │                                                    │  │
│                  │  │  ┌─ Original (llama-70b) ─┐ ┌─ Replay (mistral) ┐│  │
│                  │  │  │ Quantum computing uses  │ │ Imagine you have  ││  │
│                  │  │  │ qubits which can exist  │ │ a coin that can   ││  │
│                  │  │  │ in superposition...     │ │ be heads and...   ││  │
│                  │  │  │                         │ │                    ││  │
│                  │  │  │ 847 tok | PPL 3.2       │ │ 923 tok | PPL 4.1 ││  │
│                  │  │  └─────────────────────────┘ └────────────────────┘│  │
│                  │  │  Similarity: 0.72 | Output changed: Yes            │  │
│                  │  └────────────────────────────────────────────────────┘  │
└──────────────────┴──────────────────────────────────────────────────────────┘
```

---

### 9. MODEL MANAGEMENT & MONITORING

Connect to and monitor inference provider instances. Supports runtime provider and model swapping.

**Phase 1 (connect & observe):**
- Register provider instances (vLLM, Ollama, LM Studio) via UI or config file
- Health checks and status display
- Model info: parameters, quantization, context length, architecture
- GPU memory monitoring (per-GPU utilization, KV cache usage)
- Throughput metrics (requests/sec, tokens/sec, queue depth)
- Provider capabilities display (logprobs, guided decoding, model swap support)

**Runtime swapping (all phases):**
- **Hot-reload model:** Swap the loaded model on providers that support it (Ollama, LM Studio) without restarting
- **Swap provider:** Change which backend handles inference for an instance (e.g., switch from vLLM to Ollama) via API or UI
- **Config-driven:** Edit `providers.json` — file watcher detects changes, providers update without restart
- **Replay validation:** After swapping, use History & Replay to verify the new setup matches expected behavior

**Phase 3+ (manage):**
- HuggingFace model browser and download manager
- Quantization tools (GPTQ, AWQ, GGUF conversion)
- Multi-GPU / tensor parallelism configuration
- Auto-restart on failure

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  MODEL MANAGEMENT                                                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─ Connected Instances ──────────────────────────────────────────────┐   │
│  │                                                                      │  │
│  │  Instance 1: localhost:8000          * Running                       │  │
│  │  |-- Model: meta-llama/Meta-Llama-3.1-70B-Instruct                 │  │
│  │  |-- GPUs: 2x A100 80GB (TP=2)                                     │  │
│  │  |-- Quantization: AWQ 4-bit                                        │  │
│  │  |-- Context: 8192 tokens                                           │  │
│  │  |-- GPU Mem: [================....] 78%  |  KV Cache: 62%         │  │
│  │  |-- Throughput: 847 tok/s  |  Queue: 3 requests                    │  │
│  │  |-- Uptime: 4d 12h        |  Requests served: 12,847              │  │
│  │  |-- Logprobs: supported  |  Guided decoding: supported            │  │
│  │  [Logs] [Metrics]                                                   │  │
│  │                                                                      │  │
│  │  Instance 2: localhost:8001          * Running                       │  │
│  │  |-- Model: mistralai/Mixtral-8x7B-Instruct-v0.1                   │  │
│  │  |-- GPUs: 1x A100 80GB                                             │  │
│  │  |-- GPU Mem: [============........] 58%  |  KV Cache: 34%         │  │
│  │  |-- Throughput: 1,241 tok/s                                        │  │
│  │  [Logs] [Metrics]                                                   │  │
│  │                                                                      │  │
│  │  [+ Register Instance]                                              │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Throughput (Last 24h) ──────────────────────────────────────────────┐  │
│  │   1.5k ┤                    /--\                                     │  │
│  │   1.0k ┤    /--------\   /--   --\                                  │  │
│  │   0.5k ┤---/          ---         ----                              │  │
│  │      0 ┤                                                             │  │
│  │        └──────────────────────────────────                           │  │
│  │        00:00    06:00    12:00    18:00                               │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 9. BATCH INFERENCE ENGINE

Run large-scale inference jobs efficiently.

**Capabilities:**
- Upload/select dataset for batch processing
- Configure model, parameters, and prompt template
- Progress tracking with ETA
- Automatic retry on failures
- Rate limiting and concurrency control
- Results streaming to file as they complete
- Pause/resume capability
- Cost estimation before and tracking during run
- Output validation rules
- Post-processing pipeline (extract JSON, parse, transform)
- **Logprobs collection:** optionally capture logprobs for every batch response for downstream analysis

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  BATCH INFERENCE                                                           │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─ New Batch Job ──────────────────────────────────────────────────────┐  │
│  │  Dataset:     [customer_support_v2]  Subset: [all]                  │  │
│  │  Model:       [llama-70b]            Prompt: [NER v3]               │  │
│  │  Concurrency: [8]                   Max retries: [3]                │  │
│  │  Output:      [batch_results_{{timestamp}}.jsonl]                   │  │
│  │  Logprobs:    [x Capture]  Top-K: [5]                               │  │
│  │                                                                      │  │
│  │  Estimated: 12,847 requests | ~2.1M tokens | ~$31.40 | ~45 min     │  │
│  │  [Start Batch]                                                      │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Active Jobs ────────────────────────────────────────────────────────┐  │
│  │                                                                      │  │
│  │  Job: batch-2024-0301-001    Status: * Running                      │  │
│  │  Progress: [================..............] 54%  (6,937/12,847)     │  │
│  │  Speed: 4.2 req/s  |  Tokens: 1.14M  |  Cost: $17.08               │  │
│  │  Errors: 12 (0.2%)  |  ETA: 23 min                                  │  │
│  │  [Pause]  [Stop]  [Preview Results]                                  │  │
│  │                                                                      │  │
│  │  Job: batch-2024-0228-003    Status: Complete                        │  │
│  │  Records: 5,000  |  Errors: 3  |  Cost: $8.42  |  Duration: 18min  │  │
│  │  [Download]  [View Results]  [Re-run Failed]                         │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 10. TOKEN ECONOMICS & ANALYTICS DASHBOARD

Understand cost, usage patterns, and performance across all activities.

**Capabilities:**
- Real-time token usage tracking across all modules
- Cost breakdown by model, project, experiment, user
- Tokens-per-second trends over time
- Latency percentiles (p50, p95, p99)
- Time-to-first-token (TTFT) analysis
- Usage forecasting
- Budget alerts and limits
- GPU utilization correlation with throughput
- Daily/weekly/monthly reports
- Export analytics data

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  ANALYTICS DASHBOARD                                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  Period: [Last 7 Days]    Project: [All]    Model: [All]                  │
│                                                                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  │
│  │ Total Tokens │  │  Total Cost  │  │  Avg Latency │  │  Throughput  │  │
│  │              │  │              │  │              │  │              │  │
│  │  14.2M       │  │  $187.40     │  │  2.1s        │  │  912 tok/s   │  │
│  │  ^ 23%       │  │  ^ 18%       │  │  v 12%       │  │  ^ 8%        │  │
│  └──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘  │
│                                                                            │
│  ┌─ Token Usage Over Time ──────────────────────────────────────────────┐  │
│  │  3M ┤         /--\                                                   │  │
│  │  2M ┤    /---/   --\    /--\                                        │  │
│  │  1M ┤---/          \---/   \--\                                     │  │
│  │   0 ┤                        \--                                    │  │
│  │     └─ Mon   Tue   Wed   Thu   Fri   Sat   Sun                      │  │
│  │     --- Prompt tokens  --- Completion tokens                        │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Cost by Model ──────────────┐  ┌─ Latency Distribution ────────────┐  │
│  │                               │  │                                    │  │
│  │  llama-70b  ████████████ $98  │  │  p50: 1.8s                        │  │
│  │  mixtral    ██████ $52        │  │  p95: 4.2s   /--\                 │  │
│  │  llama-8b   ████ $31          │  │  p99: 7.1s  /    \               │  │
│  │  qwen-72b   ██ $6             │  │           /-      \--\           │  │
│  │                               │  │         /-            \-----     │  │
│  └───────────────────────────────┘  └──────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 11. RESEARCH NOTEBOOK (Embedded JupyterLite)

Rather than building a custom notebook from scratch, embed JupyterLite for computational notebooks and focus on the integration layer that makes it research-aware.

**Capabilities:**
- Embedded JupyterLite instance with Python kernel (runs in browser, no server needed)
- Pre-installed helpers: `workbench` Python package that talks to the platform API
  - `workbench.chat(model, prompt)` — call models from notebook cells
  - `workbench.get_experiment(id)` — pull experiment results into DataFrames
  - `workbench.get_dataset(id)` — load datasets directly
  - `workbench.logprobs(model, prompt)` — get raw logprobs for analysis
- One-click "Open in Notebook" from any experiment, dataset, or evaluation result
- Notebook storage and versioning in the platform
- Share and export as HTML/PDF

**What we DON'T build:** Cell rendering, code execution, kernel management — JupyterLite handles all of that.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  RESEARCH NOTEBOOKS                                                        │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─ Notebooks ────────┐  ┌─ JupyterLite ─────────────────────────────┐   │
│  │                     │  │                                            │   │
│  │  NER Comparison     │  │  [1]: import workbench as wb              │   │
│  │  RAG Analysis       │  │                                            │   │
│  │  Model Eval Report  │  │  [2]: exp = wb.get_experiment("few-shot") │   │
│  │                     │  │       df = exp.to_dataframe()              │   │
│  │  [+ New Notebook]   │  │       df.head()                           │   │
│  │                     │  │                                            │   │
│  │  ── Quick Open ──── │  │       run  | model    | f1   | ppl | ... │   │
│  │                     │  │       047  | llama-70b| 0.94 | 2.8 | ... │   │
│  │  From experiment... │  │       046  | llama-70b| 0.91 | 3.1 | ... │   │
│  │  From dataset...    │  │                                            │   │
│  │  From evaluation... │  │  [3]: lp = wb.logprobs("llama-70b",      │   │
│  │                     │  │         "Explain quantum computing")      │   │
│  │                     │  │       wb.plot_token_heatmap(lp)           │   │
│  │                     │  │                                            │   │
│  │                     │  │       [token heatmap visualization]       │   │
│  │                     │  │                                            │   │
│  └─────────────────────┘  └───────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 12. FINE-TUNING SUPPORT (Integration, Not Implementation)

Rather than building a training loop, this module focuses on **dataset preparation** and **evaluation of fine-tuned models** — the parts unique to this platform. Actual training is delegated to proven tools (Axolotl, Unsloth, HuggingFace TRL).

**What we build:**
- Dataset export in fine-tuning formats (Alpaca, ShareGPT, ChatML, OpenAI JSONL)
- Data validation for training format compliance
- One-click "Export for fine-tuning" from Dataset Manager
- Import fine-tuned model/adapter back into vLLM for evaluation
- A/B comparison: base model vs fine-tuned model in Playground
- Evaluation suite integration: run benchmarks on fine-tuned model vs base

**What we DON'T build:** Training loops, loss curve visualization, checkpoint management, LoRA configuration UI. Use Axolotl/Unsloth/TRL directly for training — they're battle-tested and actively maintained.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  FINE-TUNING SUPPORT                                                       │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                            │
│  ┌─ Export for Training ──────────────────────────────────────────────┐   │
│  │                                                                      │  │
│  │  Dataset: [customer_support_v2]  Split: [train]                     │  │
│  │  Format:  [* ShareGPT] [ Alpaca] [ ChatML] [ OpenAI JSONL]        │  │
│  │                                                                      │  │
│  │  Validation:                                                         │  │
│  │  [x] All records have system/user/assistant turns                   │  │
│  │  [x] No empty responses                                             │  │
│  │  [x] Token lengths within model context (avg: 361, max: 1,204)     │  │
│  │  [ ] 3 records have formatting warnings (review)                    │  │
│  │                                                                      │  │
│  │  [Export 9,847 records]  -> customer_support_sharegpt.jsonl         │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
│                                                                            │
│  ┌─ Evaluate Fine-Tuned Model ────────────────────────────────────────┐   │
│  │                                                                      │  │
│  │  Register adapter: [Browse... lora-adapter-epoch3/]                 │  │
│  │  Base model instance: [localhost:8000 - llama-8b]                   │  │
│  │                                                                      │  │
│  │  Quick Compare (Playground):                                         │  │
│  │  [Open side-by-side: base vs fine-tuned]                            │  │
│  │                                                                      │  │
│  │  Full Evaluation:                                                    │  │
│  │  [Run eval suite on fine-tuned model] -> sends to Evaluation Suite  │  │
│  └──────────────────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

### 13. STRUCTURED OUTPUT TOOLKIT

Tools for working with structured (JSON/XML) LLM outputs.

**Capabilities:**
- JSON Schema editor with visual builder
- Schema-constrained generation (vLLM guided decoding)
- Output validation and error highlighting
- Schema testing against multiple inputs
- Type coercion and transformation rules
- Response parsing pipeline
- Schema library and versioning
- **Logprobs for structure:** inspect token confidence within structured outputs — low confidence on a JSON value suggests the model is guessing

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  STRUCTURED OUTPUT                                                         │
├────────────────────────────┬────────────────────────────────────────────────┤
│                            │  ┌─ Output ──────────────────────────────────┐│
│  ┌─ JSON Schema ────────┐  │  │  {                                        ││
│  │ {                     │  │  │    "people": [                            ││
│  │   "type": "object",  │  │  │      {                                    ││
│  │   "properties": {    │  │  │        "name": "John Smith",  ok (0.97)  ││
│  │     "people": {      │  │  │        "role": "customer"     ok (0.89)  ││
│  │       "type": "array"│  │  │      }                                    ││
│  │       "items": {     │  │  │    ],                                     ││
│  │         "name": str, │  │  │    "organizations": [                     ││
│  │         "role": str  │  │  │      {                                    ││
│  │       }              │  │  │        "name": "Acme",        ok (0.94)  ││
│  │     }                │  │  │        "type": "company"      ok (0.82)  ││
│  │   }                  │  │  │      }                                    ││
│  │ }                    │  │  │    ]                                      ││
│  └──────────────────────┘  │  │  }                                        ││
│                            │  │  Validation: All fields valid              ││
│  [Visual Builder]          │  │  Avg confidence: 0.91                      ││
│  [Test with Dataset]       │  └───────────────────────────────────────────┘│
└────────────────────────────┴───────────────────────────────────────────────┘
```

---

## Global Navigation & Layout

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  AI Research Workbench                                   [alerts] [config] │
├────┬────────────────────────────────────────────────────────────────────────┤
│    │                                                                       │
│ PG │   (Active Module Content Area)                                        │
│    │                                                                       │
│ PR │                                                                       │
│    │                                                                       │
│ EX │                                                                       │
│    │                                                                       │
│ DS │                                                                       │
│    │                                                                       │
│ EV │                                                                       │
│    │                                                                       │
│ RG │                                                                       │
│    │                                                                       │
│ AG │                                                                       │
│    │                                                                       │
│ MD │                                                                       │
│    │                                                                       │
│ BA │                                                                       │
│    │                                                                       │
│ AN │                                                                       │
│    │                                                                       │
│ NB │                                                                       │
│    │                                                                       │
│ FT │                                                                       │
│    │                                                                       │
│ SO │                                                                       │
│    │                                                                       │
├────┴────────────────────────────────────────────────────────────────────────┤
│  Status: vLLM * 2 instances | GPU: 78% | Queue: 3 | Session tokens: 14.2k│
└─────────────────────────────────────────────────────────────────────────────┘

Sidebar Legend:
PG Playground          PR Prompt Lab        EX Experiments
HI History & Replay    DS Datasets          EV Evaluation
RG RAG Workbench       AG Agents            MD Models
BA Batch Inference     AN Analytics         NB Notebooks
FT Fine-Tuning         SO Structured Output
```

---

## Data Models (Core Entities)

```
Project
|-- id, name, description, created_at, user_id?
|-- has many: Experiments, Prompts, Datasets

Experiment
|-- id, project_id, name, description, status
|-- has many: Runs

Run
|-- id, experiment_id, model, parameters (JSON)
|-- prompt_version_id, dataset_id
|-- metrics (JSON), tokens_used, cost, latency_ms
|-- input, output, status, error, created_at
|-- logprobs_data (JSON, nullable) — raw logprobs when captured
|-- perplexity (float, nullable) — computed from logprobs

PromptTemplate
|-- id, project_id, name, category, tags
|-- has many: PromptVersions

PromptVersion
|-- id, template_id, version, system_prompt, user_template
|-- variables (JSON), few_shot_examples (JSON)
|-- created_at, notes

Dataset
|-- id, project_id, name, format, schema (JSON)
|-- record_count, size_bytes, version
|-- has many: DatasetRecords, DatasetSplits

InferenceInstance
|-- id, name, endpoint, provider_type (vllm/ollama/lmstudio/openai_compatible)
|-- model_id, status, gpu_config (JSON), parameters (JSON)
|-- capabilities (JSON: logprobs, guided_decoding, model_swap, etc.)
|-- created_at, last_health_check
|-- source (config_file / api / database)

InferenceRecord
|-- id, source_module, provider_instance_id, provider_type, model
|-- request (JSON), response (JSON)
|-- prompt_tokens, completion_tokens, latency_ms, ttft_ms
|-- perplexity, cost, prompt_version_id?, run_id?
|-- tags[], correlation_id, created_at

ReplaySession
|-- id, name, mode (single/batch/group), status
|-- override_model?, override_provider_id?, override_parameters (JSON)?
|-- source_record_ids[], total_records, completed, failed
|-- has many: ReplayResults

AgentWorkflow
|-- id, project_id, name, description
|-- config (YAML/JSON), tools (JSON)
|-- has many: AgentRuns

RagCollection
|-- id, project_id, name, embedding_model
|-- chunk_strategy, chunk_size, chunk_overlap
|-- document_count, chunk_count

BatchJob
|-- id, dataset_id, model, prompt_version_id
|-- status, progress, total_records
|-- completed, failed, cost, started_at, finished_at
|-- capture_logprobs (bool)
```

---

## API Structure (.NET Minimal API)

```
/api/v1/
|-- /models
|   |-- GET    /                              # List available models
|   |-- GET    /{id}                          # Model details
|   |-- POST   /instances                     # Register provider instance
|   |-- PUT    /instances/{id}                # Update provider config at runtime
|   |-- DELETE /instances/{id}                # Remove provider instance
|   |-- GET    /instances/{id}/metrics        # Runtime metrics
|   |-- POST   /instances/{id}/swap-model     # Hot-reload model (if supported)
|   |-- GET    /instances/{id}/available-models # Models available on this provider
|   |-- POST   /instances/reload-config       # Force reload from providers.json
|
|-- /inference
|   |-- POST   /chat                # Chat completion (SSE streaming)
|   |-- POST   /complete            # Text completion
|   |-- POST   /chat/logprobs       # Chat with full logprobs response
|   |-- POST   /batch               # Start batch job
|
|-- /history
|   |-- GET    /                    # Browse inference history (paginated, filterable)
|   |-- GET    /{id}                # Full detail of one inference record
|   |-- PUT    /{id}/tags           # Add/remove tags on a record
|   |-- DELETE /{id}                # Delete a record
|   |-- GET    /search              # Full-text search across prompts/responses
|   |-- POST   /tag-batch           # Tag multiple records at once
|   |-- POST   /replay              # Start a replay session
|   |-- GET    /replay/{id}         # Replay session status + results
|   |-- POST   /replay/{id}/pause   # Pause replay
|   |-- POST   /replay/{id}/resume  # Resume replay
|   |-- POST   /replay/{id}/cancel  # Cancel replay
|   |-- GET    /replay/{id}/diff    # Diff view: originals vs replays
|
|-- /prompts
|   |-- GET    /                    # List prompt templates
|   |-- POST   /                    # Create template
|   |-- GET    /{id}/versions       # List versions
|   |-- POST   /{id}/versions       # Create version
|   |-- POST   /{id}/test           # Test prompt with variables
|
|-- /experiments
|   |-- GET    /                    # List experiments
|   |-- POST   /                    # Create experiment
|   |-- GET    /{id}/runs           # List runs
|   |-- POST   /{id}/runs           # Create run
|   |-- GET    /{id}/compare        # Compare selected runs
|
|-- /datasets
|   |-- GET    /                    # List datasets
|   |-- POST   /upload              # Upload dataset
|   |-- GET    /{id}/records        # Browse records
|   |-- POST   /{id}/split          # Create train/test split
|   |-- POST   /{id}/generate       # Generate synthetic data
|   |-- POST   /{id}/export         # Export in fine-tuning format
|
|-- /evaluation
|   |-- POST   /start               # Start evaluation
|   |-- GET    /{id}/results        # Get results
|   |-- GET    /leaderboard         # Model leaderboard
|
|-- /rag
|   |-- POST   /collections         # Create collection
|   |-- POST   /collections/{id}/ingest  # Ingest documents
|   |-- POST   /collections/{id}/query   # Query collection
|   |-- POST   /collections/{id}/rag     # Full RAG pipeline
|
|-- /agents
|   |-- GET    /                    # List agent workflows
|   |-- POST   /                    # Create workflow
|   |-- POST   /{id}/run            # Execute agent
|   |-- GET    /{id}/runs/{runId}   # Get run trace
|
|-- /analytics
|   |-- GET    /usage               # Token usage stats
|   |-- GET    /costs               # Cost breakdown
|   |-- GET    /performance         # Latency/throughput metrics
|
|-- /notebooks
    |-- GET    /                    # List notebooks
    |-- POST   /                    # Create notebook
    |-- PUT    /{id}                # Update notebook
    |-- GET    /{id}/download       # Download .ipynb
```

---

## Deployment Architecture

```
┌──────────────────────────────────────────────────────────┐
│                   Docker Compose                          │
│                                                           │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │   React App  │  │  .NET API    │  │  PostgreSQL    │ │
│  │   (Vite dev) │--|  (SSE)       │--|  + pgvector    │ │
│  │   :5173      │  │  :5000       │  │  :5432         │ │
│  └──────────────┘  └──────────────┘  └────────────────┘ │
│                                                           │
│  Phase 3+:  ┌──────────────┐                             │
│             │    Redis     │                              │
│             │    :6379     │                              │
│             └──────────────┘                              │
│                                                           │
└──────────────────────────┬───────────────────────────────┘
                           │
             ┌─────────────┴──────────────┐
             │  Inference Providers        │
             │  (via IInferenceProvider)   │
             │                             │
             │  vLLM       :8000, :8001    │
             │  Ollama     :11434          │
             │  LM Studio  :1234           │
             │  Any OpenAI-compatible      │
             └────────────────────────────┘
```

Phase 1 is just 3 services: React dev server, .NET API, PostgreSQL.

---

## Implementation Priority (Revised Phases)

### Phase 1 — Walk (Foundation)
**Goal:** Talk to a model through your own UI, record everything, swap backends freely.
1. **Inference Playground** — Single pane chat, parameter tuning, SSE streaming, logprobs toggle + token heatmap
2. **Model Management** — Register provider instances (vLLM/Ollama/LM Studio) via UI or config file, health checks, hot-reload model on supported providers, config file watcher for live provider updates
3. **Inference History** — Automatic recording of every call, browse/search/tag history
4. Basic conversation persistence in PostgreSQL

**Infra:** .NET API + PostgreSQL + React. Config-driven provider registry (`providers.json` with file watcher). No Redis, no MinIO, no SignalR.

### Phase 2 — Jog (Research Core)
**Goal:** Systematic prompt development, experiment tracking, and replay.
5. **Prompt Engineering Lab** — Template editor, versioning, variables, A/B testing
6. **Experiment Tracker** — Projects, runs, metrics logging, comparison charts
7. **History Replay** — Single/batch/group replay with model/provider/param overrides, diff view
8. Multi-pane Playground (side-by-side model comparison with logprobs diff)

### Phase 3 — Run (Data & Evaluation)
**Goal:** Work with datasets at scale and evaluate systematically.
9. **Dataset Manager** — Upload, browse, stats, export in fine-tuning formats
10. **Evaluation Suite** — Automated scoring, LLM-as-judge, leaderboard, logprobs metrics
11. **Batch Inference** — Large-scale processing with progress tracking
12. **Analytics Dashboard** — Full cost/usage/performance tracking

**Infra adds:** Redis for job queue.

### Phase 4 — Sprint (RAG & Structure)
**Goal:** Advanced retrieval and structured generation research.
13. **RAG Workbench** — Document ingestion, chunking, retrieval testing, full RAG pipeline
14. **Structured Output** — JSON schema builder, guided decoding, confidence annotations

**Infra adds:** pgvector extension enabled.

### Phase 5 — Fly (Agents & Integration)
**Goal:** Agent workflows and deep notebook integration.
15. **Agent Builder** — YAML config, Semantic Kernel integration, execution traces
16. **Research Notebook** — Embedded JupyterLite with `workbench` helper package
17. **Fine-Tuning Support** — Dataset export, adapter import, base-vs-fine-tuned evaluation

**Infra adds:** SignalR for agent bidirectional updates (if needed).

---

## Key Design Principles

### Research
1. **Provider-agnostic inference**: All inference goes through `IInferenceProvider`. Supports vLLM, Ollama, LM Studio, and any OpenAI-compatible backend. Swap providers without touching feature code.
2. **Logprobs are first-class**: Token probabilities, perplexity, and confidence visualization are woven throughout — not an afterthought. This is what distinguishes a research tool from a chat wrapper.
3. **Everything is an experiment**: Any interaction can be saved as a run for later comparison and reproduction.
4. **Reproducible**: Every run captures full configuration (model, provider, params, prompt version, logprobs settings) for exact reproduction.

### Architecture
5. **Vertical Slice + Clean Architecture**: Code organized by feature. Each feature owns its Domain, Application, Infrastructure, and Api layers. See `ARCHITECTURE.md`.
6. **Result pattern**: All application-layer operations return `Result<T>`. Errors are values, not exceptions. Flows cleanly from handler -> endpoint -> HTTP response.
7. **Abstracted infrastructure**: Database (`IRepository`), cache (`ICacheService`), jobs (`IJobQueue`), storage (`IFileStorage`) — all behind interfaces. Swap implementations without touching features.
8. **Documented public API**: All public types and methods carry XML doc comments with `<summary>`, `<param>`, `<returns>`, and `<example>`. Enforced by compiler warnings-as-errors.

### Operational
9. **Observable by default**: Serilog structured logging, correlation IDs on every request, usage tracking middleware. You never guess what happened.
10. **SSE-first, upgrade later**: Start with Server-Sent Events for streaming. Add SignalR only when bidirectional communication is needed.
11. **Earn your infrastructure**: No Redis until batch jobs need it. No pgvector until RAG needs it. No SignalR until agents need it. Every dependency justifies its operational cost.

### UX
12. **High-quality UX**: Dark mode default, keyboard navigable, loading/error/empty states everywhere, consistent design system (shadcn/ui + Tailwind). Research tools should feel premium, not like internal tooling.
13. **Composable**: Prompts, datasets, and models can be combined freely across modules.
14. **Local-first**: Runs entirely on your infrastructure, no cloud dependencies.
15. **Integrate, don't rebuild**: Use JupyterLite for notebooks, Axolotl/Unsloth for training, Semantic Kernel for agents. Build the research-specific UX, not commodity infrastructure.
