# Prism Demo Flows

Scripted demonstrations of flagship capabilities. Each flow can be executed in under 5 minutes.

## Prerequisites

- Prism running locally (API + Frontend + PostgreSQL)
- At least one inference provider registered (vLLM or Ollama)
- Seed data loaded (automatic on first dev launch)

---

## Flow 1: Token Analysis

**Goal:** Show how Prism reveals token-level model behavior.

1. Open **Playground** (http://localhost:5173/playground)
2. Select a registered inference instance from the parameter sidebar
3. Enable **Logprobs** toggle, set Top Logprobs to 10
4. Send: "What is the capital of France?"
5. Observe the response with inline **token heatmap** — green tokens are confident, red are uncertain
6. Click any token to open the **Token Inspector Drawer** on the right
7. Navigate between tokens with arrow buttons — observe probability, entropy, and alternatives
8. Switch to **Entropy** mode — see per-token entropy bars
9. Switch to **Surprise** mode — see wavy underlines on low-probability tokens
10. Check the **Response Metrics Strip** below the message: tokens, latency, TTFT, throughput, perplexity

**Key insight:** "Paris" should have very high confidence (>95%), while function words like "The" show moderate confidence with alternatives.

---

## Flow 2: Replay and Diff

**Goal:** Show how replaying a run against a different model reveals behavioral differences.

1. Open **History** (http://localhost:5173/history)
2. Select any successful inference record
3. Click **Replay** in the detail panel
4. Select a different provider instance (or the same one with parameter overrides)
5. Expand **Parameter Overrides** — change temperature to 1.5
6. Click **Replay**
7. Observe the **side-by-side diff** — word-level highlighting shows changes
8. Check the **Metrics Comparison** table — compare latency, token counts, and model names

**Key insight:** Higher temperature increases output variability. The diff view makes divergence immediately visible.

---

## Flow 3: Prompt Lab Experiment

**Goal:** Show systematic prompt development with A/B testing.

1. Open **Prompt Lab** (http://localhost:5173/prompt-lab)
2. Select the "Structured Data Extractor" template from the sidebar
3. Switch between **v1** and **v2** using the version selector
4. Click **Diff** to see the side-by-side version comparison
5. Go to the **Test** panel on the right
6. Fill in variables: text = "John visited Paris on March 5", fields = "name, date, location"
7. Click **Test Prompt** — observe the extracted JSON output with metrics
8. Select multiple instances in the **Compare** section
9. Click **Test N Instances** — see all results pinned for comparison
10. Click **Fork** to create a new template variant from the current version

**Key insight:** Versioning + testing + comparison enables systematic prompt engineering.

---

## Flow 4: Evaluation Leaderboard

**Goal:** Show model comparison through systematic evaluation.

1. Open **Evaluation** (http://localhost:5173/evaluation)
2. Click the **Leaderboard** tab
3. Observe model rankings by scoring method
4. Click into any evaluation to see detail
5. Check individual result records for per-row scores

**Key insight:** The leaderboard surfaces which models perform best on your specific evaluation criteria.

---

## Flow 5: RAG Trace

**Goal:** Show end-to-end RAG pipeline with retrieval inspection.

1. Open **RAG Workbench** (http://localhost:5173/rag)
2. Select the "AI Research Papers" collection (from seed data)
3. Use the **Search** panel to query: "How does the Transformer handle attention?"
4. Observe retrieved chunks with similarity scores
5. Click **Run RAG Pipeline** to generate a response with retrieved context
6. Review the assembled context and grounded citations

**Key insight:** You can see exactly which chunks were retrieved, their scores, and how they influenced the generated response.

---

## Flow 6: Agent Trace

**Goal:** Show step-by-step agent execution with tool call inspection.

1. Open **Agents** (http://localhost:5173/agents)
2. Select the "Research Assistant" workflow (from seed data)
3. In the **Run Agent** tab, enter: "What are the key contributions of the Transformer architecture?"
4. Watch the execution trace populate in real-time (SSE streaming)
5. Each step shows: Thought, Action (tool call), Observation (tool result)
6. Switch to the **Trace View** tab for a timeline visualization
7. Click any step to expand and inspect tool input/output

**Key insight:** Every agent decision is transparent — you can see what the model thought, what tool it called, and what it observed.

---

## Flow 7: Parameter Sweep

**Goal:** Show how to systematically explore parameter space.

1. Open **Experiments** (http://localhost:5173/experiments)
2. Navigate to any project and experiment
3. Click **Sweep** button
4. Configure: Temperature values [0.0, 0.3, 0.7, 1.0, 1.5]
5. Enter a prompt and select an instance
6. Click **Run Sweep** — observe the combination count
7. After completion, view the runs in the run table
8. Check the **Stats Summary** above the table for aggregate metrics
9. Select 2+ runs and click **Compare** for side-by-side analysis

**Key insight:** Sweeps let you find the optimal parameters empirically rather than guessing.
