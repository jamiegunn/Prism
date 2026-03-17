# Prism

**See the full spectrum of your model's thinking.**

Prism is an all-in-one AI research platform built around local inference engines. It gives you deep visibility into model behavior — token probabilities, entropy, next-token prediction, step-through generation, and branch exploration — through a purpose-built research UI. Not just another chat wrapper.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## Why Prism?

Most AI tools show you the final output. Prism shows you *how the model got there*.

- **Token heatmaps** — every token colored by confidence. See exactly where the model is guessing.
- **Next-token explorer** — step through generation one token at a time. Force alternative tokens. Explore branches. See how one word changes everything downstream.
- **Probability distributions** — top-K alternatives at every position with entropy, perplexity, and surprise detection.
- **Inference history & replay** — every call is recorded. Replay against different models, parameters, or prompt versions. Diff the results.
- **Provider-agnostic** — works with vLLM, Ollama, LM Studio, or any OpenAI-compatible backend. Compare the same prompt across engines.

## Features

| Module | What It Does | Phase |
|--------|-------------|-------|
| **Playground** | Chat with streaming, logprobs heatmaps, entropy charts, surprise highlighting | 1 |
| **Token Explorer** | Next-token prediction, step-through generation, branch exploration, sampling visualization | 1 |
| **Tokenizer Explorer** | Visualize tokenization, compare tokenizers across models, cost estimation | 1 |
| **Model Management** | Register providers, monitor health/metrics, hot-swap models, KV cache visualization | 1 |
| **History & Replay** | Browse all inference history, tag, filter, replay with overrides, diff results | 1 |
| **Prompt Lab** | Template editor with variables, version control, A/B testing, few-shot management | 2 |
| **Experiments** | Track runs, compare metrics, visualize parameter sweeps, statistical analysis | 2 |
| **Datasets** | Upload, browse, split, compute statistics, export in training formats | 3 |
| **Evaluation** | Scoring methods (ROUGE, BLEU, LLM-as-Judge, perplexity), leaderboards, calibration analysis | 3 |
| **Batch Inference** | Run prompts at scale with concurrency control and progress tracking | 3 |
| **RAG Workbench** | Ingest documents, chunking strategies, vector search, end-to-end RAG pipeline debugging | 4 |
| **Structured Output** | Guided decoding with JSON schema constraints, output validation | 4 |
| **Agent Builder** | YAML-configured agents with ReAct/Plan-and-Execute patterns, execution traces | 5 |
| **Notebooks** | Embedded JupyterLite with Python helper package for programmatic access | 5 |

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 9 Minimal API |
| Frontend | React + TypeScript + Vite + Tailwind + shadcn/ui |
| Database | PostgreSQL 16 + pgvector |
| ORM | Entity Framework Core (Npgsql) |
| State | TanStack Query (server) + Zustand (client) |
| Observability | Serilog + OpenTelemetry + Aspire ServiceDefaults |
| Inference | vLLM, Ollama, LM Studio, OpenAI-compatible |
| Streaming | Server-Sent Events (SSE) |

## Architecture

Prism uses **vertical slice architecture** with **clean architecture per slice**. Every feature is self-contained. Every external dependency is behind an abstraction. Errors are values, not exceptions.

```
backend/src/
  Prism.Api/              # Startup, middleware, composition root
  Prism.Common/           # Result<T>, provider interfaces, shared infrastructure
  Prism.Features/         # Feature slices (Playground/, Models/, History/, ...)
  Prism.Tests/            # Unit + integration tests

frontend/src/
  features/               # Feature modules (mirrors backend slices)
  components/             # Shared UI (logprobs visualizations, charts, layout)
  services/generated/     # Auto-generated API client via orval
```

Key abstractions — swap any backend without touching feature code:

| Abstraction | Default | Alternatives |
|-------------|---------|-------------|
| `IInferenceProvider` | vLLM | Ollama, LM Studio, OpenAI-compatible |
| `AppDbContext` (EF Core) | PostgreSQL | SQL Server, SQLite |
| `IVectorStore` | pgvector | Qdrant, Pinecone |
| `ICacheService` | In-Memory | Redis, None |
| `IFileStorage` | Local filesystem | Azure Blob, S3 |
| `IAuthProvider` | NoAuth (local) | Local JWT, Entra ID, OIDC |

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full design. Decisions are recorded as [ADRs](docs/ADR/).

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 20+](https://nodejs.org/) (LTS)
- [Docker](https://www.docker.com/) (for PostgreSQL + vLLM)
- **An LLM inference server** — Prism needs at least one running model to work. See below for options.

### Setting Up an LLM

Prism connects to LLMs via their OpenAI-compatible API. You need at least one running before you can use the platform.

**Option 1: vLLM (recommended for GPU users)**

```bash
# Start vLLM with Llama 3.1 8B (requires NVIDIA GPU with ~16GB VRAM)
docker run --gpus all \
  -p 8000:8000 \
  --name prism-vllm \
  vllm/vllm-openai:latest \
  --model meta-llama/Llama-3.1-8B-Instruct \
  --host 0.0.0.0 \
  --port 8000 \
  --max-model-len 4096

# Or use docker compose (starts vLLM alongside PostgreSQL)
docker compose --profile gpu up -d
```

Once running, register it in Prism at http://localhost:5173/models with:
- **Name:** Local vLLM
- **Endpoint:** `http://localhost:8000/v1`
- **Provider Type:** vLLM

vLLM gives you the best Prism experience — full logprobs, tokenization, guided decoding, and GPU metrics.

**Option 2: Ollama (easiest, works on CPU)**

```bash
# Install Ollama: https://ollama.com/download
ollama serve                          # Starts on port 11434
ollama pull mistral:7b-instruct       # Download a model
```

Register in Prism with:
- **Endpoint:** `http://localhost:11434`
- **Provider Type:** Ollama

Supports logprobs and streaming. No tokenization or guided decoding.

**Option 3: LM Studio (GUI, works on CPU)**

Download from https://lmstudio.ai, load a model, and start the local server (default port 1234).

Register in Prism with:
- **Endpoint:** `http://localhost:1234/v1`
- **Provider Type:** LM Studio

**Option 4: Any OpenAI-compatible API**

Any server that implements the `/v1/chat/completions` endpoint works — including OpenAI itself, Together AI, Groq, etc.

### Provider Capability Comparison

| Feature | vLLM | Ollama | LM Studio | OpenAI API |
|---------|------|--------|-----------|------------|
| Chat + Streaming | Yes | Yes | Yes | Yes |
| Logprobs (token heatmaps) | Yes (up to 20) | Yes (up to 5) | No | Yes |
| Tokenization | Yes | No | No | No |
| Guided Decoding | Yes | No | No | No |
| GPU Metrics | Yes | No | No | No |
| Model Hot-Swap | No | Yes | Yes | N/A |

Prism automatically detects provider capabilities and disables unsupported UI controls.

## Getting Started

### Quick Start (one command)

**PowerShell:**
```powershell
.\dev.ps1              # Starts PostgreSQL + Backend API + Frontend
```

**Bash:**
```bash
./dev.sh               # Starts PostgreSQL + Backend API + Frontend
```

The script handles everything: starts Docker containers, waits for Postgres, builds and launches the API, installs npm packages, and starts the Vite dev server.

### Quick Start Options

| Command | What it does |
|---------|-------------|
| `.\dev.ps1` | Start everything (Postgres + API + Frontend) |
| `.\dev.ps1 -Gpu` | Also start vLLM inference server (requires NVIDIA GPU) |
| `.\dev.ps1 -BackendOnly` | Just Postgres + API (no frontend) |
| `.\dev.ps1 -FrontendOnly` | Just the frontend dev server |
| `.\dev.ps1 -Stop` | Stop all running services |

### Manual Start (step by step)

```bash
# 1. Start PostgreSQL (port 5438)
docker compose up -d

# 2. Start backend API (port 5000) — new terminal
cd backend
dotnet run --project src/Prism.Api --urls http://localhost:5000

# 3. Start frontend dev server (port 5173) — new terminal
cd frontend
npm install   # first time only
npm run dev
```

### What's Running

| Service | URL | Notes |
|---------|-----|-------|
| **Frontend** | http://localhost:5173 | Vite dev server with hot reload |
| **Backend API** | http://localhost:5000 | .NET Minimal API |
| **Swagger UI** | http://localhost:5000/swagger | API documentation (dev only) |
| **Health Check** | http://localhost:5000/health | Returns `Healthy` when API is up |
| **PostgreSQL** | localhost:5438 | pgvector-enabled, data persisted in Docker volume |
| **vLLM** | http://localhost:8000 | Only with `--gpu` flag |

### Environment Variables

Copy `.env.example` to `.env` and configure:

```env
# Database
DATABASE__CONNECTIONSTRING=Host=localhost;Port=5438;Database=prism;Username=postgres;Password=postgres

# Inference (default vLLM)
INFERENCEPROVIDERS__0__NAME=Local vLLM
INFERENCEPROVIDERS__0__TYPE=Vllm
INFERENCEPROVIDERS__0__ENDPOINT=http://localhost:8000

# Frontend
VITE_API_URL=http://localhost:5000
```

## Development

```bash
# Run tests
cd backend && dotnet test

# Generate TypeScript API client (after backend API changes)
cd frontend && npm run api:generate

# Add a database migration
dotnet ef migrations add MigrationName \
  --project src/Prism.Common \
  --startup-project src/Prism.Api

# Format
cd backend && dotnet format
cd frontend && npm run lint
```

## Project Documentation

| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Full architecture: structure, patterns, abstractions, interfaces |
| [DESIGN.md](DESIGN.md) | Vision, features, wireframes, data models, API surface |
| [PROJECT_PLAN.md](PROJECT_PLAN.md) | Phased task breakdown (~150 tasks across 5 phases) |
| [docs/ADR/](docs/ADR/) | Architecture Decision Records (16 ADRs) |
| [docs/product-truth.yaml](docs/product-truth.yaml) | Machine-readable status of every module |
| [docs/module-ownership.md](docs/module-ownership.md) | Module-to-slice mapping and dependency rules |
| [CLAUDE.md](CLAUDE.md) | Development guidelines for AI-assisted coding |

## Roadmap

- **Phase 1 (Walk):** Playground, Token Explorer, Tokenizer, Model Management, History — *implemented*
- **Phase 2 (Jog):** Prompt Lab, Experiments, Workspaces — *implemented*
- **Phase 3 (Run):** Datasets, Evaluation, Batch Inference, Analytics — *implemented*
- **Phase 4 (Sprint):** RAG Workbench, Structured Output — *implemented*
- **Phase 5 (Fly):** Agent Builder, Notebooks, Fine-Tuning — *implemented*

All 14 modules have backend handlers, API endpoints, and frontend pages. 56 backend unit tests. CI pipeline with 8 jobs.

## Contributing

Contributions are welcome. Please read [CLAUDE.md](CLAUDE.md) for architecture rules and coding conventions before submitting a PR. The project follows vertical slice architecture — see [SKILLS.md](SKILLS.md) for step-by-step guides on common tasks.

## License

[MIT](LICENSE)
