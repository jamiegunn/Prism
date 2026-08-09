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

Each module has a how-to guide in [docs/features/](docs/features/) covering what it is for, the
steps to do the common jobs, what every setting means, and what it will not do.

| Module | What It Does | Guide |
|--------|-------------|-------|
| **Playground** | Chat with streaming, logprobs heatmaps, entropy charts, surprise highlighting | [Guide](docs/features/playground.md) |
| **Token Explorer** | Next-token prediction, step-through generation, branch exploration, sampling visualization | [Guide](docs/features/token-explorer.md) |
| **Tokenizer Explorer** | Visualize tokenization, compare tokenizers across models, cost estimation | [Guide](docs/features/tokenizer.md) |
| **Model Management** | Register providers, monitor health/metrics, hot-swap models, KV cache visualization | [Guide](docs/features/models.md) |
| **History & Replay** | Browse all inference history, tag, filter, replay with overrides, diff results | [Guide](docs/features/history.md) |
| **Prompt Lab** | Template editor with variables, version control, few-shot management | [Guide](docs/features/prompt-lab.md) |
| **Experiments** | Track runs, compare metrics, run parameter sweeps, export results | [Guide](docs/features/experiments.md) |
| **Workspaces** | Group projects under a named workspace | [Guide](docs/features/workspaces.md) |
| **Datasets** | Upload, browse, split, compute statistics, export | [Guide](docs/features/datasets.md) |
| **Evaluation** | Scoring methods (exact match, contains, ROUGE-L, BLEU, length ratio), leaderboards | [Guide](docs/features/evaluation.md) |
| **Batch Inference** | Run prompts at scale with progress tracking, pause and resume | [Guide](docs/features/batch-inference.md) |
| **Analytics** | Usage, latency percentiles and token totals across every module | [Guide](docs/features/analytics.md) |
| **RAG Workbench** | Ingest documents, chunking strategies, vector/BM25/hybrid search | [Guide](docs/features/rag-workbench.md) |
| **Structured Output** | Guided decoding with JSON schema constraints, output validation | [Guide](docs/features/structured-output.md) |
| **Agent Builder** | ReAct agents with tool use and step-by-step execution traces | [Guide](docs/features/agents.md) |
| **Fine-Tuning** | Export datasets in Alpaca, ShareGPT, ChatML and OpenAI formats; register LoRA adapters | [Guide](docs/features/fine-tuning.md) |
| **Notebooks** | Store and edit `.ipynb` research notebooks | [Guide](docs/features/notebooks.md) |

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

| Requirement | Version | Why, and how it is pinned |
|---|---|---|
| [.NET SDK](https://dotnet.microsoft.com/download) | **10.0.100 or later 10.0.x** | Pinned by [`global.json`](global.json). The projects target `net9.0` but do not need a 9.0 runtime — [`backend/Directory.Build.props`](backend/Directory.Build.props) sets `RollForward=Major`, so they run on the 10.0 runtime you already have. |
| [Node.js](https://nodejs.org/) | **22** | Pinned by [`frontend/.nvmrc`](frontend/.nvmrc). `nvm use` in `frontend/` picks it up. 20 also works; 22 is what CI runs. |
| [Docker](https://www.docker.com/) | any recent | Runs PostgreSQL 16 + pgvector on port 5438. Also the fallback the integration tests use when `PRISM_TEST_DB` is unset. |
| An LLM inference server | — | Prism needs at least one running model to do anything. See [Setting Up an LLM](#setting-up-an-llm). |

### Check, and fix, your machine in one command

```bash
./scripts/doctor.sh
```

It finds the SDK even when it is not on `PATH`, loads Node through `nvm`, installs frontend
packages, starts Docker Desktop and the Postgres container, and tells you the one line to add
to your shell profile. Run it after cloning, after a machine rebuild, or any time something
stops working. It only asks you to intervene for things it genuinely cannot do, such as
installing an SDK that is not there at all.

If you would rather check by hand:

```bash
dotnet --list-sdks       # expect a 10.0.x
node --version           # expect v22.x
docker info              # expect no error
```

A missing .NET 9 runtime is **not** a problem — `RollForward=Major` covers it. If you see
*"You must install or update .NET to run this application"* from `dotnet test`, you are on a
checkout from before that was set; `git pull` rather than installing anything.

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

Supports streaming and structured output (via `format`). **Does not return logprobs**, so the
token heatmap, entropy chart, surprise highlighting and Token Explorer will be empty — those
views need per-token probabilities that Ollama's API does not expose. Use vLLM if token-level
introspection is the reason you are here. No tokenization endpoint either.

**Option 3: LM Studio (GUI, works on CPU)**

Download from https://lmstudio.ai, load a model, and start the local server (default port 1234).

Register in Prism with:
- **Endpoint:** `http://localhost:1234/v1`
- **Provider Type:** LM Studio

Note that "LM Studio" is currently an alias for the generic OpenAI-compatible provider, so
LM Studio's own model load/unload API is not used and hot-swap is reported as unsupported.

**Option 4: Any OpenAI-compatible API**

Any server that implements the `/v1/chat/completions` endpoint works — including OpenAI itself, Together AI, Groq, etc.

### Provider Capability Comparison

| Feature | vLLM | Ollama | LM Studio | OpenAI API |
|---------|------|--------|-----------|------------|
| Chat + Streaming | Yes | Yes | Yes | Yes |
| Logprobs (token heatmaps) | Yes (up to 20) | No | No | Yes |
| Tokenization | Yes | No | No | No |
| Guided Decoding | Yes (`guided_json`) | Yes (`format`) | No | Yes (`json_schema`) |
| GPU Metrics | Yes | No | No | No |
| Model Hot-Swap | No | Yes | Yes | N/A |

Prism probes these on registration and disables the controls a provider cannot serve, rather
than letting them fail silently. Where a capability is unprobed the UI says so — "unprobed" and
"unavailable" are different facts and are shown differently.

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

## Building

```bash
# Backend — warnings are errors, so a clean build means clean
cd backend
dotnet restore Prism.sln
dotnet build Prism.sln

# Frontend
cd frontend
npm ci                    # not `npm install` — respects the lockfile exactly
npm run build             # tsc -b && vite build, output in dist/
```

`dotnet build` treats warnings as errors across the solution. If it fails on something that
looks trivial, that is deliberate — the build is the first gate, not a suggestion.

## Testing

There are two suites and they run separately.

### Backend — 155 tests

```bash
cd backend
dotnet test Prism.sln
```

Roughly two thirds are unit tests with no external dependency. The rest are integration tests
that need **a real PostgreSQL with pgvector** — they exercise `FOR UPDATE SKIP LOCKED` job
claiming, `percentile_cont` aggregation and vector search, none of which have a meaningful
in-memory equivalent.

You have two ways to give them a database.

**Point them at a running Postgres** (faster, and works without a Docker daemon):

```bash
docker compose up -d
export PRISM_TEST_DB="Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres"
dotnet test Prism.sln
```

**Or let them start their own container** — leave `PRISM_TEST_DB` unset and the suite launches
a `pgvector/pgvector:pg16` container through Testcontainers. This needs Docker running and adds
container startup to every run.

> The test fixture **empties whatever database you point it at** on startup, so that a
> long-lived database behaves the same way a throwaway container does. Use a database that
> exists only for tests. Pointing `PRISM_TEST_DB` at a database named `prism` is refused
> outright, because that is the one the application itself uses.

Useful filters:

```bash
dotnet test Prism.sln --filter "FullyQualifiedName~Unit"          # no database needed
dotnet test Prism.sln --filter "FullyQualifiedName~Integration"
dotnet test Prism.sln --filter "FullyQualifiedName~JobWorker"
```

### Frontend — 27 tests

```bash
cd frontend
npm test                  # vitest, single run
npm run test:watch        # re-runs on change
npm run test:coverage
```

No database, no backend, no browser — jsdom plus Testing Library, with `fetch` stubbed per
test. These cover the logprob maths (perplexity, Shannon entropy in bits) and the components
that make claims about backend state.

### The full gate

This is exactly what CI runs and what the pre-commit hook runs:

```bash
docker compose up -d
export PRISM_TEST_DB="Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres"

cd backend
dotnet build Prism.sln                       # 0 warnings, 0 errors
dotnet format Prism.sln --verify-no-changes  # clean
dotnet test Prism.sln                        # 155 passed

cd ../frontend
npm ci
npx tsc -b --noEmit                          # clean
npm run lint                                 # 0 errors
npm test                                     # 27 passed
```

## Pre-commit hooks

The gate above can run automatically before every commit:

```bash
./scripts/install-hooks.sh
```

That points `core.hooksPath` at [`.githooks/`](.githooks/), so the hooks are version-controlled
and reviewed like any other code rather than living untracked in `.git/hooks`. Each clone needs
to run it once — git has no way to do this for you. It finishes by running `doctor.sh`, so if
anything is missing you find out while you are setting up rather than mid-commit later.

The hook only runs the half you touched:

| What you staged | What runs | Roughly |
|---|---|---|
| Docs, markdown, anything outside `backend/` and `frontend/` | nothing | instant |
| `frontend/` only | typecheck, lint, vitest | ~30s |
| `backend/` only | build, `dotnet format`, xunit | ~60s |
| Both, or `global.json` / `scripts/` / `.githooks/` | everything | ~90s |

### It provisions rather than complains

Most of what used to stop a commit is now fixed on the spot:

| Situation | What happens |
|---|---|
| `dotnet` not on `PATH` | Looked for in the usual install locations, including `/usr/local/share/dotnet` and `~/.dotnet` |
| No .NET 9 runtime | Nothing to do — `RollForward=Major` handles it; the hook also exports `DOTNET_ROLL_FORWARD` as a belt-and-braces measure |
| `npm` only visible via `nvm` | `nvm.sh` is sourced |
| `frontend/node_modules` missing or stale | `npm ci` runs |
| Postgres not running | `docker compose up -d postgres`, then waits for it |
| `PRISM_TEST_DB` unset | Set automatically once a local Postgres answers |
| `prism_test` database does not exist | Created by the test fixture on first connection |
| `PRISM_TEST_DB` set but unreachable | Said out loud, rather than letting sixty tests fail with connection errors and calling that a red suite |

It still refuses to guess where it should not. No SDK on the machine, or Docker not running,
stops the commit — with `./scripts/doctor.sh` named as the way out. A suite that quietly skipped
its integration half is worse than no gate at all.

You do not have to set `PRISM_TEST_DB` yourself. Setting it is still worth doing if you want a
different server, or to save the hook a few seconds:

```bash
export PRISM_TEST_DB="Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres"
```

> The test fixture **empties that database on every run**. Point it at a database that exists
> only for tests. One named `prism` is refused outright, since that is the application's own.

Two escape hatches:

```bash
git commit --no-verify        # skip the gate for one commit
PRISM_HOOK_STRICT=1 git commit  # opposite: refuse to run if the working tree differs from the index
```

`PRISM_HOOK_STRICT` exists because the hook tests the working tree, not the staged content.
When everything is staged those are the same thing. When they are not, the hook warns and names
the files; setting `PRISM_HOOK_STRICT=1` turns that warning into a refusal.

## Other development tasks

```bash
# Generate the TypeScript API client (after changing backend endpoints)
cd frontend && npm run api:generate

# Add a database migration
cd backend
dotnet ef migrations add MigrationName \
  --project src/Prism.Common \
  --startup-project src/Prism.Api

# Format
cd backend && dotnet format
cd frontend && npm run lint -- --fix
```

## Project Documentation

| Document | Description |
|----------|-------------|
| [docs/features/](docs/features/) | How to use each module — one task-oriented guide per feature |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Full architecture: structure, patterns, abstractions, interfaces |
| [DESIGN.md](DESIGN.md) | Vision, features, wireframes, data models, API surface |
| [PROJECT_PLAN.md](PROJECT_PLAN.md) | Phased task breakdown (~150 tasks across 5 phases) |
| [docs/ADR/](docs/ADR/) | Architecture Decision Records (16 ADRs) |
| [docs/product-truth.yaml](docs/product-truth.yaml) | Machine-readable status of every module |
| [docs/module-ownership.md](docs/module-ownership.md) | Module-to-slice mapping and dependency rules |
| [CLAUDE.md](CLAUDE.md) | Development guidelines for AI-assisted coding |

## Status

Every module has backend handlers, API endpoints and a frontend page. 155 backend tests and 27
frontend tests. CI runs 9 jobs, all of which gate the build.

That is not the same as every module being finished, and the difference matters if you are
choosing what to rely on:

- **Works end to end from the browser:** Playground, Token Explorer, Tokenizer, Model
  Management, History, Prompt Lab, Experiments, Datasets, RAG Workbench (retrieval), Structured
  Output, Agent Builder, Notebooks, Fine-Tuning export.
- **Runs, but has to be started through the HTTP API:** Evaluation and Batch Inference. Both
  have working background workers; neither has a create form yet.
- **Not wired up:** LoRA adapters can be registered but never reach an inference call. The
  Notebooks page links to a JupyterLite build that is not in the repository. Workspaces are a
  dropdown that does not yet filter anything.

Each feature guide in [docs/features/](docs/features/) states its own limitations at the point
where you would hit them. [docs/product-truth.yaml](docs/product-truth.yaml) holds the
machine-readable per-module position, and [docs/assumptions.md](docs/assumptions.md) records
what has been proven and what was falsified. Where this file and `product-truth.yaml` disagree,
`product-truth.yaml` is correct.

## Contributing

Contributions are welcome. Please read [CLAUDE.md](CLAUDE.md) for architecture rules and coding conventions before submitting a PR. The project follows vertical slice architecture — see [SKILLS.md](SKILLS.md) for step-by-step guides on common tasks.

## License

[MIT](LICENSE)
