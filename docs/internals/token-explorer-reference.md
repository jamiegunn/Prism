# Token Explorer — Feature Documentation

The Token Explorer is a research-oriented tool for inspecting LLM inference at the token level. It exposes next-token predictions, step-through generation, branch exploration, tokenization analysis, and cross-model comparison.

---

## Overview

| Aspect | Detail |
|--------|--------|
| Backend slice | `Features/TokenExplorer/` |
| Frontend slice | `frontend/src/features/token-explorer/` |
| API prefix | `/api/v1/token-explorer` |
| State management | Zustand (`useTokenExplorerStore`) with localStorage persistence |
| Tabs | Predictions, Step Through, Branches, Tokenizer, Compare |

---

## Capabilities

### 1. Next-Token Predictions

Predict the most likely next tokens given a prompt, with full probability distributions.

- **Endpoint:** `POST /api/v1/token-explorer/predict`
- **Handler:** `PredictNextTokenHandler` — sends prompt (optionally with assistant prefix) to the model with `max_tokens: 1` and `logprobs` enabled, returns top-N token candidates with probabilities
- **Frontend tab:** "Predictions" — enter a prompt, click Predict, see ranked token candidates with probability bars
- Supports assistant prefill for continuation predictions
- Shows raw log probabilities alongside normalized percentages

**Sampling Analysis Panel:**
When predictions are available, an expandable panel shows statistical analysis of the token distribution:

| Stat | Description |
|------|-------------|
| Effective Vocab | Number of tokens with non-negligible probability (effective vocabulary size) |
| Entropy | Shannon entropy measuring uncertainty in the distribution (bits) |
| Top-p Coverage | How many tokens needed to reach the configured top-p threshold |
| Top-k Effect | Percentage of total probability captured by the top-k tokens |
| Max Probability | Highest single-token probability (model confidence in best guess) |
| Model Confidence | Qualitative assessment: Certain / Confident / Moderate / Uncertain / Very Uncertain |
| Distribution Shape | Whether the distribution is Peaked, Smooth, Uniform, or Bimodal |

Each stat card has a tooltip explaining its meaning and how to interpret it. See ADR-012.

### 2. Step-Through Generation

Walk through autoregressive generation one token at a time, seeing the full prediction distribution at each step.

- **Endpoint:** `POST /api/v1/token-explorer/step`
- **Handler:** `StepThroughHandler` — uses assistant prefill pattern to continue from accumulated tokens
- **Frontend tab:** "Step Through" — two modes:
  - **Step (Greedy):** automatically selects the highest-probability token and advances
  - **Step (Sample):** samples from the distribution according to temperature/top-p/top-k settings

**How it works:**
1. The original prompt is always sent as the user message
2. Previously accumulated tokens are sent as an assistant message prefix
3. vLLM receives `continue_final_message: true` + `add_generation_prompt: false` to continue from the prefix
4. The model predicts the next token from the accumulated context
5. The selected token is appended to the step history

**UI shows:**
- The accumulated generated text growing token by token
- Full prediction distribution at each step
- Step history as a scrollable list

### 3. Branch Exploration

Explore alternative generation paths by choosing different tokens at any prediction point.

- **Endpoint:** `POST /api/v1/token-explorer/branch`
- **Handler:** `BranchHandler` — generates a short continuation from a given prefix to show where an alternative token choice would lead
- **Frontend tab:** "Branches" — after a prediction, click any alternative token to see a branching continuation
- Calculates perplexity for each branch to compare path quality
- Visualizes the branching tree of possible continuations

### 4. Tokenization

Inspect how text is tokenized by the model's tokenizer.

- **Endpoint:** `POST /api/v1/token-explorer/tokenize`
- **Handler:** `TokenizeHandler` — sends text through the model's tokenizer, returns token IDs and string representations
- **Frontend tab:** "Tokenizer" — enter text, see individual tokens color-coded with their IDs
- Can be embedded in the Token Explorer page or used standalone
- Shows token count, individual token boundaries, and token IDs

### 5. Cross-Model Comparison

Compare how different models tokenize the same text.

- **Endpoint:** `POST /api/v1/token-explorer/compare`
- **Handler:** `CompareHandler` — tokenizes the same text across multiple model instances, returns side-by-side results
- **Frontend tab:** "Compare" — select multiple models, enter text, see tokenization differences
- Highlights differences in token boundaries and vocabulary between models
- Can be embedded in the Token Explorer page or used standalone

### 6. Enable Thinking Toggle

For models that support reasoning chains (e.g., Qwen3), a toggle to enable/disable the `<think>` reasoning block.

- **Parameter:** `enableThinking` — sent on predict, step, and branch requests
- **UI:** Brain icon toggle in the configuration section
- When disabled, the model skips its internal reasoning chain, producing direct predictions
- Persisted in the Zustand store

---

## Frontend Components

| Component | File | Purpose |
|-----------|------|---------|
| `TokenExplorerPage` | `TokenExplorerPage.tsx` | Top-level layout: config sidebar + tabbed content area |
| `PredictionsView` | `components/PredictionsView.tsx` | Token predictions with probability bars |
| `StepThroughView` | `components/StepThroughView.tsx` | Step-by-step generation with greedy/sample modes |
| `BranchView` | `components/BranchView.tsx` | Branch exploration tree |
| `TokenizerView` | `components/TokenizerView.tsx` | Text tokenization visualization |
| `TokenCompareView` | `components/TokenCompareView.tsx` | Cross-model tokenization comparison |
| `SamplingVisualization` | `components/SamplingVisualization.tsx` | Statistical analysis of token distributions |
| `HelpPanel` | `components/HelpPanel.tsx` | Collapsible help section (What/Why/How/Tip) |

### Layout

```
+-----------------------------------------------------------+
|  Token Explorer                                            |
+----------------+------------------------------------------+
|                |  [Predictions] [Step Through] [Branches]  |
|  Config        |  [Tokenizer] [Compare]                    |
|  Sidebar       |------------------------------------------|
|                |  Help Panel (collapsible)                 |
|  - Instance    |------------------------------------------|
|  - Top Logprobs|                                          |
|  - Temperature |  Tab Content                             |
|  - Top P       |  (predictions, step-through, etc.)       |
|  - Top K       |                                          |
|  - Thinking    |  Sampling Analysis (collapsible)          |
|                |                                          |
+----------------+------------------------------------------+
```

### Help Panels (ADR-011)

Each tab has a collapsible help section explaining:
- **What** the feature does
- **Why** it's useful for research
- **How** to interpret the results
- **Tip** for getting the most out of it

Collapsed by default. Toggle via chevron icon.

---

## Backend Architecture

### Application

| Use Case | Type | Description |
|----------|------|-------------|
| `PredictNextToken` | Command | Single next-token prediction with logprobs |
| `StepThrough` | Command | Continue generation from accumulated prefix |
| `Branch` | Command | Generate continuation from alternative token |
| `Tokenize` | Command | Tokenize text via model tokenizer |
| `Compare` | Command | Cross-model tokenization comparison |

### Key Technical Details

**Assistant Prefill Pattern:**
For step-through and branch operations, the backend constructs messages as:
```
[User(original_prompt), Assistant(accumulated_tokens)]
```

The `VllmProvider` detects when the last message has the Assistant role and sets:
- `continue_final_message: true` — tells vLLM to continue from the assistant prefix
- `add_generation_prompt: false` — prevents vLLM from adding a new assistant turn marker

This ensures the model continues generating from the exact token position rather than starting a new turn.

**Perplexity Calculation:**
Branch exploration calculates perplexity for each path:
```
perplexity = exp(-1/N * sum(log_probs))
```
Lower perplexity indicates the model finds the sequence more natural/likely.

---

## State Management

The `useTokenExplorerStore` (Zustand) manages:

- Prompt text
- Selected model instance ID
- Inference parameters (temperature, topP, topK, topLogprobs)
- Enable thinking toggle
- Step history (for step-through mode)
- Active tab selection

Persisted fields configured via `partialize` — transient state like step history is excluded.

---

## Configuration Controls

All controls use the `ParamLabel` component with tooltips (ADR-012):

| Control | Tooltip Summary |
|---------|----------------|
| Model Instance | Which model to use for predictions |
| Top Logprobs | Number of alternative tokens shown per position |
| Temperature | Higher = more random predictions |
| Top P | Nucleus sampling threshold |
| Top K | Hard limit on candidate tokens |
| Enable Thinking | Toggle reasoning chain for supported models |

---

## API Request/Response Types

### PredictRequest
```typescript
{
  instanceId: string
  prompt: string
  topLogprobs?: number
  temperature?: number
  topP?: number
  topK?: number
  enableThinking?: boolean
  assistantPrefix?: string
}
```

### StepRequest
```typescript
{
  instanceId: string
  prompt: string
  selectedToken: string
  previousTokens?: string
  topLogprobs?: number
  temperature?: number
  enableThinking?: boolean
}
```

### TokenizeRequest
```typescript
{
  instanceId: string
  text: string
}
```

### CompareRequest
```typescript
{
  instanceIds: string[]
  text: string
}
```
