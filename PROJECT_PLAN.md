# Prism — Project Plan & Task Breakdown

## Research Feature Deep-Dive

Before the task list, here's the thinking behind the research-critical features that distinguish this from a chat wrapper. These capabilities inform the task breakdown.

### Logprobs & Token Probability Analysis
vLLM exposes `logprobs` and `top_logprobs` via its OpenAI-compatible API. This is the backbone of nearly every research feature.

**What you can do with logprobs:**
- **Token heatmaps**: Color each output token by log probability. Green = confident, red = uncertain. Immediately shows where the model is "guessing."
- **Alternative token explorer**: Click any token, see the top-K alternatives and their probabilities. "The model almost said X instead of Y" is enormously useful for understanding behavior.
- **Perplexity scoring**: Compute per-response perplexity (exp of average negative log probability). Lower = more confident. Compare across models, prompts, temperatures.
- **Entropy per position**: Shannon entropy of the probability distribution at each token. High entropy = model is torn between options. Low entropy = model is certain.
- **Surprise detection**: Flag tokens where probability drops below a threshold. These are the "interesting" tokens where the model made a non-obvious choice.
- **Calibration analysis**: For evaluation, compare predicted probability vs actual correctness. Is the model overconfident? Underconfident? This is a proper research metric.
- **Prompt sensitivity**: Run the same prompt with tiny variations, compare logprobs distributions. Shows how stable the model's "reasoning" is.

### Next-Token Prediction & Step-Through
This is a killer research feature — step through generation one token at a time.

**How it works:**
- User types a prompt
- System shows top-N next tokens with probabilities as a ranked list + bar chart
- User can either: let the model pick (sample), force a specific token, or explore branches
- Creates a tree of possibilities — like a "choose your own adventure" for generation
- At each step, see the full probability distribution, entropy, and how the distribution shifts
- Compare: what happens if you force token A vs token B? How does the rest of generation change?

**Why this matters for research:**
- Understand HOW the model makes decisions, not just the final output
- Explore counterfactuals: "what if the model had said X instead?"
- Debug prompt engineering: see exactly where the model goes off track
- Study sampling: compare greedy vs top-k vs top-p at each position
- Visualize beam search paths

### KV Cache Visualization & Analysis
vLLM uses PagedAttention for KV cache management. Monitoring this reveals model behavior.

**What to surface:**
- **Cache utilization**: Real-time KV cache usage per instance (vLLM exposes this via metrics)
- **Prefix caching**: vLLM supports automatic prefix caching — show cache hit rates, which prefixes are cached, estimated speedup from caching
- **Context window usage**: For each request, show how much of the model's context window is consumed (prompt tokens / max context). Warn when approaching limits.
- **Memory pressure indicators**: When KV cache is full, vLLM queues or evicts. Show when this is happening and its impact on latency.
- **Batch scheduling view**: How many sequences are running concurrently? What's the dynamic batching doing?

### Tokenizer Explorer
Understanding tokenization is fundamental to prompt engineering.

**Capabilities:**
- Paste text, see how it tokenizes (token boundaries, IDs, byte-pair encoding)
- Compare tokenizers across models (llama vs mistral vs qwen)
- Token count as you type (already planned, but with breakdown: special tokens, BOS/EOS, etc.)
- Identify tokenizer edge cases: words that split unexpectedly, Unicode handling, code tokenization
- Estimate cost from token count

### Sampling Strategy Comparison
Different sampling strategies produce wildly different outputs. Make this visible.

**Capabilities:**
- Run same prompt with different sampling configs side-by-side
- Greedy vs top-k vs top-p vs min-p vs typical sampling vs temperature
- Show how the effective vocabulary changes at each position for each strategy
- Parameter sweep: generate N outputs with a temperature range, show diversity metrics
- Repetition analysis: detect and visualize repetition patterns at different temperatures

### Attention Analysis (Future / If vLLM Exposes It)
This is aspirational — vLLM doesn't currently expose attention weights. But plan for it.

**If available:**
- Attention heatmaps (which input tokens does each output token attend to?)
- Head-level analysis (which attention heads are active?)
- Layer-by-layer attention flow

**Alternative without raw attention:**
- Token attribution via input perturbation (mask tokens in prompt, measure output change)
- This can be done with existing logprobs support

---

## Project Plan

---

### PHASE 1: Walk (Foundation)
**Goal:** Talk to a model through your own UI, see logprobs, save results.
**Duration estimate: removed — just build it feature by feature.**

---

#### 1.1 Project Scaffolding

| # | Task | Details |
|---|------|---------|
| 1.1.1 | Create .NET 9 Web API project | `dotnet new webapi -n Prism.Api` in `backend/src/`. Use Minimal API style. Add global usings, configure for development. |
| 1.1.2 | Create Common class library | `dotnet new classlib -n Prism.Common` in `backend/src/`. Shared kernel: Result pattern, abstractions, DB, cache, storage, inference interfaces. No feature dependencies. |
| 1.1.3 | Create Features class library | `dotnet new classlib -n Prism.Features` in `backend/src/`. Vertical slices — one folder per feature. References Common. |
| 1.1.4 | Create solution file | `dotnet new sln` in `backend/`. Add all three projects. Set up project references (Api -> Core, Infrastructure; Infrastructure -> Core). |
| 1.1.5 | Create React + TypeScript frontend | `npm create vite@latest frontend -- --template react-ts` in project root. Add Tailwind CSS, shadcn/ui. |
| 1.1.6 | Configure shadcn/ui | Init shadcn, add base components: Button, Input, Card, Tabs, Select, Slider, ScrollArea, Sheet, Dialog, Tooltip. |
| 1.1.7 | Set up Docker Compose | `docker-compose.yml` with PostgreSQL 16 + pgvector image. Expose port 5432. Volume for persistence. Environment variables for connection string. |
| 1.1.8 | Configure EF Core | Add Npgsql.EntityFrameworkCore.PostgreSQL to Infrastructure. Create `AppDbContext`. Configure connection string in Api `Program.cs`. |
| 1.1.9 | Set up API CORS & base config | Allow frontend origin. Add JSON serialization options (camelCase, ignore nulls). Add health check endpoint. |
| 1.1.10 | Create initial EF migration | Empty initial migration to verify DB connectivity. Add migration script to docker-compose startup. |
| 1.1.11 | Frontend API client setup | Create `src/services/api.ts` with base fetch wrapper. Configure base URL from env var. Add SSE helper utility. |
| 1.1.12 | Frontend routing setup | Install react-router-dom. Create shell layout with sidebar navigation (placeholder items for all 13 modules). Default route to Playground. |
| 1.1.13 | Create .env files | `.env.development` for frontend (API URL). `appsettings.Development.json` for backend (DB connection, vLLM endpoint). |

---

#### 1.2 vLLM Client & Model Management (Connect & Observe)

| # | Task | Details |
|---|------|---------|
| 1.2.1 | Define `VllmInstance` domain model | In Core: Id, Name, Endpoint (URL), Status (enum: Unknown, Healthy, Unhealthy, Unreachable), ModelId (string, e.g., "meta-llama/..."), GpuConfig (JSON), MaxContextLength, SupportsLogprobs, SupportsGuidedDecoding, CreatedAt, LastHealthCheck. |
| 1.2.2 | Create `IVllmClient` interface | In Core: `Task<ModelInfo> GetModelInfo(string endpoint)`, `Task<HealthStatus> CheckHealth(string endpoint)`, `Task<VllmMetrics> GetMetrics(string endpoint)`, `IAsyncEnumerable<string> StreamChatCompletion(...)`, `Task<ChatCompletion> ChatCompletion(...)`. |
| 1.2.3 | Implement `VllmClient` using HttpClient | In Infrastructure. Calls vLLM's OpenAI-compatible endpoints: `/v1/models`, `/v1/chat/completions`, `/health`. Parse SSE stream for streaming. Use `IHttpClientFactory`. |
| 1.2.4 | Implement model info parsing | Parse `/v1/models` response. Extract: model ID, max_model_len (context length), dtype, tensor_parallel_size. Call `/v1/models/{id}` for detailed info if available. |
| 1.2.5 | Implement vLLM metrics scraping | vLLM exposes Prometheus metrics at `/metrics`. Parse: `vllm:num_requests_running`, `vllm:num_requests_waiting`, `vllm:gpu_cache_usage_perc`, `vllm:cpu_cache_usage_perc`, `vllm:avg_generation_throughput_toks_per_s`, `vllm:num_preemptions_total`. |
| 1.2.6 | Create `VllmInstance` EF entity & migration | Map domain model to PostgreSQL. Add `DbSet<VllmInstance>` to context. Create migration. |
| 1.2.7 | Create instance registration API endpoints | `POST /api/v1/models/instances` — register endpoint, immediately probe for model info + health. `GET /api/v1/models/instances` — list all with current status. `DELETE /api/v1/models/instances/{id}` — unregister. `GET /api/v1/models/instances/{id}/metrics` — current metrics. |
| 1.2.8 | Background health check service | `BackgroundService` that polls all registered instances every 30s. Updates status and metrics in DB. Detects: logprobs support (try a request with logprobs=true), guided decoding support. |
| 1.2.9 | Frontend: Model Management page | List registered instances with status indicators. Show model name, endpoint, status (green/red dot), GPU memory %, KV cache %, throughput. "Register Instance" dialog: enter endpoint URL, auto-detect model. |
| 1.2.10 | Frontend: Instance detail panel | Click an instance to see: full model info, GPU config, real-time metrics (auto-refresh every 5s), supported features (logprobs, guided decoding). |
| 1.2.11 | Frontend: KV Cache visualization | Within instance detail: show KV cache usage as a gauge/bar. Show GPU cache % and CPU cache %. Color code: green < 70%, yellow 70-90%, red > 90%. Show prefix cache hit rate if available. |

---

#### 1.3 Inference Playground (Single Pane + Logprobs)

| # | Task | Details |
|---|------|---------|
| 1.3.1 | Define `ChatMessage` and `ChatRequest` models | In Core: ChatMessage { Role (system/user/assistant), Content }. ChatRequest { Model, Messages[], Temperature, TopP, TopK, MaxTokens, StopSequences[], FrequencyPenalty, PresencePenalty, RepetitionPenalty, Logprobs (bool), TopLogprobs (int, 1-20), Stream (bool) }. |
| 1.3.2 | Define `ChatResponse` and `LogprobsData` models | ChatResponse { Id, Model, Content, FinishReason, Usage { PromptTokens, CompletionTokens, TotalTokens }, Latency, TokensPerSecond }. LogprobsData { Tokens[] { Token, Logprob, TopLogprobs[] { Token, Logprob } } }. |
| 1.3.3 | Implement SSE streaming endpoint | `POST /api/v1/inference/chat` with `Accept: text/event-stream`. Proxy to vLLM's `/v1/chat/completions` with `stream: true`. Forward SSE chunks to client. Collect timing data (TTFT, total latency, tokens/sec). |
| 1.3.4 | Implement non-streaming chat endpoint | `POST /api/v1/inference/chat` (without SSE accept header or with `stream: false` body param). Return complete response with full logprobs data if requested. Compute perplexity from logprobs. |
| 1.3.5 | Implement logprobs extraction | When logprobs requested: parse `logprobs` field from vLLM response. For streaming: accumulate logprobs per token as chunks arrive. For non-streaming: extract from complete response. Return structured `LogprobsData`. |
| 1.3.6 | Compute derived metrics from logprobs | Perplexity: exp(mean(-logprob)) over all tokens. Entropy per token: -sum(p * log(p)) for top-K probs (approximate). Surprise flags: mark tokens where logprob < threshold (configurable, default -2.0). |
| 1.3.7 | Define `Conversation` and `Message` persistence models | Conversation { Id, Title (auto-generated from first message), Model, Parameters (JSON), CreatedAt, UpdatedAt }. Message { Id, ConversationId, Role, Content, TokenCount, LogprobsData (JSON, nullable), Perplexity (nullable), CreatedAt, OrderIndex }. |
| 1.3.8 | Create Conversation EF entities & migration | Map to PostgreSQL. Conversation has many Messages. Index on CreatedAt for listing. |
| 1.3.9 | Create conversation persistence endpoints | `POST /api/v1/conversations` — create. `GET /api/v1/conversations` — list (paginated, most recent first). `GET /api/v1/conversations/{id}` — get with messages. `DELETE /api/v1/conversations/{id}` — delete. Auto-save: every assistant response persists the conversation. |
| 1.3.10 | Frontend: Playground layout | Left sidebar: parameter controls. Center: chat area. Model selector dropdown (populated from registered instances). Parameter sliders/inputs: temperature (0-2, step 0.1), top_p (0-1), top_k (0-100), max_tokens (1-model_max), stop sequences (tag input), frequency_penalty (-2 to 2), presence_penalty (-2 to 2). |
| 1.3.11 | Frontend: Chat interface | Message list with user/assistant bubbles. Markdown rendering for assistant messages (use react-markdown). Auto-scroll to bottom on new tokens. Input area: textarea with Shift+Enter for newline, Enter to send. System prompt input (collapsible panel above chat). |
| 1.3.12 | Frontend: SSE streaming integration | Connect to SSE endpoint. Parse `data:` lines. Append tokens to current assistant message in real-time. Show typing indicator while streaming. Handle `[DONE]` signal. Display tokens/sec counter during streaming. |
| 1.3.13 | Frontend: Token usage display | After each response: show prompt tokens, completion tokens, total tokens. Show latency (total time), time-to-first-token (TTFT), tokens-per-second. Estimate cost (configurable $/token rates per model in settings). |
| 1.3.14 | Frontend: Logprobs toggle & controls | In parameter sidebar: "Logprobs" section. Enable/disable toggle. Top-K selector (1-20, default 5). When enabled, requests include logprobs parameters. |
| 1.3.15 | Frontend: Token heatmap view | Below or overlaid on assistant response: render each token as a colored span. Color gradient: bright green (logprob >= -0.1, ~90%+ prob) through yellow (-1.0) to red (< -3.0, < 5% prob). Tooltip on hover: show exact probability, logprob value, rank among top-K. Toggle between: normal view / heatmap view / inline heatmap (colored text). |
| 1.3.16 | Frontend: Alternative tokens panel | Click any token in heatmap to open a detail panel. Show: selected token with probability, bar chart of top-K alternatives with probabilities, entropy at this position, whether this token was the "greedy" choice (rank 1) or a sampled lower-rank choice. |
| 1.3.17 | Frontend: Perplexity display | Show per-response perplexity as a number next to token usage. Color code: green (< 3), yellow (3-6), red (> 6). Tooltip: "Perplexity measures model confidence. Lower = more confident." |
| 1.3.18 | Frontend: Entropy visualization | Optional toggle: show entropy bar under each token in heatmap view. Small bar chart: height proportional to entropy. High bars = model was uncertain at that position. |
| 1.3.19 | Frontend: Surprise highlighting | When logprobs enabled: automatically underline/highlight tokens below surprise threshold. Threshold configurable in settings (default: probability < 10%). These are the "interesting" tokens where the model made a surprising choice. |
| 1.3.20 | Frontend: Conversation history sidebar | Collapsible panel: list saved conversations (title, model, date). Click to reload. Search conversations. Delete individual conversations. |
| 1.3.21 | Frontend: Export conversation | Button to export current conversation as: JSON (full data including logprobs), Markdown (human-readable), JSONL (one message per line). |
| 1.3.22 | Frontend: System prompt library | Quick-swap dropdown for system prompts. Save current system prompt to library (name + tags). Manage library (edit, delete, categorize). Pre-populate with a few useful defaults (general assistant, code helper, JSON extractor, etc.). |

---

#### 1.4 Next-Token Prediction Explorer

This is a standalone research feature that lives within the Playground as a tab/mode.

| # | Task | Details |
|---|------|---------|
| 1.4.1 | Implement next-token prediction endpoint | `POST /api/v1/inference/next-token` — Takes: messages[] (or raw prompt), model, temperature, top_k_display (how many alternatives to show, default 20). Implementation: call vLLM with `max_tokens: 1, logprobs: true, top_logprobs: 20`. Return: top-N tokens with probabilities, entropy, full distribution stats. |
| 1.4.2 | Implement step-through generation endpoint | `POST /api/v1/inference/step` — Takes: messages[], model, selected_token (optional, null = let model sample), step_history[]. Appends selected_token to conversation, calls next-token prediction for the next position. Returns: updated conversation + next token predictions. This enables stepping through generation one token at a time. |
| 1.4.3 | Implement branch exploration endpoint | `POST /api/v1/inference/branch` — Takes: messages[], branch_token (force this token), then_generate: N (generate N more tokens after forced token). Returns: the branch continuation. Enables "what if the model had said X?" counterfactual exploration. |
| 1.4.4 | Frontend: Next-Token Prediction mode | New tab in Playground: "Token Explorer". Input area for prompt (can import from chat). Large panel showing top-N predicted next tokens as a ranked list with: token text, probability (%), logprob, bar visualization of probability. |
| 1.4.5 | Frontend: Probability distribution chart | Bar chart (horizontal) showing top-20 next tokens and their probabilities. Cumulative probability line overlay. Show where top-p and top-k cutoffs would fall for current settings. Color code: tokens that would be sampled (within top-p/top-k) vs filtered out. |
| 1.4.6 | Frontend: Step-through interface | "Step" button: model picks next token (using current sampling settings), appends to output, shows new predictions. "Force" action: click any token in the list to force it as next token. Output area shows the growing text with each token colored by its probability when chosen. Undo button to step back. |
| 1.4.7 | Frontend: Branch exploration | Right-click a token in the prediction list -> "Explore this branch". Opens a split view: current path vs branch path. Shows how generation diverges after forcing a different token. Visual tree view of explored branches. |
| 1.4.8 | Frontend: Sampling visualization | Side panel showing: effective vocabulary size at current position (tokens above min probability), temperature effect (show how distribution flattens/sharpens), top-p cumulative cutoff visualized on the bar chart, top-k cutoff line on the bar chart. Toggle between sampling strategies to see how they change the effective distribution. |
| 1.4.9 | Frontend: Generation tree view | Collapsible tree visualization of all explored branches. Each node: token (probability). Highlight the "greedy" path (always picking rank-1). Highlight the actual sampled path. Show branch points where you forced alternative tokens. |

---

#### 1.5 Tokenizer Explorer

| # | Task | Details |
|---|------|---------|
| 1.5.1 | Implement tokenizer info endpoint | `GET /api/v1/models/instances/{id}/tokenizer` — Query vLLM for tokenizer info. Return: vocab size, model type, special tokens (BOS, EOS, PAD, etc.), tokenizer family (BPE, SentencePiece, etc.). |
| 1.5.2 | Implement tokenize endpoint | `POST /api/v1/inference/tokenize` — Takes: text, model. Calls vLLM's `/tokenize` endpoint (if available) or use the tokenizer from the model info. Returns: token IDs, token strings, byte representations, character spans (which characters map to which token). |
| 1.5.3 | Implement detokenize endpoint | `POST /api/v1/inference/detokenize` — Takes: token_ids[], model. Returns decoded text. Useful for inspecting specific tokens by ID. |
| 1.5.4 | Frontend: Tokenizer Explorer page | Text input area: paste or type text. Below: tokenized output showing each token as a distinct colored block (alternating colors for visual separation). Token display: text representation, token ID, byte length. Total token count with breakdown (text tokens + special tokens). |
| 1.5.5 | Frontend: Token detail on hover/click | Hover a token block: tooltip with token ID, byte representation (hex), Unicode codepoints, position index. Click: highlight all occurrences of same token in the text. |
| 1.5.6 | Frontend: Multi-model tokenizer comparison | Side-by-side: tokenize same text with 2+ models. Show differences in tokenization: different token boundaries, different token counts, tokens unique to each tokenizer. |
| 1.5.7 | Frontend: Token cost estimator | Below tokenizer output: show estimated cost based on per-model token pricing. "This text would cost ~$X.XX as a prompt with [model]." |

---

### PHASE 2: Jog (Research Core)
**Goal:** Systematic prompt development and experiment tracking.

---

#### 2.1 Prompt Engineering Lab

| # | Task | Details |
|---|------|---------|
| 2.1.1 | Define `PromptTemplate` and `PromptVersion` domain models | PromptTemplate { Id, ProjectId, Name, Category, Tags[], CreatedAt, UpdatedAt }. PromptVersion { Id, TemplateId, Version (int, auto-increment), SystemPrompt, UserTemplate, Variables[] (JSON: name, type, default, description), FewShotExamples[] (JSON: input, output), CreatedAt, Notes, CreatedBy }. |
| 2.1.2 | Create EF entities & migration | Map to PostgreSQL. PromptTemplate has many PromptVersions. Index on Name, Category, Tags (GIN index for array). |
| 2.1.3 | Create prompt CRUD endpoints | `POST /api/v1/prompts` — create template. `GET /api/v1/prompts` — list with filtering (category, tags, search). `GET /api/v1/prompts/{id}` — template with latest version. `PUT /api/v1/prompts/{id}` — update metadata. `DELETE /api/v1/prompts/{id}`. |
| 2.1.4 | Create version endpoints | `POST /api/v1/prompts/{id}/versions` — create new version (copies latest, applies changes). `GET /api/v1/prompts/{id}/versions` — list all versions. `GET /api/v1/prompts/{id}/versions/{v}` — specific version. `GET /api/v1/prompts/{id}/diff?v1=X&v2=Y` — diff between versions. |
| 2.1.5 | Implement template variable rendering | Engine to parse `{{variable_name}}` in user template. Given a dict of variable values, render the final prompt. Validate: all declared variables are provided, no undeclared variables used. Return rendered prompt + token count. |
| 2.1.6 | Implement prompt test endpoint | `POST /api/v1/prompts/{id}/test` — Takes: version (optional, default latest), variables (dict), model, parameters, logprobs settings. Renders template, calls model, returns response with metrics. Optionally saves as experiment run. |
| 2.1.7 | Implement A/B test endpoint | `POST /api/v1/prompts/ab-test` — Takes: variations[] (each: prompt_version_id + variables), models[], parameter_sets[], num_runs_per_combo. Creates experiment, enqueues all combinations. Returns experiment ID. Uses BackgroundService to execute. |
| 2.1.8 | Frontend: Prompt library sidebar | List all templates grouped by category. Search/filter. Create new template. Click to open in editor. Show version count, last modified. |
| 2.1.9 | Frontend: Monaco prompt editor | Install @monaco-editor/react. Configure: syntax highlighting for `{{variables}}` (custom monarch language). Two editor panes: system prompt + user template. Variable declaration panel below editors (name, type, default value, description). |
| 2.1.10 | Frontend: Template variable UI | Auto-detect variables from template text (`{{...}}` pattern). Show variable input form: one field per detected variable. Live preview: rendered prompt shown in read-only panel as you fill in variables. Token count updates live. |
| 2.1.11 | Frontend: Version history panel | List versions with timestamp and notes. Click to load version. Diff button: side-by-side diff view between any two versions (use Monaco's diff editor). "Restore" button to create new version from old one. |
| 2.1.12 | Frontend: Few-shot example manager | Add/remove/reorder few-shot examples. Each example: input field + output field. Drag-and-drop to reorder. Show token count for all examples combined. Import examples from dataset. |
| 2.1.13 | Frontend: Prompt test panel | "Run" button: select model, fill variables, execute. Show response with logprobs heatmap (reuse Playground's logprobs components). "A/B Test" button: configure matrix (versions x models x params). Launch and link to Experiment Tracker. |
| 2.1.14 | Frontend: Prompt chain/pipeline builder | Define ordered sequence of prompts. Each step: prompt template + variable mappings. Output of step N can be mapped as input variable to step N+1. Execute full pipeline, show results at each stage. |

---

#### 2.2 Experiment Tracker

| # | Task | Details |
|---|------|---------|
| 2.2.1 | Define `Project`, `Experiment`, `Run` domain models | Project { Id, Name, Description, CreatedAt }. Experiment { Id, ProjectId, Name, Description, Status (active/archived), CreatedAt }. Run { Id, ExperimentId, Model, Parameters (JSON), PromptVersionId (nullable), DatasetId (nullable), Input, Output, Metrics (JSON: flexible key-value), TokensUsed, Cost, LatencyMs, TTFT_Ms, TokensPerSecond, Perplexity (nullable), LogprobsData (JSON, nullable), Status (pending/running/complete/failed), Error, CreatedAt, Tags[] }. |
| 2.2.2 | Create EF entities & migration | Map to PostgreSQL. Index on ExperimentId + CreatedAt. GIN index on Metrics JSON for querying. GIN index on Tags array. |
| 2.2.3 | Create project CRUD endpoints | `POST /api/v1/projects` — create. `GET /api/v1/projects` — list. `GET /api/v1/projects/{id}` — with experiment summary. `PUT /api/v1/projects/{id}`. `DELETE /api/v1/projects/{id}` (soft delete). |
| 2.2.4 | Create experiment CRUD endpoints | `POST /api/v1/experiments` — create under project. `GET /api/v1/experiments?projectId=X` — list for project. `GET /api/v1/experiments/{id}` — with run summary (count, best metrics). `PUT /api/v1/experiments/{id}`. Archive/unarchive. |
| 2.2.5 | Create run CRUD endpoints | `POST /api/v1/experiments/{id}/runs` — create run (manual or from Playground "Save Run" action). `GET /api/v1/experiments/{id}/runs` — list with sorting/filtering on any metric. `GET /api/v1/experiments/{id}/runs/{runId}` — full run detail with logprobs. `DELETE` — delete run. |
| 2.2.6 | Implement run comparison endpoint | `POST /api/v1/experiments/{id}/compare` — Takes: run_ids[]. Returns: aligned parameter diff, metric comparison table, output side-by-side. Compute: which params changed between runs, metric deltas, statistical significance (if enough runs). |
| 2.2.7 | Implement run search/filter | `GET /api/v1/experiments/{id}/runs?model=X&minF1=0.8&sortBy=perplexity&order=asc`. Support filtering on any metric key, any parameter, tags, date range. Support sorting on any metric. |
| 2.2.8 | Auto-log integration | Middleware/interceptor: any inference call from Playground or Prompt Lab can optionally save as a Run. Capture: full request config, response, logprobs, timing, token counts, computed perplexity. Triggered by "Save Run" button or auto-save toggle. |
| 2.2.9 | Frontend: Project/Experiment navigation | Sidebar or breadcrumb: Projects > Experiment > Runs. Create/edit/archive dialogs. Show run count and best metric per experiment. |
| 2.2.10 | Frontend: Run table | Sortable, filterable table of runs. Columns: run ID, model, key parameters (temp, top_p), key metrics (configurable), tokens, cost, perplexity, latency, date. Checkbox column for selecting runs to compare. Column visibility toggle. |
| 2.2.11 | Frontend: Run comparison view | Select 2+ runs, click "Compare". Side-by-side: parameter diff (highlight changes), output diff (text diff), metric table with delta (green/red for improvement/regression). Charts: bar chart comparing selected metrics across runs. |
| 2.2.12 | Frontend: Metric visualization | Scatter plots: any metric vs any other metric (e.g., F1 vs latency, perplexity vs temperature). Each point is a run, hover for details. Parallel coordinates plot: visualize many params/metrics simultaneously. Time series: metric values over run creation time (track improvement). |
| 2.2.13 | Frontend: Run detail page | Full input/output text. All parameters. All metrics. Logprobs heatmap (reuse component). Perplexity and entropy charts. "Re-run" button (re-execute with same config). "Fork" button (open in Playground with same config). |
| 2.2.14 | Frontend: Experiment-level analytics | Summary stats: best/worst/mean/stddev for each metric. Distribution charts per metric. Correlation matrix between metrics. |

---

#### 2.3 Multi-Pane Playground

| # | Task | Details |
|---|------|---------|
| 2.3.1 | Frontend: Multi-pane layout | Support 1-4 chat panes side-by-side. Each pane: independent model, parameters, conversation. "Add Pane" / "Remove Pane" controls. Responsive layout: 1 pane = full width, 2 = 50/50, 3 = 33/33/33, 4 = 2x2 grid. |
| 2.3.2 | Frontend: Linked input mode | Toggle: "Link Inputs" — when enabled, typing in any pane's input sends the same message to all panes simultaneously. Each pane responds independently with its own model/params. Useful for same-prompt model comparison. |
| 2.3.3 | Frontend: Compare outputs panel | After linked responses complete: show side-by-side output comparison. Logprobs comparison: show heatmaps for each response aligned vertically. Metrics comparison: token counts, latency, perplexity per pane. |
| 2.3.4 | Frontend: Logprobs diff view | For two responses to the same prompt: highlight tokens that appear in both responses vs unique tokens. Show where models agree (both high confidence) vs disagree. Entropy comparison chart. |
| 2.3.5 | Frontend: Sampling comparison mode | Special mode: same model, same prompt, different sampling params per pane. E.g., Pane 1: greedy, Pane 2: temp=0.7 top_p=0.9, Pane 3: temp=1.0 top_k=50. See how sampling strategy affects output and logprobs. |

---

### PHASE 3: Run (Data & Evaluation)
**Goal:** Work with datasets at scale and evaluate systematically.

---

#### 3.1 Dataset Manager

| # | Task | Details |
|---|------|---------|
| 3.1.1 | Define `Dataset`, `DatasetRecord`, `DatasetSplit` models | Dataset { Id, ProjectId, Name, Description, Format (csv/json/jsonl/parquet), Schema (JSON), RecordCount, SizeBytes, Version, CreatedAt }. DatasetRecord { Id, DatasetId, Data (JSONB), SplitLabel (train/test/val/null), OrderIndex }. DatasetSplit { Id, DatasetId, Name, RecordCount, CreatedAt }. |
| 3.1.2 | Create EF entities & migration | JSONB column for record data. GIN index on data for querying. Index on DatasetId + SplitLabel. |
| 3.1.3 | Implement file upload & parsing | `POST /api/v1/datasets/upload` — multipart file upload. Parse CSV, JSON, JSONL, Parquet. Auto-detect schema from first N records (column names, types). Stream-insert records to avoid memory issues on large files. Return: dataset metadata + schema. |
| 3.1.4 | Implement dataset CRUD | Standard CRUD endpoints. `GET /api/v1/datasets/{id}/records` — paginated (page, pageSize), filterable (JSON field queries), sortable. `PUT /api/v1/datasets/{id}/records/{recordId}` — inline edit. |
| 3.1.5 | Implement dataset splitting | `POST /api/v1/datasets/{id}/split` — Takes: train_ratio, test_ratio, val_ratio (must sum to 1.0), strategy (random, stratified by column), seed. Assigns split labels to records. Returns split stats. |
| 3.1.6 | Implement dataset statistics | `GET /api/v1/datasets/{id}/stats` — Compute: column distributions (value counts for categorical, histogram for numeric, token length distributions for text), null counts, duplicate detection, total token counts (using tokenizer for specified model). |
| 3.1.7 | Implement dataset export | `POST /api/v1/datasets/{id}/export` — Takes: format (csv, json, jsonl, alpaca, sharegpt, chatml, openai_jsonl), split (optional), column_mapping. Export in fine-tuning formats with proper structure. |
| 3.1.8 | Implement synthetic data generation | `POST /api/v1/datasets/{id}/generate` — Takes: model, prompt_template (how to generate new examples), num_records, seed_records (optional: use existing records as examples). Background job that generates and appends records. |
| 3.1.9 | Frontend: Dataset list page | Table of datasets with name, format, records, size, version. Upload button (drag-and-drop zone). Click to open dataset browser. |
| 3.1.10 | Frontend: Dataset browser | Paginated data table. Column headers from schema. Search across all text fields. Filter by column values. Inline edit (click cell to edit). Select records for bulk operations. |
| 3.1.11 | Frontend: Schema editor | Visual schema: list columns with type, mapping purpose (input/output/label/metadata). Rename/retype columns. |
| 3.1.12 | Frontend: Statistics panel | Charts for each column: histograms, value distributions, token length distributions. Summary stats. |
| 3.1.13 | Frontend: Split tool | Dialog: set ratios, choose strategy, preview split sizes. Execute. Show resulting split distribution. |
| 3.1.14 | Frontend: Export dialog | Choose format, split, column mapping. Preview first 3 records in target format. Download button. |

---

#### 3.2 Evaluation & Benchmarking Suite

| # | Task | Details |
|---|------|---------|
| 3.2.1 | Define `Evaluation`, `EvaluationResult` models | Evaluation { Id, ProjectId, DatasetId, SplitLabel, Models[], PromptVersionId, ScoringMethods[], Config (JSON), Status, Progress, CreatedAt }. EvaluationResult { Id, EvaluationId, Model, RecordId, Input, ExpectedOutput, ActualOutput, Scores (JSON: method->score), LogprobsData (nullable), Perplexity, LatencyMs }. |
| 3.2.2 | Create EF entities & migration | Index on EvaluationId + Model. JSONB index on Scores for filtering. |
| 3.2.3 | Implement scoring methods | Each as an `IScoringMethod` interface. **ExactMatch**: string equality (with optional normalization). **ROUGE-L**: longest common subsequence (use a C# implementation or Python interop). **BLEU**: n-gram precision. **SemanticSimilarity**: embed both texts, cosine similarity (requires embedding model). **LLM-as-Judge**: send (input, expected, actual) to a judge model with configurable judge prompt, parse 1-10 score. **Perplexity**: from logprobs. **Calibration**: predicted confidence (from logprobs) vs actual correctness. |
| 3.2.4 | Implement evaluation orchestrator | `POST /api/v1/evaluation/start` — Takes: dataset_id, split, models[], prompt_version_id, scoring_methods[], parallelism, max_samples, capture_logprobs. Background job: iterate records, call each model, score, store results. Progress tracking. |
| 3.2.5 | Add Redis for job queue | Add Redis to docker-compose. Implement job queue: `IJobQueue<EvaluationJob>`. Worker pool with configurable parallelism. Job status tracking. Support pause/resume/cancel. |
| 3.2.6 | Implement evaluation results endpoints | `GET /api/v1/evaluation/{id}/results` — aggregate scores per model (mean, median, stddev for each metric). `GET /api/v1/evaluation/{id}/results/records` — per-record results (paginated, filterable). `GET /api/v1/evaluation/{id}/results/export` — CSV/JSON export. |
| 3.2.7 | Implement leaderboard endpoint | `GET /api/v1/evaluation/leaderboard?projectId=X` — Aggregate across evaluations. Rank models by selected metric. Show: rank, model, per-metric averages, total records evaluated, avg cost, avg latency. |
| 3.2.8 | Implement logprobs-based evaluation metrics | Calibration curve: bucket predictions by confidence (from logprobs), compute accuracy within each bucket. Expected Calibration Error (ECE). Overconfidence/underconfidence metrics. Entropy-based uncertainty quantification. Auto-flag high-uncertainty predictions for human review. |
| 3.2.9 | Frontend: Evaluation setup page | Form: select dataset + split, select models (multi-select), select prompt version, choose scoring methods (checkboxes), configure judge prompt, set parallelism + max samples. Cost estimate before launch. "Start Evaluation" button. |
| 3.2.10 | Frontend: Evaluation progress | Progress bar with ETA. Live-updating result preview (first N results as they complete). Cancel/pause buttons. |
| 3.2.11 | Frontend: Results dashboard | Per-model aggregate scores (table). Score distribution charts per model per metric. Per-record results table with filtering (e.g., show only records where model scored < 0.5). Click record to see full input/output/scores. |
| 3.2.12 | Frontend: Leaderboard view | Ranked table. Configurable primary sort metric. Highlight best-in-class per metric. Cost/quality scatter plot (cost on X, quality on Y — Pareto frontier highlighted). |
| 3.2.13 | Frontend: Human evaluation interface | Side-by-side response comparison (2 models). "A is better" / "Tie" / "B is better" buttons. Elo rating calculation from pairwise comparisons. Progress tracker (N/total rated). Skip button. Randomize presentation order (avoid position bias). |
| 3.2.14 | Frontend: Calibration visualization | Calibration curve plot: confidence (X) vs accuracy (Y). Diagonal = perfect calibration. Show ECE score. Histogram of confidence values. Uncertainty-flagged records list. |

---

#### 3.3 Batch Inference Engine

| # | Task | Details |
|---|------|---------|
| 3.3.1 | Define `BatchJob` and `BatchResult` models | BatchJob { Id, DatasetId, SplitLabel, Model, PromptVersionId, Parameters (JSON), Concurrency, MaxRetries, CaptureLogprobs, Status (queued/running/paused/complete/failed/cancelled), Progress, TotalRecords, CompletedRecords, FailedRecords, TokensUsed, Cost, StartedAt, FinishedAt, OutputPath }. BatchResult { Id, BatchJobId, RecordId, Input, Output, LogprobsData (nullable), Perplexity (nullable), TokensUsed, LatencyMs, Status (success/failed/retry), Error, Attempt }. |
| 3.3.2 | Create EF entities & migration | Index on BatchJobId + Status. |
| 3.3.3 | Implement batch job orchestrator | Background worker that: dequeues jobs from Redis, fetches dataset records, calls model with concurrency limit (SemaphoreSlim), handles retries with exponential backoff, writes results as they complete, updates progress, supports pause/resume (check flag between records), streams results to JSONL file as they complete (don't wait for all). |
| 3.3.4 | Implement batch API endpoints | `POST /api/v1/inference/batch` — create and enqueue job. `GET /api/v1/inference/batch/{id}` — status + progress. `POST /api/v1/inference/batch/{id}/pause`. `POST /api/v1/inference/batch/{id}/resume`. `POST /api/v1/inference/batch/{id}/cancel`. `GET /api/v1/inference/batch/{id}/results` — paginated results. `GET /api/v1/inference/batch/{id}/download` — download JSONL output file. `POST /api/v1/inference/batch/{id}/retry-failed` — re-run only failed records. |
| 3.3.5 | Implement cost estimation | `POST /api/v1/inference/batch/estimate` — Takes: dataset_id, model, prompt_version_id. Sample N records, measure avg tokens, extrapolate. Return: estimated total tokens, estimated cost, estimated time (based on current throughput). |
| 3.3.6 | Frontend: Batch job creation page | Select dataset + split + model + prompt. Configure concurrency, retries, logprobs capture. Cost estimate display (auto-calculated). Launch button. |
| 3.3.7 | Frontend: Job monitoring dashboard | Active jobs with progress bars, speed, ETA, cost-so-far. Completed jobs with summary stats. Preview first N results for running job. Pause/resume/cancel controls. |

---

#### 3.4 Analytics Dashboard

| # | Task | Details |
|---|------|---------|
| 3.4.1 | Implement usage tracking | Middleware that logs every inference request: model, tokens (prompt + completion), latency, TTFT, source module (playground/prompt-lab/evaluation/batch/agent), timestamp. Lightweight: just INSERT to a `UsageLog` table. |
| 3.4.2 | Create `UsageLog` entity & migration | UsageLog { Id, Model, PromptTokens, CompletionTokens, LatencyMs, TTFTMs, TokensPerSecond, SourceModule, ProjectId (nullable), Cost, CreatedAt }. Partitioned by month for performance. |
| 3.4.3 | Implement analytics aggregation endpoints | `GET /api/v1/analytics/usage?period=7d&model=X&project=Y` — token usage by time bucket (hourly/daily). `GET /api/v1/analytics/costs` — cost breakdown by model, project, module. `GET /api/v1/analytics/performance` — latency percentiles (p50/p95/p99), TTFT percentiles, throughput over time. |
| 3.4.4 | Frontend: Analytics dashboard | Summary cards: total tokens, total cost, avg latency, throughput. Time series chart: token usage over time (prompt vs completion). Bar chart: cost by model. Bar chart: cost by project. Latency distribution histogram. P50/P95/P99 trend lines. |

---

### PHASE 4: Sprint (RAG & Structure)
**Goal:** Advanced retrieval and structured generation research.

---

#### 4.1 RAG Workbench

| # | Task | Details |
|---|------|---------|
| 4.1.1 | Enable pgvector extension | Migration to enable `CREATE EXTENSION vector`. Create `RagCollection` entity with embedding config. Create `RagDocument` entity (id, collection_id, filename, content, metadata). Create `RagChunk` entity (id, document_id, content, embedding vector, metadata, order_index). |
| 4.1.2 | Implement document ingestion pipeline | `POST /api/v1/rag/collections/{id}/ingest` — multipart file upload. Parsers for: PDF (PdfPig), TXT, MD, HTML (HtmlAgilityPack), DOCX (DocumentFormat.OpenXml). Extract text content per document. Store raw document. |
| 4.1.3 | Implement chunking strategies | `IChunkingStrategy` interface. Implementations: **Fixed**: split at N characters/tokens with overlap. **Sentence**: split at sentence boundaries (use regex or NLP tokenizer). **Recursive**: try splitting by paragraphs, then sentences, then fixed. **Semantic**: use embedding similarity to find natural break points. Config: chunk_size (tokens), chunk_overlap (tokens). |
| 4.1.4 | Implement embedding generation | Call vLLM or external embedding model. Batch embed chunks (configurable batch size). Store vectors in pgvector column. Support multiple embedding models per collection (for comparison). |
| 4.1.5 | Implement vector search | `POST /api/v1/rag/collections/{id}/query` — Takes: query text, top_k, search_type (vector/bm25/hybrid). Vector search: embed query, cosine similarity against pgvector. BM25: PostgreSQL full-text search with ts_rank. Hybrid: weighted combination of vector + BM25 scores. Return: chunks with scores, source document info. |
| 4.1.6 | Implement re-ranking | Optional re-ranking step after initial retrieval. **Cross-encoder**: use a model to score (query, chunk) pairs. **LLM-based**: ask LLM to rank retrieved chunks by relevance. Config: reranker type, top-N after reranking. |
| 4.1.7 | Implement full RAG pipeline | `POST /api/v1/rag/collections/{id}/rag` — Takes: query, model, prompt_template (with {{context}} and {{query}} variables), retrieval config. Steps: retrieve chunks -> format context -> render prompt -> call model -> return response with source attribution. Capture logprobs for confidence analysis. |
| 4.1.8 | Implement RAG evaluation metrics | Faithfulness: are claims in response supported by retrieved context? (LLM-as-judge). Relevance: are retrieved chunks relevant to query? (semantic similarity). Recall: does retrieved context contain the answer? (compare to known ground truth if available). |
| 4.1.9 | Frontend: Collection management | Create/delete collections. Configure chunking and embedding parameters. Upload documents (drag-and-drop, multi-file). Ingestion progress. |
| 4.1.10 | Frontend: Chunking preview | Before ingesting: show sample document chunked with current settings. Side-by-side: compare chunking strategies on same document. Show chunk sizes, overlap regions. |
| 4.1.11 | Frontend: Retrieval test panel | Query input. Retrieved chunks displayed with scores, source document, highlighted matching text. Toggle between vector/BM25/hybrid results. |
| 4.1.12 | Frontend: RAG pipeline test | Query input. Full pipeline execution: show retrieved chunks + generated response + source citations. Logprobs heatmap on generated response (low confidence tokens may indicate hallucination). |

---

#### 4.2 Structured Output Toolkit

| # | Task | Details |
|---|------|---------|
| 4.2.1 | Implement JSON schema storage | `JsonSchema` entity { Id, ProjectId, Name, Schema (JSON), Version, CreatedAt }. CRUD endpoints. |
| 4.2.2 | Implement guided decoding endpoint | `POST /api/v1/inference/structured` — Takes: model, messages, json_schema. Calls vLLM with `guided_json` parameter for constrained decoding. Returns: parsed JSON + validation status + logprobs per JSON value. |
| 4.2.3 | Implement output validation | Validate response against JSON schema. Report: field-level validation (pass/fail per field), type errors, missing required fields, extra fields. |
| 4.2.4 | Frontend: JSON Schema editor | Monaco editor for JSON schema. Visual builder alternative: add fields with type dropdowns, nested objects, arrays. Live preview of schema. |
| 4.2.5 | Frontend: Structured output test panel | Input prompt area. Execute with guided decoding. Output: formatted JSON with field-level confidence annotations (from logprobs on value tokens). Validation results sidebar. |
| 4.2.6 | Frontend: Batch schema testing | Select dataset, run structured extraction on all records. Show: success rate, common validation failures, confidence distribution per field. |

---

### PHASE 5: Fly (Agents & Integration)
**Goal:** Agent workflows and deep notebook integration.

---

#### 5.1 Agent Builder

| # | Task | Details |
|---|------|---------|
| 5.1.1 | Define agent workflow model | AgentWorkflow { Id, ProjectId, Name, Description, Config (YAML: pattern, model, tools, system_prompt, max_steps, token_budget, guardrails), Version, CreatedAt }. AgentRun { Id, WorkflowId, Status, Steps[] (JSON), TotalTokens, TotalCost, TotalLatency, CreatedAt }. |
| 5.1.2 | Implement tool registry | `IAgentTool` interface { Name, Description, ParameterSchema (JSON), Execute(params) -> result }. Built-in tools: WebSearch (via API), Calculator (expression eval), CodeExecution (sandboxed), RAGQuery (calls RAG workbench), APICall (configurable HTTP), FileRead. |
| 5.1.3 | Implement ReAct agent pattern | Thought-Action-Observation loop. System prompt template for ReAct. Parse LLM output for action selection. Execute tool. Feed observation back. Stop conditions: model says "Final Answer", max steps, token budget. Log every step with logprobs (confidence in tool selection). |
| 5.1.4 | Implement agent execution endpoints | `POST /api/v1/agents/{id}/run` — Start agent execution. `GET /api/v1/agents/{id}/runs/{runId}` — Get run with full trace. SSE endpoint for live step streaming. |
| 5.1.5 | Frontend: Agent config editor | YAML/JSON editor for agent workflow config. Tool selection panel. Test input area. |
| 5.1.6 | Frontend: Execution trace viewer | Step-by-step display: Thought (with logprobs), Action (tool + params + confidence), Observation (tool result). Expandable/collapsible steps. Token/cost counter per step and cumulative. |

---

#### 5.2 Research Notebook (JupyterLite)

| # | Task | Details |
|---|------|---------|
| 5.2.1 | Embed JupyterLite | Add JupyterLite as a static asset or iframe embed. Configure Python kernel with pre-installed packages. |
| 5.2.2 | Build `workbench` Python package | Python helper that calls platform API: `workbench.chat(model, prompt)`, `workbench.get_experiment(id)`, `workbench.get_dataset(id)`, `workbench.logprobs(model, prompt)`, `workbench.plot_token_heatmap(logprobs)`. Package as a JupyterLite extension or pre-installed package. |
| 5.2.3 | Implement notebook storage endpoints | `GET/POST/PUT /api/v1/notebooks` — Store .ipynb files. Version history. Download. |
| 5.2.4 | Frontend: Notebook list & launcher | List notebooks. "New notebook" button. "Open in notebook" action from experiments, datasets, evaluations. |

---

#### 5.3 Fine-Tuning Support

| # | Task | Details |
|---|------|---------|
| 5.3.1 | Implement fine-tuning format export | Extend dataset export with format-specific validation. Alpaca: instruction/input/output. ShareGPT: conversations[]. ChatML: with role tokens. OpenAI JSONL: messages[]. Validate: all required fields present, no empty values, token lengths within limits. |
| 5.3.2 | Implement adapter registration | Register a LoRA adapter path with a vLLM instance. vLLM supports `--enable-lora` and loading adapters dynamically. Endpoint to add/remove adapters. |
| 5.3.3 | Frontend: Export wizard | Step-by-step: select dataset -> select split -> select format -> column mapping -> validation -> export. |
| 5.3.4 | Frontend: Fine-tuned model comparison | "Compare base vs fine-tuned" action. Opens Playground in 2-pane mode: base model (pane 1) vs adapter-loaded model (pane 2). Linked input mode on by default. |

---

## Cross-Cutting Concerns (Apply Throughout All Phases)

| Concern | Implementation |
|---------|---------------|
| **Error handling** | Global exception handler middleware. Structured error responses { code, message, details }. Frontend toast notifications for errors. |
| **Logging** | Serilog with structured logging. Log to console + file. Request/response logging (excluding large bodies). |
| **Settings** | Model cost rates ($/token per model) stored in DB, configurable via settings page. vLLM connection config. Default parameters. |
| **Testing** | Unit tests for Core logic (scoring methods, template rendering, logprobs computation). Integration tests for API endpoints. Frontend: component tests for logprobs visualizations. |
| **Performance** | Pagination on all list endpoints. Streaming for large responses. Avoid loading full logprobs data in list views (only on detail). DB query optimization with proper indexes. |
| **Frontend state** | Zustand for global state (selected model, active project). React Query (TanStack Query) for server state + caching. |

---

## Milestone Summary

| Milestone | You Can... | Key Research Capability |
|-----------|-----------|------------------------|
| Phase 1 complete | Chat with models, see token probabilities, step through generation, explore tokenization | Logprobs analysis, next-token prediction, tokenizer comparison |
| Phase 2 complete | Version prompts, A/B test them, track experiments, compare models side-by-side | Systematic prompt development, experiment reproducibility, logprobs diff across models |
| Phase 3 complete | Upload datasets, run batch evaluations, see leaderboards, track costs | Calibration curves, human eval, cost/quality Pareto analysis |
| Phase 4 complete | Build RAG pipelines, test retrieval, use guided JSON decoding | Hallucination detection via logprobs, retrieval quality metrics |
| Phase 5 complete | Build agents, use notebooks for analysis, evaluate fine-tuned models | Full research loop: build -> test -> evaluate -> analyze -> publish |
