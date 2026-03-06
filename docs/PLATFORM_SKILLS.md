# AI Research Workbench — Skills Registry

## Overview

Skills are the atomic capabilities that agents, pipelines, and the UI can invoke. Every skill is:

1. **Self-describing** — Has a name, description, parameter schema, and return schema (so LLMs can use them as tools)
2. **Logged** — Every invocation is traced with inputs, outputs, tokens consumed, latency, and cost
3. **Composable** — Skills can call other skills. A RAG pipeline skill composes retrieval + generation + scoring.
4. **Rate-aware** — Skills that call LLMs respect concurrency limits and token budgets

Skills are grouped into categories by what they operate on.

---

## Skill Interface

Every skill implements this interface. Skills follow the project's Result pattern (see `ARCHITECTURE.md`) — they never throw exceptions for expected failures.

Skills are thin wrappers over feature application-layer handlers. They live in `AiResearch.Features/Skills/Implementations/` and delegate to the same use case handlers the API endpoints call. This avoids duplicating logic.

```csharp
/// <summary>
/// Defines an executable skill that agents, pipelines, and the UI can invoke.
/// Each skill is self-describing (name, schema) so LLMs can use them as tools,
/// and observable (every invocation is traced with metrics).
/// </summary>
public interface ISkill
{
    /// <summary>Unique skill identifier used in agent configs and API calls.</summary>
    string Name { get; }

    /// <summary>Human and LLM readable description of what this skill does.</summary>
    string Description { get; }

    /// <summary>Category for grouping in the UI (e.g., "inference", "datasets").</summary>
    string Category { get; }

    /// <summary>JSON Schema defining valid input parameters.</summary>
    JsonSchema ParameterSchema { get; }

    /// <summary>JSON Schema defining the return value structure.</summary>
    JsonSchema ReturnSchema { get; }

    /// <summary>If true, agent execution pauses for human confirmation before invoking.</summary>
    bool RequiresApproval { get; }

    /// <summary>
    /// Execute the skill with the given parameters.
    /// Returns <see cref="SkillResult"/> which wraps <see cref="Result{T}"/> with
    /// additional metrics (tokens consumed, cost, latency).
    /// </summary>
    /// <param name="parameters">JSON parameters matching <see cref="ParameterSchema"/>.</param>
    /// <param name="context">Execution context with budget, tracing, and cancellation.</param>
    /// <returns>A skill result containing the output data and execution metrics.</returns>
    Task<SkillResult> ExecuteAsync(JsonElement parameters, SkillContext context);
}

/// <summary>
/// Execution context passed to every skill invocation. Provides budget tracking,
/// trace logging, and cancellation support for agent guardrails.
/// </summary>
/// <param name="ProjectId">Optional project scope for data access.</param>
/// <param name="UserId">Optional user scope (for future multi-user).</param>
/// <param name="CancellationToken">Cancellation token — agents cancel when guardrails trigger.</param>
/// <param name="Budget">Remaining token/cost budget from the calling agent's guardrails.</param>
/// <param name="TraceLogger">Logger for agent execution trace recording.</param>
public record SkillContext(
    string? ProjectId,
    string? UserId,
    CancellationToken CancellationToken,
    TokenBudget? Budget,
    ISkillTraceLogger TraceLogger
);

/// <summary>
/// The result of a skill execution. Wraps the Result pattern with execution metrics.
/// On success, Data contains the JSON output. On failure, the Error field describes what went wrong.
/// Metrics (tokens, cost, latency) are always populated regardless of success/failure.
/// </summary>
public sealed record SkillResult
{
    /// <summary>Whether the skill executed successfully.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The JSON output data on success. Matches the skill's ReturnSchema.</summary>
    public JsonElement? Data { get; init; }

    /// <summary>The error details on failure. Follows the standard Error type from Result pattern.</summary>
    public Error? Error { get; init; }

    /// <summary>Total tokens consumed by this skill invocation (0 if no inference was called).</summary>
    public int TokensConsumed { get; init; }

    /// <summary>Estimated dollar cost of this invocation.</summary>
    public decimal Cost { get; init; }

    /// <summary>Wall-clock execution time.</summary>
    public TimeSpan Latency { get; init; }

    public static SkillResult Success(JsonElement data, int tokens, decimal cost, TimeSpan latency)
        => new() { IsSuccess = true, Data = data, TokensConsumed = tokens, Cost = cost, Latency = latency };

    public static SkillResult Failure(Error error, int tokens = 0, decimal cost = 0, TimeSpan latency = default)
        => new() { IsSuccess = false, Error = error, TokensConsumed = tokens, Cost = cost, Latency = latency };
}
```

### How Skills Call Feature Handlers

Skills don't reimplement logic. They deserialize parameters, call the feature's application-layer handler, and wrap the result:

```csharp
/// <summary>
/// Skill wrapper for the Experiments feature's CompareRuns handler.
/// Deserializes agent-provided JSON parameters and delegates to the typed handler.
/// </summary>
public sealed class CompareRunsSkill : ISkill
{
    private readonly CompareRunsHandler _handler;

    public string Name => "compare_runs";
    public string Description => "Compare 2 or more experiment runs. Shows parameter differences, metric deltas, and output differences.";
    public string Category => "experiments";
    // ...

    /// <summary>
    /// Compares the specified runs by delegating to <see cref="CompareRunsHandler"/>.
    /// </summary>
    public async Task<SkillResult> ExecuteAsync(JsonElement parameters, SkillContext context)
    {
        var runIds = parameters.GetProperty("run_ids").Deserialize<Guid[]>();
        var query = new CompareRunsQuery(runIds!);

        var stopwatch = Stopwatch.StartNew();
        var result = await _handler.HandleAsync(query, context.CancellationToken);
        stopwatch.Stop();

        return result.Match(
            onSuccess: data => SkillResult.Success(
                JsonSerializer.SerializeToElement(data), tokens: 0, cost: 0, stopwatch.Elapsed),
            onFailure: error => SkillResult.Failure(error, latency: stopwatch.Elapsed));
    }
}
```

### Inference Provider Awareness

Skills that call AI inference use `IInferenceProvider` (not vLLM directly). The provider is resolved via `IInferenceProviderFactory` based on the model/instance specified in the parameters. Skills check `provider.Capabilities` before using optional features like logprobs or guided decoding.

For LLM tool-use, each skill auto-generates an OpenAI-compatible function definition:

```json
{
  "type": "function",
  "function": {
    "name": "search_experiments",
    "description": "Search for experiment runs matching criteria...",
    "parameters": { ... }
  }
}
```

---

## Skill Categories

---

### 1. INFERENCE SKILLS

Core model interaction capabilities.

#### `run_inference`
Send a prompt to a model and get a response.

```yaml
name: run_inference
description: >
  Send a chat completion request to a model. Returns the response text,
  token usage, latency, and optionally logprobs data.
category: inference
parameters:
  model: string              # Model ID or instance endpoint
  messages:                  # Chat messages
    - role: enum             # system | user | assistant
      content: string
  temperature: float?        # 0.0 - 2.0 (default: model default)
  top_p: float?              # 0.0 - 1.0
  top_k: int?                # 0 - 100
  max_tokens: int?           # Max completion tokens
  stop: string[]?            # Stop sequences
  frequency_penalty: float?  # -2.0 to 2.0
  presence_penalty: float?   # -2.0 to 2.0
  logprobs: bool?            # Return logprobs (default: false)
  top_logprobs: int?         # Number of top logprobs per token (1-20)
  guided_json: json_schema?  # JSON schema for constrained decoding
  stream: bool?              # Stream response (default: false)
returns:
  content: string            # Response text
  finish_reason: string      # stop | length | content_filter
  usage:
    prompt_tokens: int
    completion_tokens: int
    total_tokens: int
  latency_ms: int
  ttft_ms: int               # Time to first token
  tokens_per_second: float
  logprobs_data: object?     # Per-token logprobs (if requested)
  perplexity: float?         # Computed from logprobs (if requested)
```

#### `run_inference_batch`
Run the same prompt template against multiple inputs efficiently.

```yaml
name: run_inference_batch
description: >
  Run inference on multiple inputs in parallel. Useful for quick A/B tests
  or gathering multiple data points. Not a full batch job — limited to ~100 inputs.
parameters:
  model: string
  prompt_template: string    # Template with {{input}} variable
  system_prompt: string?
  inputs: string[]           # List of input values
  parameters: object?        # Temperature, top_p, etc.
  logprobs: bool?
  concurrency: int?          # Parallel requests (default: 4)
returns:
  results:
    - input: string
      output: string
      tokens: int
      latency_ms: int
      perplexity: float?
      logprobs_data: object?
  aggregate:
    total_tokens: int
    total_cost: float
    avg_latency_ms: int
    avg_perplexity: float?
```

#### `run_inference_stream`
Stream a response token-by-token. Used by the Playground and agents that need real-time output.

```yaml
name: run_inference_stream
description: >
  Stream a chat completion response. Returns an async stream of tokens.
  Each chunk includes the token text and logprobs (if enabled).
parameters:
  # Same as run_inference
returns:
  stream:
    - token: string
      logprob: float?
      top_logprobs: object[]?
  final:
    # Same as run_inference returns (aggregated)
```

---

### 2. LOGPROBS & TOKEN ANALYSIS SKILLS

Deep model behavior analysis.

#### `get_logprobs`
Get detailed logprobs for a specific inference request or saved run.

```yaml
name: get_logprobs
description: >
  Get per-token log probabilities for a model response. If given a run_id,
  returns stored logprobs. If given a prompt, runs inference with logprobs enabled.
parameters:
  run_id: string?            # Get logprobs from a saved run
  # OR
  model: string?             # Run new inference
  messages: object[]?
  top_logprobs: int?         # 1-20 (default: 5)
returns:
  tokens:
    - token: string          # The token text
      logprob: float         # Log probability of this token
      probability: float     # exp(logprob) — actual probability
      rank: int              # Rank among all tokens (1 = most likely)
      entropy: float         # Shannon entropy at this position
      top_alternatives:
        - token: string
          logprob: float
          probability: float
  summary:
    perplexity: float        # exp(mean(-logprob))
    mean_entropy: float
    max_entropy_token: string
    min_confidence_token: string
    surprise_tokens: string[] # Tokens below confidence threshold
    total_tokens: int
```

#### `predict_next_token`
Predict the most likely next tokens at a given position.

```yaml
name: predict_next_token
description: >
  Given a prompt (or partial generation), predict the top-N most likely
  next tokens with their probabilities. Core skill for the Token Explorer.
parameters:
  model: string
  messages: object[]         # Context up to prediction point
  top_n: int?                # How many alternatives (default: 20, max: 100)
  temperature: float?        # Affects probability distribution
  include_sampling_info: bool? # Show which tokens pass top-p/top-k filters
  top_p: float?              # For sampling visualization
  top_k: int?                # For sampling visualization
returns:
  predictions:
    - token: string
      token_id: int
      logprob: float
      probability: float
      cumulative_probability: float  # Running sum (for top-p visualization)
      within_top_p: bool             # Would this token be sampled with given top_p?
      within_top_k: bool             # Would this token be sampled with given top_k?
  distribution:
    entropy: float                   # Shannon entropy of full distribution
    effective_vocab_size: int        # Tokens with probability > 0.001
    top_1_probability: float         # How dominant is the top choice?
    top_5_cumulative: float          # How much probability mass in top 5?
    temperature_adjusted: bool       # Was temperature applied?
```

#### `explore_branch`
Force a specific token and generate a continuation — counterfactual exploration.

```yaml
name: explore_branch
description: >
  Force a specific token at the current position and generate N more tokens.
  Used for counterfactual analysis: "what if the model had said X instead of Y?"
parameters:
  model: string
  messages: object[]         # Context up to branch point
  forced_token: string       # Token to force
  continue_tokens: int?      # How many tokens to generate after (default: 50)
  temperature: float?
  logprobs: bool?            # Capture logprobs for continuation (default: true)
returns:
  forced_token:
    token: string
    original_probability: float  # How likely was this token originally?
    rank: int                    # What rank was it?
  continuation:
    text: string               # Generated text after forced token
    tokens: object[]           # Per-token data with logprobs
    perplexity: float          # Perplexity of the continuation
  comparison:                  # If original generation is available
    divergence_point: int      # Token index where outputs first differ
    original_text: string
    branch_text: string
```

#### `analyze_confidence`
Aggregate confidence analysis across a set of responses.

```yaml
name: analyze_confidence
description: >
  Analyze model confidence patterns across multiple responses. Useful for
  understanding systematic overconfidence, uncertainty patterns, and calibration.
parameters:
  run_ids: string[]?         # Analyze saved runs
  # OR
  model: string?             # Run new analysis
  messages_batch: object[]?  # Multiple prompts
returns:
  overall:
    mean_perplexity: float
    median_perplexity: float
    perplexity_std: float
    mean_entropy: float
  per_response:
    - run_id: string
      perplexity: float
      surprise_count: int     # Tokens below threshold
      high_entropy_regions: object[]  # Spans of uncertain tokens
  patterns:
    confidence_by_position: float[]  # Avg confidence at each token position
    confidence_trend: string         # "increasing" | "decreasing" | "stable"
    common_low_confidence_tokens: string[]  # Tokens that are often uncertain
```

---

### 3. TOKENIZER SKILLS

Understanding how text becomes tokens.

#### `tokenize_text`
Tokenize text using a model's tokenizer.

```yaml
name: tokenize_text
description: >
  Break text into tokens using a specific model's tokenizer.
  Shows token boundaries, IDs, and byte representations.
parameters:
  model: string
  text: string
returns:
  tokens:
    - text: string           # Token as rendered text
      id: int                # Token ID in vocabulary
      bytes: string          # Hex byte representation
      char_start: int        # Character offset start in original text
      char_end: int          # Character offset end
      is_special: bool       # BOS, EOS, PAD, etc.
  total_tokens: int
  special_tokens:
    bos: string?
    eos: string?
    pad: string?
  model_info:
    vocab_size: int
    tokenizer_type: string   # BPE, SentencePiece, etc.
```

#### `compare_tokenizers`
Compare how different models tokenize the same text.

```yaml
name: compare_tokenizers
description: >
  Tokenize the same text with multiple models and highlight differences.
  Useful for understanding tokenization impact on prompt design.
parameters:
  models: string[]           # 2+ model IDs
  text: string
returns:
  comparisons:
    - model: string
      token_count: int
      tokens: object[]       # Same as tokenize_text
  differences:
    total_token_counts: object  # model -> count
    divergence_points: int[]   # Character positions where tokenization differs
    unique_splits: object[]    # Tokens that only appear in one model's tokenization
  cost_impact:
    - model: string
      estimated_cost: float   # Based on per-token pricing
```

#### `get_token_counts`
Quick token count for text or dataset fields. Lightweight — doesn't return full tokenization.

```yaml
name: get_token_counts
description: >
  Count tokens for one or more texts. Fast batch operation for
  dataset analysis and cost estimation.
parameters:
  model: string
  texts: string[]            # Or dataset_id + column for batch
  dataset_id: string?
  column: string?
returns:
  counts: int[]              # Per-text token counts
  total: int
  min: int
  max: int
  mean: float
  median: float
  p95: int                   # 95th percentile
  histogram: object          # Bucket distribution
```

---

### 4. EXPERIMENT & RUN SKILLS

Query and manipulate experiment data.

#### `search_experiments`
Find experiments and runs matching criteria.

```yaml
name: search_experiments
description: >
  Search for experiment runs by model, metrics, parameters, tags, or date range.
  Returns matching runs sorted by the specified metric.
parameters:
  project_id: string?
  experiment_id: string?
  model: string?
  metric: string?            # Metric name to filter/sort by
  min_value: float?          # Minimum metric value
  max_value: float?
  sort_by: string?           # Metric name or "created_at", "cost", "latency"
  sort_order: enum?          # asc | desc
  tags: string[]?
  date_from: datetime?
  date_to: datetime?
  limit: int?                # Max results (default: 20)
returns:
  runs:
    - id: string
      experiment_name: string
      model: string
      parameters: object
      metrics: object
      tokens_used: int
      cost: float
      latency_ms: int
      perplexity: float?
      created_at: datetime
      tags: string[]
  total_matching: int
```

#### `get_run_details`
Get full details of a specific run including input, output, and logprobs.

```yaml
name: get_run_details
description: >
  Get complete details of an experiment run: input, output, all parameters,
  all metrics, and optionally the full logprobs data.
parameters:
  run_id: string
  include_logprobs: bool?    # Include full logprobs data (default: false, large)
returns:
  id: string
  experiment: object
  model: string
  parameters: object
  prompt_version: object?
  input: string
  output: string
  metrics: object
  tokens_used: int
  cost: float
  latency_ms: int
  ttft_ms: int
  perplexity: float?
  logprobs_data: object?     # Only if include_logprobs == true
  created_at: datetime
```

#### `compare_runs`
Side-by-side comparison of multiple runs.

```yaml
name: compare_runs
description: >
  Compare 2 or more experiment runs. Shows parameter differences,
  metric deltas, and output differences.
parameters:
  run_ids: string[]          # 2+ run IDs
  diff_outputs: bool?        # Include text diff of outputs (default: true)
returns:
  parameter_diff:
    changed: object          # Parameters that differ between runs
    common: object           # Parameters shared by all runs
  metric_comparison:
    - metric: string
      values: object         # run_id -> value
      best_run: string
      delta: float           # Max - min
  output_diff: object?       # Text diff if requested
  cost_comparison:
    - run_id: string
      tokens: int
      cost: float
      latency_ms: int
```

#### `save_run`
Save an inference result as an experiment run.

```yaml
name: save_run
description: >
  Save an inference result as a tracked experiment run. Captures full
  configuration for reproducibility.
parameters:
  experiment_id: string
  model: string
  parameters: object
  prompt_version_id: string?
  input: string
  output: string
  metrics: object?           # Custom metrics (key -> value)
  logprobs_data: object?
  tags: string[]?
returns:
  run_id: string
  experiment_id: string
```

#### `calculate_statistics`
Compute statistical summaries over run metrics.

```yaml
name: calculate_statistics
description: >
  Compute statistical summaries for experiment metrics. Includes mean, stddev,
  confidence intervals, and optional significance testing between groups.
parameters:
  run_ids: string[]?         # Specific runs, or use filters below
  experiment_id: string?
  metric: string             # Which metric to analyze
  group_by: string?          # Group by: "model", "temperature", or any parameter
  significance_test: bool?   # Run t-test between groups (default: false)
returns:
  overall:
    count: int
    mean: float
    median: float
    stddev: float
    min: float
    max: float
    ci_95: [float, float]    # 95% confidence interval
  groups:                    # If group_by specified
    - group: string
      count: int
      mean: float
      stddev: float
      ci_95: [float, float]
  significance:              # If significance_test == true
    - group_a: string
      group_b: string
      t_statistic: float
      p_value: float
      significant: bool      # p < 0.05
      effect_size: float     # Cohen's d
```

---

### 5. PROMPT SKILLS

Work with prompt templates and versions.

#### `search_prompts`
Find prompt templates by name, category, or tags.

```yaml
name: search_prompts
description: >
  Search the prompt library for templates matching criteria.
parameters:
  query: string?             # Free-text search
  category: string?
  tags: string[]?
  project_id: string?
returns:
  templates:
    - id: string
      name: string
      category: string
      tags: string[]
      latest_version: int
      updated_at: datetime
```

#### `get_prompt_version`
Get a specific prompt version with all details.

```yaml
name: get_prompt_version
description: >
  Get a specific version of a prompt template including system prompt,
  user template, variables, and few-shot examples.
parameters:
  template_id: string
  version: int?              # Default: latest
returns:
  id: string
  template_id: string
  version: int
  system_prompt: string
  user_template: string
  variables:
    - name: string
      type: string
      default: string?
      description: string?
  few_shot_examples:
    - input: string
      output: string
  created_at: datetime
  notes: string?
```

#### `create_prompt_version`
Create a new version of a prompt template.

```yaml
name: create_prompt_version
description: >
  Create a new version of an existing prompt template. Use this to
  save prompt improvements discovered during optimization.
parameters:
  template_id: string
  system_prompt: string
  user_template: string
  variables: object[]?
  few_shot_examples: object[]?
  notes: string?
returns:
  id: string
  version: int
requires_approval: false     # Creating a version is non-destructive
```

#### `render_prompt`
Render a prompt template with given variable values.

```yaml
name: render_prompt
description: >
  Render a prompt template by filling in variable values.
  Returns the final prompt text with token count.
parameters:
  template_id: string
  version: int?
  variables: object          # Variable name -> value
returns:
  system_prompt: string      # Rendered system prompt
  user_message: string       # Rendered user message
  token_count: int           # Total tokens for the rendered prompt
  variables_used: string[]
  variables_missing: string[] # Variables not provided (using defaults or empty)
```

---

### 6. DATASET SKILLS

Data access and manipulation.

#### `search_datasets`
List and search datasets.

```yaml
name: search_datasets
description: >
  List available datasets, optionally filtered by project or search term.
parameters:
  project_id: string?
  query: string?             # Search name/description
returns:
  datasets:
    - id: string
      name: string
      format: string
      record_count: int
      size_bytes: int
      version: int
      splits: string[]       # Available split labels
```

#### `get_dataset_stats`
Get statistical summary of a dataset.

```yaml
name: get_dataset_stats
description: >
  Get statistics for a dataset: column distributions, token lengths,
  value counts, null rates, and duplicate detection.
parameters:
  dataset_id: string
  split: string?             # Optional: stats for specific split
  columns: string[]?         # Optional: only these columns
  model: string?             # For token count stats
returns:
  record_count: int
  columns:
    - name: string
      type: string
      null_count: int
      unique_count: int
      value_distribution: object?  # For categorical columns
      numeric_stats: object?       # For numeric columns (min, max, mean, etc.)
      token_stats: object?         # If model specified (min, max, mean, p95)
  duplicate_count: int
  split_distribution: object?      # Label -> count
```

#### `search_dataset_records`
Search and filter dataset records.

```yaml
name: search_dataset_records
description: >
  Search within a dataset for records matching criteria.
  Useful for finding specific examples, edge cases, or patterns.
parameters:
  dataset_id: string
  split: string?
  filters: object?           # Column -> filter (e.g., {"category": "billing", "difficulty": {">": 3}})
  search: string?            # Full-text search across text columns
  sort_by: string?
  limit: int?                # Default: 20
  offset: int?
returns:
  records:
    - id: string
      data: object           # The record's fields
      split_label: string?
  total_matching: int
```

#### `update_dataset_record`
Modify a dataset record (requires approval in agent context).

```yaml
name: update_dataset_record
description: >
  Update a record in a dataset. Used for data correction, label fixing, etc.
  CAUTION: Modifies data. Requires human approval when called by agents.
parameters:
  dataset_id: string
  record_id: string
  updates: object            # Field -> new value
returns:
  record_id: string
  updated_fields: string[]
  previous_values: object
requires_approval: true
```

#### `generate_synthetic_data`
Use a model to generate new dataset records.

```yaml
name: generate_synthetic_data
description: >
  Generate synthetic dataset records using an LLM. Provide seed examples
  and generation instructions. Useful for augmenting small datasets or
  covering edge cases.
parameters:
  dataset_id: string
  model: string
  generation_prompt: string  # Instructions for generation
  seed_records: int?         # Number of existing records to use as examples (default: 3)
  num_to_generate: int       # How many new records
  temperature: float?        # Default: 0.8
returns:
  generated:
    - data: object           # The generated record
      quality_score: float?  # Self-assessed quality (if model provides)
  total_generated: int
  total_tokens: int
  cost: float
requires_approval: true      # Modifies dataset
```

---

### 7. EVALUATION SKILLS

Scoring and benchmarking.

#### `run_evaluation_sample`
Run evaluation on a small sample — fast iteration for prompt optimization.

```yaml
name: run_evaluation_sample
description: >
  Run evaluation on a small random sample from a dataset. Much faster than
  full evaluation. Useful for quick feedback during prompt optimization.
parameters:
  dataset_id: string
  split: string?             # Default: test
  sample_size: int?          # Default: 20
  model: string
  prompt_version_id: string
  scoring_methods: string[]  # exact_match, rouge_l, bleu, llm_judge, semantic, perplexity
  judge_model: string?       # For llm_judge scoring
  judge_prompt: string?
  logprobs: bool?            # Default: true
returns:
  scores:
    - method: string
      mean: float
      stddev: float
      min: float
      max: float
  per_record:
    - record_id: string
      input: string
      expected: string
      actual: string
      scores: object
      perplexity: float?
  failures:                  # Records where score was below threshold
    - record_id: string
      input: string
      expected: string
      actual: string
      scores: object
      failure_reason: string? # Heuristic guess at why it failed
  total_tokens: int
  cost: float
```

#### `calculate_metrics`
Compute scoring metrics between expected and actual outputs.

```yaml
name: calculate_metrics
description: >
  Score an actual output against an expected output using various metrics.
  Does not call a model — pure computation (except for llm_judge and semantic).
parameters:
  expected: string
  actual: string
  methods: string[]          # exact_match, rouge_l, rouge_1, rouge_2, bleu, f1_token
  normalize: bool?           # Lowercase, strip whitespace (default: true)
returns:
  scores:
    - method: string
      score: float           # 0.0 - 1.0
```

#### `get_evaluation_results`
Fetch results from a completed evaluation.

```yaml
name: get_evaluation_results
description: >
  Get aggregate and per-record results from a completed evaluation run.
parameters:
  evaluation_id: string
  model: string?             # Filter to specific model
  min_score: float?          # Filter to records above/below threshold
  max_score: float?
  sort_by: string?           # Score method name
  limit: int?
returns:
  aggregate:
    - model: string
      scores: object         # method -> {mean, stddev, min, max}
      total_records: int
      avg_perplexity: float?
      avg_latency_ms: int
      total_cost: float
  records: object[]?         # Per-record details if requested
```

---

### 8. RAG SKILLS

Retrieval-augmented generation operations.

#### `query_rag_collection`
Search a RAG collection for relevant chunks.

```yaml
name: query_rag_collection
description: >
  Query a RAG collection using vector search, BM25, or hybrid search.
  Returns ranked chunks with relevance scores.
parameters:
  collection_id: string
  query: string
  top_k: int?               # Default: 5
  search_type: enum?         # vector | bm25 | hybrid (default: hybrid)
  rerank: bool?              # Apply reranker (default: false)
  rerank_top_n: int?         # After reranking, keep top N (default: 3)
  filter: object?            # Metadata filter (e.g., {"source": "vllm.md"})
returns:
  chunks:
    - id: string
      content: string
      score: float
      document: string       # Source document name
      metadata: object
      chunk_index: int       # Position within document
  query_embedding_time_ms: int
  search_time_ms: int
```

#### `run_rag_pipeline`
Execute a full RAG pipeline: retrieve + generate + (optionally) evaluate.

```yaml
name: run_rag_pipeline
description: >
  Full RAG pipeline: embed query, retrieve chunks, format context,
  generate response with citations, optionally evaluate faithfulness.
parameters:
  collection_id: string
  query: string
  model: string
  prompt_template: string?   # Template with {{context}} and {{query}} (default provided)
  retrieval_config:
    top_k: int?
    search_type: enum?
    rerank: bool?
  generation_config:
    temperature: float?
    max_tokens: int?
    logprobs: bool?          # Default: true (for hallucination detection)
  evaluate: bool?            # Run faithfulness check (default: false)
returns:
  response:
    text: string
    citations: object[]      # Source attribution
    logprobs_data: object?
    perplexity: float?
    confidence: float?       # Average token probability
  retrieval:
    chunks_used: object[]
    search_scores: float[]
  evaluation:                # If evaluate == true
    faithfulness_score: float
    unsupported_claims: string[]  # Claims not backed by retrieved context
  total_tokens: int
  cost: float
```

#### `get_chunk_details`
Get full details of a specific chunk.

```yaml
name: get_chunk_details
description: >
  Get the full content and metadata of a specific chunk, including
  its position in the source document and neighboring chunks.
parameters:
  chunk_id: string
  include_neighbors: bool?   # Include previous and next chunks (default: false)
returns:
  id: string
  content: string
  document: string
  metadata: object
  chunk_index: int
  token_count: int
  neighbors:
    previous: object?
    next: object?
```

#### `compare_chunking`
Compare different chunking strategies on the same document.

```yaml
name: compare_chunking
description: >
  Chunk the same document with different strategies and compare results.
  Shows how chunk boundaries, sizes, and retrieval quality differ.
parameters:
  document_id: string
  strategies:
    - type: enum             # fixed | sentence | recursive | semantic
      chunk_size: int
      chunk_overlap: int
  test_query: string?        # Optional: test retrieval with each strategy
returns:
  comparisons:
    - strategy: object
      chunk_count: int
      avg_chunk_tokens: int
      min_chunk_tokens: int
      max_chunk_tokens: int
      sample_chunks: object[]  # First 3 chunks for preview
      retrieval_result: object? # If test_query provided
```

---

### 9. ANALYTICS SKILLS

Usage and performance data.

#### `get_analytics_data`
Query platform usage analytics.

```yaml
name: get_analytics_data
description: >
  Get usage analytics: token consumption, costs, latency stats,
  and throughput over time. Filterable by model, project, module, and date range.
parameters:
  period: enum?              # 1d | 7d | 30d | 90d | custom
  date_from: datetime?
  date_to: datetime?
  model: string?
  project_id: string?
  source_module: string?     # playground | prompt_lab | evaluation | batch | agent
  group_by: enum?            # hour | day | week | month | model | project | module
returns:
  summary:
    total_tokens: int
    total_cost: float
    total_requests: int
    avg_latency_ms: int
    p95_latency_ms: int
    avg_ttft_ms: int
    avg_tokens_per_second: float
  time_series:
    - timestamp: datetime
      tokens: int
      cost: float
      requests: int
      avg_latency_ms: int
  breakdown:                 # If group_by specified
    - group: string
      tokens: int
      cost: float
      requests: int
```

#### `get_logprobs_summary`
Aggregate logprobs statistics across runs.

```yaml
name: get_logprobs_summary
description: >
  Aggregate logprobs statistics across multiple runs. Shows confidence
  patterns, calibration data, and systematic uncertainty.
parameters:
  run_ids: string[]?
  experiment_id: string?
  model: string?
returns:
  overall:
    mean_perplexity: float
    perplexity_distribution: object  # Histogram
    mean_entropy: float
    entropy_distribution: object
  calibration:
    buckets:
      - confidence_range: [float, float]
        predicted_confidence: float
        actual_accuracy: float      # Only if ground truth available
        count: int
    expected_calibration_error: float?
  common_uncertain_tokens:
    - token: string
      avg_logprob: float
      frequency: int
```

---

### 10. HISTORY & REPLAY SKILLS

Browse inference history and replay past calls.

#### `search_history`
Search and filter inference history records.

```yaml
name: search_history
description: >
  Search the inference history for past calls matching criteria.
  Every inference call in the platform is recorded automatically.
parameters:
  source_module: string?     # playground | prompt_lab | evaluation | agent | batch
  model: string?
  provider_type: string?     # Vllm | Ollama | LmStudio
  tags: string[]?
  search: string?            # Full-text search across prompts and responses
  date_from: datetime?
  date_to: datetime?
  min_perplexity: float?
  max_perplexity: float?
  sort_by: string?           # created_at | latency | tokens | perplexity | cost
  limit: int?                # Default: 20
returns:
  records:
    - id: string
      source_module: string
      model: string
      provider_type: string
      prompt_preview: string   # First 100 chars of user message
      response_preview: string # First 100 chars of response
      tokens: int
      latency_ms: int
      perplexity: float?
      cost: float
      tags: string[]
      created_at: datetime
  total_matching: int
```

#### `get_history_record`
Get full details of a single inference record.

```yaml
name: get_history_record
description: >
  Get complete details of a historical inference call including the full
  request, response, logprobs data, and all metadata.
parameters:
  record_id: string
returns:
  id: string
  source_module: string
  model: string
  provider_type: string
  provider_endpoint: string
  request: object             # Full chat request (messages, params)
  response: object            # Full chat response (content, usage, logprobs)
  prompt_tokens: int
  completion_tokens: int
  latency_ms: int
  ttft_ms: int?
  perplexity: float?
  cost: float
  prompt_version_id: string?
  run_id: string?
  tags: string[]
  created_at: datetime
```

#### `replay_history`
Replay one or more historical inference records against a different model/provider/params.

```yaml
name: replay_history
description: >
  Replay historical inference records. Can change the model, provider, parameters,
  or prompt version. Returns comparison of original vs replayed results.
  Use this to validate model swaps, prompt changes, or provider migrations.
parameters:
  source:
    record_ids: string[]?     # Specific records to replay
    filter: object?           # Or filter criteria (same as search_history)
    tag: string?              # Or all records with this tag
  overrides:
    model: string?            # Use a different model
    provider_instance_id: string?  # Use a different provider
    parameters: object?       # Override temperature, top_p, etc.
    prompt_version_id: string? # Use a different prompt template
  options:
    capture_logprobs: bool?   # Default: true
    concurrency: int?         # Default: 4
    compute_similarity: bool? # Compare output similarity (default: true)
returns:
  replay_session_id: string
  status: string              # pending | running | complete
  total_records: int
  completed: int
  failed: int
  results:
    - original_record_id: string
      output_changed: bool
      output_similarity: float?
      original_perplexity: float?
      replayed_perplexity: float?
      original_tokens: int
      replayed_tokens: int
      original_latency_ms: int
      replayed_latency_ms: int
      original_cost: float
      replayed_cost: float
  summary:
    outputs_changed_count: int
    avg_similarity: float?
    total_original_cost: float
    total_replayed_cost: float
    avg_original_latency_ms: int
    avg_replayed_latency_ms: int
```

#### `tag_history`
Add or remove tags on inference history records.

```yaml
name: tag_history
description: >
  Tag inference history records for grouping. Tags enable replaying
  specific groups of records (e.g., "before-prompt-change", "ner-tests").
parameters:
  record_ids: string[]
  add_tags: string[]?
  remove_tags: string[]?
returns:
  updated_count: int
requires_approval: false
```

---

### 11. PROVIDER MANAGEMENT SKILLS

Runtime provider and model management.

#### `list_providers`
List all registered inference providers and their status.

```yaml
name: list_providers
description: >
  List all registered inference provider instances with their current
  status, model, capabilities, and health.
parameters: {}
returns:
  providers:
    - id: string
      name: string
      type: string           # Vllm | Ollama | LmStudio | OpenAiCompatible
      endpoint: string
      model: string
      status: string         # Healthy | Unhealthy | Unreachable
      capabilities: object   # What features this provider supports
      metrics: object?       # Current throughput, GPU, cache (if available)
```

#### `swap_model`
Hot-reload a different model on a provider that supports it.

```yaml
name: swap_model
description: >
  Load a different model on an inference provider instance.
  Only works on providers that support model hot-swap (Ollama, LM Studio).
  The current model is unloaded. In-flight requests may fail.
parameters:
  provider_instance_id: string
  model_id: string           # Model to load (e.g., "llama3.1:70b" for Ollama)
returns:
  previous_model: string
  new_model: string
  load_time_ms: int
  capabilities: object       # Updated capabilities for the new model
requires_approval: true      # Changing models affects all users of this provider
```

#### `swap_provider`
Switch which provider handles inference for a registered instance.

```yaml
name: swap_provider
description: >
  Update a registered provider instance's configuration at runtime.
  Change the endpoint, provider type, or other settings without restarting.
parameters:
  instance_id: string
  endpoint: string?          # New endpoint URL
  provider_type: string?     # New provider type
  name: string?              # New display name
returns:
  instance_id: string
  previous_config: object
  new_config: object
requires_approval: true
```

---

### 12. UTILITY SKILLS

General-purpose tools for agents.

#### `web_search`
Search the web (requires external API integration).

```yaml
name: web_search
description: >
  Search the web for information. Uses a configured search API
  (SearXNG, Brave Search, or similar).
parameters:
  query: string
  num_results: int?          # Default: 5
returns:
  results:
    - title: string
      url: string
      snippet: string
```

#### `calculator`
Evaluate mathematical expressions.

```yaml
name: calculator
description: >
  Evaluate a mathematical expression. Supports basic arithmetic,
  exponents, logarithms, trigonometry, and statistical functions.
parameters:
  expression: string         # e.g., "mean(0.94, 0.91, 0.88)" or "log2(32768)"
returns:
  result: float
  expression: string
```

#### `code_execution`
Execute code in a sandboxed environment.

```yaml
name: code_execution
description: >
  Execute Python code in a sandboxed environment. Has access to numpy,
  pandas, scipy, matplotlib. Output is captured as text and/or image.
parameters:
  code: string
  timeout_seconds: int?      # Default: 30
returns:
  stdout: string
  stderr: string
  images: string[]?          # Base64 encoded images (from matplotlib, etc.)
  execution_time_ms: int
```

#### `export_to_notebook`
Push data to a Jupyter notebook for deeper analysis.

```yaml
name: export_to_notebook
description: >
  Create or append to a Jupyter notebook with data, code, and
  visualizations. Useful for agents that want to leave artifacts
  for the researcher to explore further.
parameters:
  notebook_id: string?       # Existing notebook, or null to create new
  notebook_name: string?     # Name for new notebook
  cells:
    - type: enum             # markdown | code
      content: string
returns:
  notebook_id: string
  notebook_name: string
  url: string                # Link to open in JupyterLite
```

---

## Skill Composition

Skills can be composed into higher-level operations. The platform provides a few built-in compositions:

### Prompt Evaluation Pipeline
```
render_prompt -> run_inference (with logprobs) -> calculate_metrics -> save_run
```

### RAG Quality Check
```
query_rag_collection -> run_rag_pipeline (with logprobs) -> analyze_confidence -> flag hallucinations
```

### Model Comparison
```
run_inference_batch (model A) -> run_inference_batch (model B) -> compare_runs -> calculate_statistics
```

### Prompt Optimization Loop
```
get_prompt_version -> run_evaluation_sample -> analyze failures -> create_prompt_version -> repeat
```

### Provider Swap Validation
```
search_history (last 50 calls) -> swap_provider (new backend) -> replay_history (overrides: new provider) -> compare results -> report regressions
```

### Model Upgrade Regression Test
```
tag_history ("baseline") -> swap_model (new version) -> replay_history (tag: "baseline", override: new model) -> diff outputs + perplexity
```

### History-Driven Evaluation
```
search_history (filter: tag="ner-tests") -> replay_history (override: prompt_version=v4) -> compare original vs replayed -> calculate_statistics
```

---

## Adding Custom Skills

Users can register custom skills via OpenAPI spec or function schema:

```yaml
# Custom skill definition
name: my_custom_api
description: Query my internal knowledge base API
endpoint:
  method: POST
  url: https://internal-api.example.com/search
  headers:
    Authorization: "Bearer ${MY_API_KEY}"
parameters:
  query: string
  limit: int?
returns:
  results: object[]
timeout_ms: 5000
```

Custom skills appear in the tool registry alongside built-in skills and can be assigned to any agent.

---

## Skill Registry API

```
GET  /api/v1/skills                    # List all registered skills
GET  /api/v1/skills/{name}             # Get skill details + schema
GET  /api/v1/skills/{name}/openai      # Get OpenAI function definition
POST /api/v1/skills/{name}/execute     # Execute a skill directly (for testing)
POST /api/v1/skills/custom             # Register a custom skill
GET  /api/v1/skills/categories         # List skill categories
```
