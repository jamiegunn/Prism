# Analytics

**Where your tokens went and how slow the slow requests were, across all of Prism.**

Sidebar: **Analytics**.

**There is not a single control on this page.** No date picker, no model filter, no module
filter, no refresh button, no export. You open it, you read it, you leave. Everything below
describes numbers you cannot adjust.

---

## Before you start

Nothing to configure, and nothing to switch on. Every inference call made anywhere in Prism —
Playground, Token Explorer, Prompt Lab, Agents, RAG, Structured Output, Evaluation, Batch
Inference — is recorded automatically as it happens. If you have used Prism at all, this page
has data.

It also means the page is empty on a fresh install. Nothing seeds it; the numbers only start
existing once you make a real call.

Two fixed windows you cannot change:

| | Value |
|---|---|
| Time range | The last 30 days, always |
| Grouping for the time series | By calendar day, UTC, always |

The API behind the page accepts `from`, `to`, `model`, `sourceModule` and `projectId`. The page
sends none of them. If you need a different window or a filter, call
`GET /api/v1/analytics/usage` and `GET /api/v1/analytics/performance` yourself.

---

## The four summary cards

Across the top, covering the whole 30-day window:

| Card | What it counts |
|---|---|
| **Total Requests** | Every recorded inference call. |
| **Total Tokens** | Prompt plus completion, summed. |
| **Avg Latency** | Mean end-to-end request duration in milliseconds. |
| **P95 Latency** | The duration that 95% of requests came in under. |

---

## Usage

The **Usage** tab has three sections.

**Requests Over Time** is a line chart of request count per day. Days with no traffic are absent
rather than plotted as zero, so a gap in usage shows up as a straight line between two points
rather than a dip to the floor. Read it for shape, not for precision.

**Usage by Model** is a bar chart of request count per model. This is the fastest answer to
"which model am I actually using", which is frequently not the one you think.

**Usage by Module** is a row of tiles, one per originating feature, with request count and token
total.

> **The module breakdown is not trustworthy.** Two problems.
>
> First, **Evaluation and Batch Inference do not label their traffic**, so everything those two
> features spend lands in a tile called `unknown`. On a machine where you have been running
> evaluations, `unknown` may well be your largest tile, and it tells you nothing about which
> feature it was.
>
> Second, one feature can appear as several tiles. Prompt Lab's A/B tests are labelled
> separately from its single tests, replayed calls from History are labelled separately again.
> The tiles are exactly the labels the calls carried, with no grouping.
>
> Use it as a rough sense of where traffic comes from. Do not use it to attribute spend.

---

## Performance

Four tiles: **P50 Latency**, **P95 Latency**, **P99 Latency**, **Avg Tokens/sec**.

Then a **Performance by Model** table: **Model**, **Requests**, **Avg Latency**, **P50**,
**P95**, **TTFT**, **Tok/s**.

### What the percentiles are actually telling you

The average latency is the least useful number on this page. A handful of very slow requests
drag it upward, so it describes neither the typical request nor the bad one.

**P50** is the median: half of your requests were faster than this. That is what a request
usually feels like.

**P95** is what a bad request feels like — one in twenty is at least this slow. If you are
deciding whether a model is fast enough to sit in front of a person, P95 is the number to look
at, because it is the experience they will remember.

**P99** is the tail. With only a few hundred requests recorded, P99 is computed from a handful
of samples and moves around a lot; treat it as indicative.

The relationship between them matters more than any one of them. **A P95 close to P50 means
your latency is consistent** — the model behaves the same way every time, and you can plan
around it. A P95 several times P50 means something intermittent is happening: queueing behind
other work, model swapping, or a subset of prompts that are far longer than the rest. That gap
is a thing to investigate, and it is invisible in the average.

### Two numbers to read carefully

**TTFT** (time to first token) and **Tok/s** in the per-model table **display `0ms` and `0.0`
when the value is unknown**, rather than a dash. An Ollama-only setup does not report
time-to-first-token, so every row shows `0ms`, which reads like an instant response and means
the opposite: no measurement exists. The `Avg Tokens/sec` tile above the table does show a dash
when unknown, so a dash there next to zeroes below is the signature of missing data rather than
fast responses.

---

## Cost

> **The Cost tab does not show your costs.** The backend computes a real per-call cost for every
> model it has pricing for, and stores it. The page does not read it.
>
> What the table actually does is check whether the model name contains the string `llama`,
> `mistral` or `qwen`. If it does, it prints `$0.00`. If it does not, it prints `—`. That is the
> entire calculation. A hosted `gpt-4o` shows `—` in the Est. Cost column even though a genuine
> dollar figure was computed and saved for every one of its calls.
>
> Read the Requests, Tokens and Avg Tokens/Req columns, which are real. Ignore the cost column.
> If you need the figure, `GET /api/v1/analytics/usage` returns `totalCost` overall and per
> model — and correctly returns `null` rather than zero for models it has no pricing for, so an
> unknown cost stays distinguishable from a free one.

The explanatory text on the tab also points at a cost estimator "in the Tokenizer Explorer",
which is where per-model pricing can be experimented with. See [Tokenizer](tokenizer.md).

---

## Why bother

The honest use of this page is two questions.

**Where are my tokens going?** The Usage by Model chart usually surprises people. A model you
added for one experiment and forgot about, still selected as your default, quietly serving most
of your traffic, is a common and easily fixed discovery.

**Does my latency have a tail?** The P50-to-P95 gap answers it in one glance, and no amount of
watching individual requests in the Playground will. A model that feels responsive because your
own hand-typed prompts are short can have a badly-behaved tail on the long ones, and this is
where that shows up.

Everything beyond those two questions — attribution by module, cost, anything outside 30 days —
is better served by the API than by this page.

---

## What this page will not do

- **No controls of any kind.** No date range, no filters, no drill-down.
- **Fixed 30-day window, fixed daily grouping.**
- **No auto-refresh.** Reload the page.
- **No export.** No CSV, no image, no copy button.
- **The Cost tab does not show real costs** — see above.
- **Per-model TTFT and Tok/s show `0` for unknown**, which is easy to misread as instant.
- **Evaluation and Batch Inference traffic is bucketed as `unknown`.**
- **No per-request drill-down.** Clicking a bar does nothing; use [History](history.md).

---

## See also

- [History](history.md) — the individual calls behind these aggregates
- [Model Management](models.md) — which providers report TTFT and throughput
- [Tokenizer](tokenizer.md) — the cost estimator with configurable pricing

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | History has seeded records, so Analytics has something to show | **No.** `HistorySeeder` writes `InferenceRecord` rows only; the sole writer of `UsageLog` is the runtime persistence service. So History shows three calls while Analytics reads zero | `HistorySeeder.cs:27-78`, `InferenceRecordPersistenceService.cs:116-118` |
| P2 | `0 ms` means nothing was recorded | **No — it looks measured.** An empty window returns literal zeros, so no traffic renders as instantaneous inference | `GetPerformanceHandler.cs:114-117` |
| P3 | Null and zero are consistently distinguished | **Only for cost.** Per-model rows coerce unknown TTFT and throughput to 0, while the same unknown reads "—" at summary level | `GetPerformanceHandler.cs:110` |
| P4 | The Cost tab's prose agrees with its Cost column | **No.** The captions say local models cost $0; local models have no pricing entry, so the column correctly reads "not priced". The number is honest and the surrounding text contradicts it | `CostCalculator.cs:15-25` |
| P5 | The window control bounds both ends | Half — only `from` is sent; the server defaults `to` to now | `analytics/api.ts` |
| P6 | These aggregates scale | Yes — computed in SQL with `GROUPING SETS` and `percentile_cont`, with a volume test | `GetPerformanceHandler.cs:49-68`; `AnalyticsAggregationTests` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | With no usage in the window, totals read zero | `An_Empty_Window_Returns_Zeros` | MET |
| R2 | One Playground call increases requests by exactly one and adds its tokens | `UsageLog_Projection_Carries_Tokens_And_Latency` | MET |
| R3 | Usage is attributed to the Prism feature that made each call | run one Playground and one Prompt Lab call; read the module boxes | MET |
| R4 | A model with no known pricing reads "not priced", never `$0.0000` | `UsageLog_Projection_Leaves_Unpriced_Models_Null_Not_Zero`; browser check | MET |
| R5 | Choosing a window re-queries with a `from` parameter | browser check: 7 days issues a new request | MET |
| R6 | An empty window does not assert a measured latency | none — the cards read `0ms` | **UNMET** |
| R7 | Per-model TTFT that was never measured reads "—", not `0ms` | none — coerced at the handler | **UNMET** |
| R8 | The Cost tab's prose agrees with its column | none — see P4 | **UNMET** |
| R9 | A failed request says so rather than showing zeros | none — the page has no error branch | **UNMET** |
