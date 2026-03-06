# AI Research Workbench — Agent Architecture

## Overview

Agents in this platform serve two distinct purposes:

1. **Platform Agents** — Built-in AI assistants that help *you* do research more effectively. They operate inside the workbench, calling platform APIs as tools. Think of them as research co-pilots.
2. **User-Built Agents** — Custom agent workflows you design, test, and evaluate as research subjects. These are the agents you're studying, not the ones helping you study.

Both types share the same execution engine, tool system, and tracing infrastructure. The difference is intent: platform agents are utilities, user-built agents are experiments.

### Architecture Alignment

Agents follow the same patterns as the rest of the platform (see `ARCHITECTURE.md`):

- **Result pattern:** All agent operations return `Result<T>`. Agent execution returns `Result<AgentRunTrace>`. Skills return `SkillResult` (which wraps `Result<T>` with metrics). Errors propagate cleanly — a tool failure doesn't crash the agent, it becomes an observation the agent can reason about.
- **Provider abstraction:** Agents call models via `IInferenceProvider`, not vLLM directly. An agent can use any registered provider (vLLM, Ollama, LM Studio). The agent config specifies a model identifier, and `IInferenceProviderFactory` resolves the right provider.
- **Vertical slice:** The Agents feature lives in `AiResearch.Features/Agents/` with its own Domain, Application, Infrastructure, and Api layers. Agent patterns (ReAct, etc.) are strategy implementations in the Application layer.
- **Skills as tools:** Agents invoke skills (see `SKILLS.md`), which are thin wrappers over feature application-layer handlers. This means agents get the same validation, logging, and Result handling as direct API calls.
- **XML documentation:** All public agent interfaces, pattern implementations, and executor methods carry full XML doc comments.
- **Structured logging:** Every agent step is logged with Serilog structured properties (AgentRunId, StepNumber, ToolName, ToolSelectionConfidence, TokensUsed).

---

## Execution Engine

### Architecture

```
┌──────────────────────────────────────────────────────────────┐
│                    Agent Runtime                              │
│                                                              │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────┐  │
│  │ IAgentPattern │  │ SkillRegistry│  │ ISkillTraceLogger │  │
│  │ (Strategy)    │  │ (ISkill[])   │  │ (Structured Logs) │  │
│  └───────┬──────┘  └──────┬───────┘  └────────┬──────────┘  │
│          │                │                     │             │
│  ┌───────┴────────────────┴─────────────────────┴─────────┐  │
│  │               AgentExecutor                             │  │
│  │                                                         │  │
│  │  Loop: returns Result<AgentRunTrace>                    │  │
│  │    1. Format context -> IInferenceProvider (logprobs)   │  │
│  │    2. Parse Result<ChatResponse> -> action | answer     │  │
│  │    3. Resolve ISkill from registry -> execute           │  │
│  │    4. SkillResult wraps Result<T> + metrics             │  │
│  │    5. Log step (Serilog structured: RunId, Step, Tool)  │  │
│  │    6. Check guardrails -> Result.Failure if exceeded    │  │
│  │    7. Repeat or return Result<AgentRunTrace>            │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │  IInferenceProvider (resolved via factory)              │  │
│  │  - Any backend: vLLM, Ollama, LM Studio                │  │
│  │  - Capabilities checked before logprobs/guided decoding │  │
│  └─────────────────────────────────────────────────────────┘  │
│                                                              │
│  ┌─────────────────────────────────────────────────────────┐  │
│  │               Guardrails                                │  │
│  │  - Max steps          - Token budget                    │  │
│  │  - Max wall time      - Blocked tool patterns           │  │
│  │  - Cost ceiling       - Required human approval         │  │
│  │  - Output validation  - Loop detection                  │  │
│  └─────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────┘
```

### Error Handling in Agent Loops

Agent execution never throws. Errors are values:

```
Tool returns SkillResult.Failure
  -> AgentExecutor converts to observation: "Tool 'X' failed: {error.Message}"
  -> LLM sees the error and can retry with different parameters or choose a different tool
  -> If max retries exceeded -> step recorded with error, agent continues

Provider returns Result.Failure (model unavailable, timeout)
  -> Agent step recorded with error
  -> If critical (provider down) -> AgentExecutor returns Result.Failure(Error.Unavailable(...))
  -> If transient (timeout) -> retry based on guardrail config

Guardrail exceeded (steps, tokens, cost)
  -> AgentExecutor returns Result.Failure(Error.Validation("Token budget exceeded: 16000/16000"))
  -> Partial trace is still returned for debugging
```

### Agent Patterns

Patterns are the reasoning strategies an agent follows. Each pattern defines how the LLM output is parsed and how the agent loop progresses.

#### ReAct (Reasoning + Acting)
The default pattern. The model interleaves thinking and tool use.

```
System: You are a research assistant. You have access to the following tools: ...
        Use this format:
        Thought: <reasoning about what to do>
        Action: <tool_name>
        Action Input: <JSON parameters>
        ... (observe result) ...
        Thought: <reasoning about result>
        Final Answer: <response to user>

Step 1: Thought -> "I need to find experiments with F1 > 0.9"
         Action -> search_experiments({metric: "f1", min: 0.9})
Step 2: Observation -> [3 experiments found]
         Thought -> "Let me compare their configurations"
         Action -> compare_runs({ids: [47, 46, 44]})
Step 3: Observation -> [comparison table]
         Thought -> "The key difference is temperature"
         Final Answer -> "Your best F1 scores all use temperature 0.1..."
```

#### Plan-and-Execute
For complex multi-step tasks. The model first creates a plan, then executes each step.

```
Phase 1 - Planning:
  System: Given this task, create a numbered plan of steps.
  LLM -> "1. Load the dataset  2. Run eval on model A  3. Run eval on model B  4. Compare results"

Phase 2 - Execution:
  For each step:
    System: Execute step N of the plan: <step description>
    LLM -> (uses tools to execute the step)
    Log result, move to next step

Phase 3 - Synthesis:
  System: Summarize the results of all steps.
  LLM -> Final summary
```

#### Critique-Revise
For iterative refinement tasks like prompt optimization.

```
Loop:
  1. Generate: Produce an output (e.g., a prompt, a response)
  2. Critique: Evaluate the output against criteria (can use logprobs, eval metrics)
  3. Revise: Improve the output based on critique
  4. Check: Has quality threshold been met? If yes, stop. If no, loop.
```

#### Observe-Hypothesize-Test (Research-Specific)
Purpose-built for AI research workflows.

```
1. Observe: Gather data (run inference, collect logprobs, check metrics)
2. Hypothesize: Form a hypothesis about model behavior
3. Test: Design and execute an experiment to test the hypothesis
4. Analyze: Evaluate results, update understanding
5. Report: Summarize findings
```

#### Custom Pattern
Users can define custom patterns via a system prompt template and output parsing rules.

---

## Platform Agents (Built-In Research Co-Pilots)

These agents ship with the platform. They use platform skills (see SKILLS.md) as tools and are pre-configured for specific research workflows. Users can customize them or use them as-is.

### 1. Research Assistant

**Purpose:** General-purpose research helper. Answers questions about your experiments, suggests next steps, finds patterns in results.

```yaml
name: research-assistant
pattern: react
model: ${default_model}
system_prompt: |
  You are an AI research assistant working inside the AI Research Workbench.
  You help the researcher analyze experiments, understand model behavior,
  and plan next steps. You have access to all platform data through tools.

  When analyzing results, always consider:
  - Statistical significance (is the sample size large enough?)
  - Confounding variables (did multiple things change between runs?)
  - Cost efficiency (is a small quality gain worth 10x the cost?)

  Be concise. Lead with insights, not process.
tools:
  - search_experiments
  - get_run_details
  - compare_runs
  - search_datasets
  - get_dataset_stats
  - run_inference
  - get_logprobs
  - calculate_metrics
  - search_prompts
guardrails:
  max_steps: 15
  token_budget: 16000
  cost_ceiling: $1.00
```

**Example interactions:**
- "Which prompt version gives the best F1 on the billing subset?"
- "Why did run-047 outperform run-046?"
- "What should I try next to improve NER accuracy?"
- "Show me tokens where the model is least confident in run-047's output"

---

### 2. Prompt Optimizer

**Purpose:** Iteratively improves prompts based on evaluation results. Uses the Critique-Revise pattern.

```yaml
name: prompt-optimizer
pattern: critique-revise
model: ${default_model}
system_prompt: |
  You are a prompt engineering expert. Given a prompt template and evaluation
  results, you iteratively improve the prompt to maximize the target metric.

  Strategy:
  1. Analyze current performance (look at failures, low-confidence outputs)
  2. Identify patterns in failures (common error types, edge cases)
  3. Propose specific prompt modifications
  4. Test the modified prompt on a sample
  5. Compare metrics

  Focus on high-leverage changes:
  - Clarifying ambiguous instructions
  - Adding/improving few-shot examples (pick examples that cover failure cases)
  - Restructuring for better chain-of-thought
  - Adjusting output format constraints
tools:
  - get_prompt_version
  - create_prompt_version
  - run_evaluation_sample    # Run eval on a small sample for fast iteration
  - get_evaluation_results
  - get_logprobs             # Analyze WHERE the model fails (low confidence tokens)
  - search_dataset_records   # Find records similar to failure cases
  - run_inference            # Test individual examples
guardrails:
  max_steps: 20
  max_iterations: 5          # Max critique-revise cycles
  token_budget: 32000
  cost_ceiling: $5.00
  stop_condition: "metric_improvement < 0.01 for 2 consecutive iterations"
```

**Example interactions:**
- "Optimize my NER v3 prompt to improve F1 on the customer_support test set"
- "My summarization prompt is generating outputs that are too long. Fix it."
- "Improve the few-shot examples for my code review prompt"

---

### 3. Data Analyst

**Purpose:** Analyzes experiment results, generates statistical summaries, creates visualizations, identifies trends.

```yaml
name: data-analyst
pattern: react
model: ${default_model}
system_prompt: |
  You are a data analyst specializing in ML experiment analysis.
  You analyze experiment results, identify statistically significant
  differences, find correlations, and produce clear summaries.

  Always:
  - Report sample sizes alongside metrics
  - Note when differences may not be statistically significant
  - Consider practical significance, not just statistical significance
  - Look for confounding variables
  - Present cost/quality tradeoffs
tools:
  - search_experiments
  - get_run_details
  - compare_runs
  - get_evaluation_results
  - calculate_statistics     # Mean, stddev, confidence intervals, t-tests
  - get_analytics_data       # Usage, cost, latency data
  - get_logprobs_summary     # Aggregate logprobs statistics
  - export_to_notebook       # Push results to a Jupyter notebook for deeper analysis
guardrails:
  max_steps: 20
  token_budget: 24000
  cost_ceiling: $2.00
```

**Example interactions:**
- "Compare all llama-70b runs vs mixtral runs on NER. Is the difference significant?"
- "What's the correlation between temperature and perplexity across my experiments?"
- "Generate a summary report of this week's experiments"
- "Is my model's confidence well-calibrated on the test set?"

---

### 4. Dataset Curator

**Purpose:** Helps build, clean, and augment datasets. Identifies quality issues, suggests improvements, generates synthetic examples.

```yaml
name: dataset-curator
pattern: plan-and-execute
model: ${default_model}
system_prompt: |
  You are a dataset engineering specialist. You help build high-quality
  datasets for LLM evaluation and fine-tuning.

  Key concerns:
  - Data quality (inconsistencies, labeling errors, ambiguous examples)
  - Distribution balance (are categories represented fairly?)
  - Edge cases (does the dataset cover difficult/unusual inputs?)
  - Contamination (are there duplicates or near-duplicates?)
  - Tokenization impact (are examples within context length limits?)
tools:
  - get_dataset_info
  - get_dataset_stats
  - search_dataset_records
  - update_dataset_record
  - generate_synthetic_data
  - deduplicate_dataset
  - get_token_counts        # Tokenize records, check lengths
  - run_inference            # Generate labels, classify, or evaluate examples
  - get_logprobs             # Use model confidence to identify ambiguous examples
guardrails:
  max_steps: 25
  token_budget: 32000
  cost_ceiling: $3.00
  require_approval:
    - update_dataset_record  # Don't modify data without human confirmation
    - deduplicate_dataset
```

**Example interactions:**
- "Audit my customer_support dataset for quality issues"
- "Generate 50 more 'billing' examples that cover edge cases we're missing"
- "Find records where the model is least confident — they might be mislabeled"
- "Prepare this dataset for fine-tuning in ShareGPT format"

---

### 5. RAG Debugger

**Purpose:** Diagnoses and optimizes RAG pipeline performance. Identifies retrieval failures, suggests chunking improvements, detects hallucination.

```yaml
name: rag-debugger
pattern: observe-hypothesize-test
model: ${default_model}
system_prompt: |
  You are a RAG pipeline specialist. You diagnose retrieval failures,
  optimize chunking strategies, and detect hallucination in generated
  responses.

  Common issues to investigate:
  - Retrieval failures (relevant content exists but isn't retrieved)
  - Chunking problems (relevant info split across chunks, chunks too large/small)
  - Embedding mismatches (query and relevant content use different terminology)
  - Hallucination (model generates claims not in retrieved context)
  - Context window waste (retrieved chunks contain mostly irrelevant padding)
tools:
  - query_rag_collection
  - get_chunk_details
  - run_rag_pipeline
  - get_logprobs             # Low confidence in RAG response = possible hallucination
  - compare_chunking         # Re-chunk same doc with different settings, compare retrieval
  - get_embedding_similarity # Check similarity between query and chunks
  - search_documents         # Find documents that should be relevant
guardrails:
  max_steps: 20
  token_budget: 24000
  cost_ceiling: $3.00
```

**Example interactions:**
- "My RAG pipeline can't answer questions about memory limits even though vllm.md has the info. Why?"
- "Compare recursive chunking at 256/512/1024 tokens for my tech-docs collection"
- "Flag any hallucinated claims in the last 50 RAG responses"
- "What's the optimal chunk size for my legal corpus?"

---

### 6. Model Behavior Investigator

**Purpose:** Deep analysis of model behavior using logprobs, token prediction, and systematic probing. The most research-oriented agent.

```yaml
name: model-investigator
pattern: observe-hypothesize-test
model: ${default_model}
system_prompt: |
  You are a model behavior researcher. You use logprobs, token prediction,
  and systematic probing to understand how LLMs make decisions.

  Research methods:
  - Logprobs analysis (confidence patterns, entropy distribution)
  - Minimal pair testing (change one thing, observe effect)
  - Token prediction analysis (what does the model "want" to say?)
  - Prompt perturbation (how sensitive is the model to phrasing?)
  - Cross-model comparison (do different models behave the same way?)

  Always formulate clear hypotheses before testing them.
  Report findings with evidence, not speculation.
tools:
  - run_inference
  - get_logprobs
  - predict_next_token
  - explore_branch           # Force a token, see how generation changes
  - tokenize_text
  - run_inference_batch      # Run multiple variations efficiently
  - compare_runs
  - calculate_metrics
guardrails:
  max_steps: 30
  token_budget: 48000
  cost_ceiling: $10.00
```

**Example interactions:**
- "Why does llama-70b output JSON with trailing commas on this prompt but not that one?"
- "Is the model more confident when answering factual questions vs creative ones?"
- "Test if adding 'think step by step' actually changes the token probability distribution"
- "Compare how llama and mixtral tokenize and process code differently"

---

## User-Built Agents

Users can create custom agents using the Agent Builder module. These follow the same execution engine but are fully configurable.

### Configuration Schema

```yaml
# Full agent configuration schema
name: string                     # Agent identifier
description: string              # What this agent does
pattern: enum                    # react | plan-and-execute | critique-revise |
                                 # observe-hypothesize-test | custom
model: string                    # Model ID or ${default_model}
system_prompt: string            # The agent's instruction prompt
tools: string[]                  # List of tool names from registry
parameters:                      # Model parameters
  temperature: float
  top_p: float
  max_tokens: int
  logprobs: bool                 # Capture logprobs for analysis (default: true)
  top_logprobs: int
guardrails:
  max_steps: int                 # Hard limit on reasoning steps
  max_iterations: int            # For iterative patterns (critique-revise)
  token_budget: int              # Total tokens (prompt + completion) across all steps
  cost_ceiling: float            # Maximum dollar cost
  max_wall_time: duration        # Maximum execution time (e.g., "5m", "1h")
  blocked_tools: string[]        # Tools this agent cannot use
  require_approval: string[]     # Tools that need human confirmation before execution
  output_validation: json_schema # Validate final output against schema
  loop_detection: bool           # Detect and break repeated action patterns
custom_pattern:                  # Only if pattern == "custom"
  step_template: string          # Prompt template per step
  parse_rules: object            # How to parse LLM output into action/answer
  stop_conditions: string[]      # When to stop
memory:                          # Agent memory configuration
  type: enum                     # none | buffer | summary | sliding_window
  max_tokens: int                # Memory budget
  summary_model: string          # Model for memory summarization (if type == summary)
```

### Example User Agent: Literature Reviewer

```yaml
name: paper-reviewer
description: Reads and summarizes research papers from RAG collection
pattern: plan-and-execute
model: llama-70b
system_prompt: |
  You review AI research papers stored in the platform's RAG collections.
  Given a research topic, you:
  1. Search for relevant papers/sections
  2. Extract key claims and methodologies
  3. Identify agreements and contradictions across papers
  4. Produce a structured literature review summary
tools:
  - query_rag_collection
  - get_chunk_details
  - run_inference
  - get_logprobs
parameters:
  temperature: 0.3
  max_tokens: 4096
guardrails:
  max_steps: 20
  token_budget: 32000
  cost_ceiling: $5.00
```

---

## Execution Trace

Every agent run produces a detailed trace. This is critical for research — you need to understand *why* an agent did what it did.

### Trace Schema

```json
{
  "run_id": "agent-run-001",
  "workflow_id": "research-assistant",
  "status": "complete",
  "started_at": "2026-03-05T10:00:00Z",
  "finished_at": "2026-03-05T10:00:12Z",
  "total_steps": 4,
  "total_tokens": 3847,
  "total_cost": 0.058,
  "input": "Which prompt version gives the best F1 on billing?",
  "output": "NER v3 at temperature 0.1 gives F1=0.94...",
  "steps": [
    {
      "step": 1,
      "type": "thought",
      "content": "I need to search for experiments on the billing subset",
      "logprobs": { "perplexity": 2.1, "min_confidence_token": "billing" },
      "tokens": 89,
      "latency_ms": 340
    },
    {
      "step": 2,
      "type": "action",
      "tool": "search_experiments",
      "tool_input": { "dataset_subset": "billing", "metric": "f1", "sort": "desc" },
      "tool_output": { "runs": [{"id": 47, "f1": 0.94}, ...] },
      "tool_selection_confidence": 0.91,
      "tokens": 156,
      "latency_ms": 520
    },
    {
      "step": 3,
      "type": "thought",
      "content": "Run 47 has the best F1. Let me get its details.",
      "tokens": 45,
      "latency_ms": 180
    },
    {
      "step": 4,
      "type": "final_answer",
      "content": "NER v3 at temperature 0.1 gives F1=0.94...",
      "logprobs": { "perplexity": 1.8 },
      "tokens": 234,
      "latency_ms": 890
    }
  ],
  "guardrail_checks": {
    "steps_used": "4/15",
    "tokens_used": "3847/16000",
    "cost_used": "$0.058/$1.00",
    "loop_detected": false
  }
}
```

---

## Agent Evaluation

Agents are research subjects too. The platform supports evaluating agent performance.

### Metrics

| Metric | Description |
|--------|-------------|
| **Task completion rate** | Did the agent achieve its goal? (manual or automated check) |
| **Step efficiency** | Steps taken vs minimum steps needed |
| **Tool selection accuracy** | Did the agent pick the right tool? (logprobs confidence vs outcome) |
| **Cost efficiency** | Total cost vs simpler approaches (single LLM call, hardcoded pipeline) |
| **Hallucination rate** | Did the agent claim things not supported by tool outputs? |
| **Guardrail trigger rate** | How often does the agent hit limits? |
| **Latency** | Total wall time, per-step breakdown |
| **Reasoning quality** | Are the "Thought" steps logical? (LLM-as-judge on trace) |

### Evaluation Method

1. Create a test dataset of agent tasks (input + expected outcome)
2. Run agent on each task
3. Score outputs (automated + human review)
4. Analyze traces for patterns (common failure modes, tool misuse, reasoning errors)
5. Compare agent configurations (different models, patterns, system prompts)

---

## Agent-to-Agent Communication (Future)

Not in initial scope. If added later:
- Agents can invoke other agents as tools (sub-agent pattern)
- Parent agent receives child agent's final output + cost summary
- Trace nests child traces inside parent
- Guardrails cascade (child inherits remaining budget from parent)
