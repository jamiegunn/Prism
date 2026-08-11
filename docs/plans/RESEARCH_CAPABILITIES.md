# Research capabilities — plan

**Status:** proposed, not started.
**Companion:** every item here is implemented using
[`docs/prompts/IMPLEMENT_RESEARCH_CAPABILITY.md`](../prompts/IMPLEMENT_RESEARCH_CAPABILITY.md).
That prompt is not optional reading — it defines what "done" means for this plan, and in
particular what counts as proof for a number.

---

## The problem this plan solves

Prism's distinctive asset is that it records everything: per-token probabilities, time to first
token, decode rate, tool calls, retrieval scores. Almost none of it can leave, and several of
the numbers it computes cannot be compared to anyone else's.

Two consequences, and they are the whole plan:

**The recordings are trapped.** There is no export endpoint on History. Every Python research
library — `sacrebleu`, `ragas`, `lm-evaluation-harness`, MLflow — is unreachable, not because
Prism is .NET but because the data has no way out.

**Some of the numbers are not comparable.** BLEU and ROUGE-L are hand-rolled. BLEU in particular
varies with tokenisation, smoothing and brevity-penalty handling, which is exactly why
`sacrebleu` exists and why papers cite it by name. A hand-rolled BLEU produces a number that
cannot be compared with any published result, which defeats the purpose of using BLEU at all.

A third, quieter one: OpenTelemetry is already a dependency but only instruments ASP.NET and
HTTP. There is no `gen_ai.*` anywhere, so Prism's traces are a private shape that no standard
tool reads.

---

## Rules for every item in this plan

These are summarised here and specified in the prompt. An item that skips any of them is not
done, regardless of whether it works.

1. **The capability must be reachable from the UI.** A metric no page displays, an export no
   button triggers, a span nothing surfaces — these are the exact failure this repository has
   been correcting for two days. `useAbTest`, `useRagPipeline`, `useUpdateWorkflow` and
   `CalibrationPlot` are all finished work that no user can reach.
2. **Numbers are proved, not asserted.** Every metric carries reference vectors from a published
   source, invariants that must hold for all inputs, and a differential test against a reference
   implementation where one exists. "The test passes" is not evidence the maths is right; it is
   evidence the code agrees with itself.
3. **Assume the first version is wrong.** Build it, then attack it, then fix it, then attack it
   again. Two adversarial passes minimum, documented.
4. **Tests must be shown to fail.** Every new test is mutation-checked: break the code it
   guards, watch it go red, restore. A test never observed failing is not known to test anything.
5. **Requirements are updated in the same change.** The tab's `docs/features/<tab>.md`
   requirements table gains the new rows, MET with the check that proved them.

---

## Phase 1 — Let the data out

Nothing else in this plan is reachable until this exists. Smallest phase, largest unlock.

### 1.1 History export

| | |
|---|---|
| **Library** | `Parquet.Net` for Parquet; JSONL needs nothing |
| **Backend** | `GET /api/v1/history/export?format=jsonl\|parquet\|csv`, honouring every filter the search endpoint accepts |
| **UI** | An Export control on `/history`, beside the filter bar, exporting **what the current filters select** — not everything. It states the row count before writing |
| **Proof** | Round-trip: export N filtered rows, re-read the file, assert the set of ids and every scalar field matches the API response exactly. Parquet schema asserted field by field against the DTO |
| **Traps** | Streaming, not buffering — a 100k-row export must not be assembled in memory. Null must survive as null, not become 0 or "" |

### 1.2 A real notebook client

| | |
|---|---|
| **Library** | none server-side; `pandas` inside the notebook |
| **Backend** | none — consumes 1.1 |
| **UI** | The Notebooks page shows the available `workbench` calls and a copyable snippet that actually runs |
| **Proof** | A notebook executed in the shipped JupyterLite kernel fetches records and produces a DataFrame with the expected columns and dtypes. Not a snippet in a doc — an executed cell |
| **Traps** | `workbench.py` currently only ships `help()`. Its `chat()` accepts a `model` argument it never sends. Verify every function against the live API before documenting it |

---

## Phase 2 — Make the numbers comparable

### 2.1 Reference-correct BLEU and ROUGE-L

| | |
|---|---|
| **Library** | port `sacrebleu`'s definition; keep the implementation in-process |
| **Backend** | Replace `BleuScorer` and `RougeLScorer`. Record which definition and version produced each score alongside the score |
| **UI** | Evaluation shows the metric definition next to the number — tokeniser, smoothing, whether it is corpus- or sentence-level. A BLEU with no stated tokeniser is not a citable number |
| **Proof** | **Differential**: agree with `sacrebleu` to 1e-9 on at least 20 published sentence pairs, including empty hypotheses, single tokens, and no-overlap cases. **Invariants**: identical strings score 1.0; disjoint token sets score 0.0; score is invariant to the order of independent sentence pairs; brevity penalty ≤ 1 and applies only when the hypothesis is shorter |
| **Traps** | Smoothing method changes the number materially on short sentences — state which one. Corpus BLEU is not the mean of sentence BLEUs, and presenting one as the other is a real error |

### 2.2 Calibration

| | |
|---|---|
| **Library** | none — ECE and Brier are short and exactly specified |
| **Backend** | Compute Expected Calibration Error and Brier score from stored logprobs where a correctness label exists |
| **UI** | Revive `CalibrationPlot` — it exists and is imported by nothing — as a tab on the evaluation detail page, with the reliability diagram and the ECE beneath it |
| **Proof** | **Hand-computed**: a fixture of 10 predictions across 3 bins whose ECE is worked out by hand in the test's comment and asserted exactly. **Invariants**: a perfectly calibrated set has ECE 0; a maximally over-confident set has ECE 1; Brier is bounded in [0,1] and equals mean squared error against the label |
| **Traps** | Bin count changes ECE; state it. Confidence must come from the chosen token's probability, not from the top-1 probability, when those differ |

---

## Phase 3 — Make retrieval measurable

### 3.1 Retrieval metrics

| | |
|---|---|
| **Library** | none required — precision@k, recall@k, MRR and nDCG are exactly specified. `Ragas`-style faithfulness needs a judge, which Prism already has |
| **Backend** | Score a retrieval against a labelled set of relevant chunk ids |
| **UI** | RAG collection detail gains an Evaluate tab: pick a labelled query set, see the metrics per retrieval mode, so vector, BM25 and hybrid are compared on evidence rather than by eye |
| **Proof** | **Hand-computed** worked examples for each metric, including the ties and truncation cases nDCG gets wrong. **Invariants**: recall@k is non-decreasing in k; precision@k of a perfect ranking is 1 until relevant items are exhausted; nDCG of the ideal ranking is exactly 1 |
| **Traps** | Hybrid scores are normalised and blended, so they are not comparable to vector scores — the UI must not put them on one axis. The seeded collection has null embeddings, so a fixture must build its own |

---

## Phase 4 — Make the traces standard

### 4.1 OpenTelemetry GenAI semantic conventions

| | |
|---|---|
| **Library** | `OpenTelemetry.Api` — already a dependency |
| **Backend** | Emit `gen_ai.*` attributes on inference spans: system, request model, response model, token counts, temperature, top-p, finish reasons |
| **UI** | History detail shows the trace and span id for a call, so a row can be correlated with Jaeger, Langfuse or Phoenix. Copyable |
| **Proof** | An in-memory exporter captures spans in a test; every attribute name is asserted against the semantic-convention constant, not a literal string. A span for a failed call carries the error status |
| **Traps** | Prompt and completion content are opt-in in the convention and are sensitive — default them off and make that explicit |

### 4.2 Experiment export in an MLflow-compatible shape

| | |
|---|---|
| **Backend** | Extend the existing runs export with an `mlflow` format |
| **UI** | The existing export control gains the format; the CSV gap (no parameters, no tags, no custom metrics) is fixed at the same time |
| **Proof** | Round-trip through `mlflow`'s own importer in a notebook, asserting params and metrics survive |

---

## Explicitly out of scope

**Training.** Decided 2026-08-09 and unchanged. `mlx-lm`, `peft` and `unsloth` are what a
trainer would use; none of them is being added.

**Python in the backend.** The notebook is where Python belongs. If a metric genuinely cannot be
implemented in-process, that is an argument for exporting the data, not for a Python subprocess
in the API.

**A new charting library.** Recharts is present and adequate. `MetricChart` already refuses to
plot a missing value as zero, which is the behaviour that matters.

---

## Order, and why

1. **Phase 1** first and alone. Everything else is more valuable once the data can leave, and
   1.1 is the smallest item in the plan.
2. **Phase 2** next: the metrics that exist today produce numbers nobody can cite.
3. **Phase 3** after that: RAG has the most silent failure modes and the least measurement.
4. **Phase 4** last: valuable, but interoperability matters less than correctness.

Each phase ends with the affected `docs/features/<tab>.md` requirements tables updated, and
`docs/product-truth.yaml` counts refreshed.
