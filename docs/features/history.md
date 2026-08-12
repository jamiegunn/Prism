# History

**Every inference call Prism has ever made, with the exact request that produced it.**

You do not have to switch this on and you cannot opt a module out of it. Every inference call
made anywhere in Prism is written here as it happens, with the full request JSON, the full
response JSON, timings, token counts and a snapshot of the environment it ran in.

What is uneven is *attribution* — not every module labels its traffic, which matters the moment
you try to filter. See [Finding a specific module's calls](#finding-a-specific-modules-calls).

Sidebar: **History**. Page heading **Inference History**.

---

## Before you start

Nothing to configure. If you have made an inference call, there is a record.

Two fields will be blank for some providers: **Perplexity** and **TTFT** are derived from
per-token probabilities, so they are empty for anything that does not return logprobs — Ollama,
most notably. Everything else is recorded regardless. See [Model Management](models.md).

---

## Find a run

The filter bar across the top has seven controls: **Search**, **Source**, **Model**, **From**,
**To**, **Status** and **Tag**, with **Apply** and **Reset** at the end.

**Search** is free text across the prompt, the response, the source module name, the model name
and the request parameters — everything is matched against the stored JSON, so a temperature
value or a stop sequence is as findable as a phrase from the prompt.

**Source** is a dropdown: All Sources, Playground, Token Explorer, Prompt Lab, Experiments,
Batch Inference, RAG, Agents.

**Status** is All, Success or Failed. **From** and **To** are dates. **Tag** matches a single
tag.

Three things about this bar will waste your time if nobody tells you:

> **Apply only applies the search box.** Every other filter takes effect the instant you change
> it. Change Source and the table reloads immediately; type in Search and nothing happens until
> you press **Apply** or hit Enter.

> **Model is an exact match, despite the placeholder reading "Filter model…".** Typing `llama`
> returns nothing at all against `meta-llama/Llama-3.1-8B-Instruct`. You must paste the full
> model ID, character for character. To search loosely by model, put the fragment in the
> **Search** box instead — that one does substring matching, and the model name is part of what
> it searches.

> **The To date excludes the final day.** It binds to midnight at the start of the date you
> pick, so setting **To** to today returns nothing from today. Set **To** to tomorrow to include
> today.

**Reset** clears every filter and the search box together.

### Finding a specific module's calls

The **Source** dropdown offers eight fixed options and matches them exactly against the label a
module stamped on the call. Several modules do not stamp the label the dropdown expects, so
those options return nothing:

| You pick | What you get |
|---|---|
| Playground, Token Explorer, RAG, Agents | Works. |
| **Batch Inference** | **Nothing, ever.** Batch calls are recorded without a module label and land under `unknown`. |
| **Experiments** | **Nothing.** Parameter sweeps — the only way to create runs — label themselves `experiments-sweep`. |
| **Prompt Lab** | Quick tests work. A/B tests label themselves `prompt-lab-ab-test` and are missed. |

`history-replay`, `structured-output` and `evaluation-judge` do have options — **Replays**,
**Structured Output** and **Evaluation (judge calls)** — and they return rows.

Until this is tidied up, **use the Search box rather than the Source dropdown** when you are
looking for a particular module. Search matches the module name as a substring, so `experiments`
finds `experiments-sweep` and `batch` finds nothing — for batch runs, search the model name or a
phrase from the prompt instead.

On a fresh development database the three seeded demo records are labelled `Playground` and
`TokenExplorer` with capitals, which match neither the badge colours nor the filter, so they show
a grey badge and never appear under any Source selection.

---

## Read the table

| Column | Notes |
|---|---|
| **Time** | Relative — "4m ago", "3d ago". Hover for the full timestamp. |
| **Source** | Colour-coded badge naming the module that made the call. Unlabelled calls read `unknown`; labels the colour map does not know render grey. |
| **Model** | The full model ID, truncated to fit; hover for all of it. |
| **Prompt** | The first user message, cut at 60 characters. |
| **Tokens** | Prompt / completion. |
| **Latency** | Total call time in milliseconds. |
| **Status** | A green tick or a red cross. |
| **Tags** | Up to three, then a `+N`. **Clicking a tag filters the table by it** — the fastest navigation on this page. |

Newest first, 20 rows per page, and the page size is not adjustable. Click any row to open it.

---

## Inspect a single call

The detail sheet slides in from the right.

Across the top, six metric cards: **Prompt Tokens**, **Completion Tokens**, **Total Tokens**,
**Latency**, **TTFT** and **Perplexity**. The last two read `--` where the provider did not
supply the data to compute them.

Below that, an **Environment** block naming the provider, the model and the source module, with
the full environment snapshot behind an expandable *Full environment JSON*. This is the part
that answers "what was this actually running on".

Then **Request**, **Response** and **Tokens** tabs. The first two hold the complete JSON for
each side of the call, with a copy button on both — the request JSON is the authoritative
record of the call: every parameter as it was actually sent, not as the UI displayed it. The
**Tokens** tab renders the recorded per-token trace: the same heatmap / entropy / surprise
views the Playground uses, over the token-by-token logprobs, entropies and alternatives the
recording spine stored, with a stats line underneath (token count, mean entropy, surprise
count with its defining threshold, trace schema version). A record with no trace says why —
distinguishing "the call failed before a response existed" from "no logprobs were recorded".

The **Tags** editor sits under that. Type and press Enter to add, click the × on a badge to
remove. Tags are lowercased and duplicates are dropped, and every change saves immediately —
there is no save button and no undo.

At the bottom: **Replay**, **Copy Request**, **Copy Response**.

---

## Replay a call

**Replay** re-runs the stored request against an instance you choose.

Pick a **Target Instance**, optionally open **Parameter Overrides** to change **Temperature**,
**Top-P**, **Max Tokens** or **Model**, and click **Replay**. Anything you do not override is
taken from the original request exactly as recorded.

The comparison then shows original and replay responses side by side with word-level differences
highlighted, a diff summary line, and a metrics table comparing model, latency, prompt tokens
and completion tokens with deltas. A deterministic run replayed against the same model comes
back **Responses are identical** — that is the check worth doing first when you suspect an
instance has drifted.

> **Only instances currently marked Online appear in the dropdown.** Health checks run every 30
> seconds, so after restarting the backend the list can be empty for up to half a minute even
> though everything is running. Wait and reopen the dialog.

Three things to keep in mind:

**The replay runs the model the record names.** Resolution order is your explicit override, then
the model on the original request, then the target instance's own model if the record names none.
Replaying a Mistral run against an instance serving Llama therefore asks that instance for
Mistral. That is deliberate: silently substituting the instance's model would make the two
responses differ for a reason the comparison never states.

When the instance you choose does not have the record's model — which is what happens to the
seeded demo rows, whose models are not on anyone's machine — the dialog says so **before** you
run anything, and the **Model** override lists what that instance actually serves. Choosing one
is the whole fix; the amber **changed** badge in the metrics table marks that you did.

**Overrides are range-checked.** Temperature 0–2, Top-P 0–1, Max Tokens 1 or more. Out-of-range
values are rejected with a message rather than passed to the inference server, which used to
either ignore them silently or fail in a way the page reported as a server fault.

**Replays are recorded under the source `history-replay`**, which is the **Replays** option in
the **Source** dropdown. The new row appears in the table a moment after the call returns —
records are written on a background channel, not inside the request.

---

## Why this page is the one you will be glad about

Everything else in Prism is for the experiment you are running now. History is for the one you
ran three weeks ago and now have to defend.

"Which model produced this output" and "what temperature was I using" are answerable here and
essentially nowhere else, because the stored request JSON is what was actually sent — not the
parameters the UI was showing, not what you meant to set. When a result stops reproducing, the
first useful move is to open the original record and diff its request JSON against your current
settings.

Tagging is the cheap part of this and the part people skip. A run you tag `baseline-v2` or
`ablation-no-rag` while you are running it takes two seconds and is findable forever. The same
run untagged is a row among thousands, identified only by a timestamp you will not remember and
a 60-character prompt preview shared with fifty near-identical attempts. Tag as you go; the
retrospective version of this task is searching response text for a phrase you half remember.

---

## Export

The Export control sits beside the filter bar and writes **exactly what the current filters
select** — not everything. The button states the row count before anything is written
("Export 41"), and is disabled when nothing matches, rather than being clickable and doing
nothing.

Three formats, all streamed from the database rather than assembled in memory:

| Format | Notes |
|---|---|
| **JSONL** | One record per line, camelCase, full request/response JSON included. A metric that was not measured is `null` — never 0. |
| **CSV** | RFC-4180. Non-null strings are always quoted, so a null field (completely empty) is distinguishable from an empty string (`""`). |
| **Parquet** | Snappy-compressed, 2000-row groups, schema mirroring the record field-for-field with nullability. Timestamps at millisecond precision. Tags travel as a JSON-encoded string column. |

Every row carries the full scalar record: request/response JSON, token counts, timings,
perplexity/entropy/surprise where measured, cost, tags, and the `traceId`/`spanId` of the
span that covered the call. The same endpoint drives the notebook client's
`workbench.export_history()` and `workbench.history_dataframe()`.

The wire is `GET /api/v1/history/export?format=jsonl|csv|parquet` plus every filter the
search endpoint accepts; the row count is returned in `X-Export-Row-Count`.

---

## What this page will not do

- **Nothing deletes.** No record deletion, no bulk selection, no retention policy, no purge.
  History grows without limit and there is no UI to trim it.
- ~~**Per-token traces are recorded but not viewable.**~~ No longer true: the detail sheet's
  **Tokens** tab displays the recorded trace (`GET /history/{id}/trace`).
- **Perplexity and TTFT are blank for providers without logprobs.** Not zero — blank. This is
  correct behaviour, not a fault.
- ~~**The replay comparison is broken.**~~ No longer true: the comparison renders, the diff is
  aligned by longest common subsequence rather than by word position, and out-of-range overrides
  are refused before the call.
- **Several sources cannot be filtered for** — see the table above. Replays are no longer among
  them.
- **Page size is fixed at 20 rows.**
- **You cannot edit anything but tags.**

---

## See also

- [Playground](playground.md) — where most records come from
- [Token Explorer](token-explorer.md) — predictions, steps and branches all land here too
- [Model Management](models.md) — which providers populate perplexity and TTFT

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | Every inference from every module is recorded | **Yes** — recording wraps the single point where providers are constructed, so no feature can bypass it | `InferenceProviderFactory.cs:73-85` |
| P2 | Every record carries the label its Source filter uses | **Yes** since the research-capabilities change: batch and evaluation label their requests, and the judge labels its own calls `evaluation-judge` | `BatchJobHandler.cs`, `EvaluationJobHandler.cs`, `LlmJudgeScorer.cs` |
| P3 | The Source dropdown's options correspond to labels that exist | **Yes** — the option list was rebuilt from the labels writers actually produce (incl. `experiments-sweep`, `evaluation`, `evaluation-judge`, `structured-output`, `history-replay`) | `history/utils.ts` |
| P4 | The seeded rows are reachable through the filters | **Yes** — the seeder now writes the lowercase labels the exact-match filter uses | `HistorySeeder.cs` |
| P5 | The model filter matches loosely | **No** — exact equality. `mistral` returns nothing; `mistral:7b-instruct` returns rows | `SearchHistoryHandler.cs:87` |
| P6 | Perplexity and TTFT are recorded for every call | **No** — only when the response carried logprobs | `InferenceRecordPersistenceService.cs:97-107` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | Unfiltered, the table lists recent calls newest-first with working pagination | `GET /history?page=2&pageSize=20` returns page 2 of 3 | MET |
| R2 | A call made anywhere appears without having been marked as worth keeping | send a Playground message, reload | MET |
| R3 | Search matches prompt and response text | `GET /history?search=capital` returns rows | MET |
| R4 | Setting a date filters the table rather than erroring | `GET /history?from=2026-08-01` → 200 with 48 rows (was 500 — `DateTimeKind.Unspecified` against a timestamptz column) | MET |
| R5 | Copy Request and Copy Response place the stored JSON on the clipboard | click either, paste | MET |
| R6 | Reset returns to the unfiltered first page | set filters, Reset | MET |
| R7 | Adding a tag reports success when the server stored it | fixed by the 204 handling in `apiClient` | MET |
| R8 | Clicking a tag badge filters by that tag | `Tags` is now a native `text[]`, so the predicate translates (`@tag = ANY`); migration converts existing jsonb data in place. `HistoryExportTests.Export_Selects_Exactly_What_Search_Selects_Including_Tag_Filter`; clicked in the browser | MET |
| R9 | Every Source option returns rows when that module has run | batch and evaluation now label their calls (`batch-inference`, `evaluation`), the seeder writes lowercase labels the filter matches, and the dropdown lists exactly the labels writers produce (`utils.ts` `SOURCE_MODULES`) | MET |
| R10 | A failed call's error message is visible | the Response tab renders the stored `errorMessage` in a failure panel when `responseJson` is null; Copy Response disables with a reason | MET |
| R11 | Export selects exactly what the filters select, streams, and preserves null | round-trip tests for JSONL/CSV/Parquet assert every scalar field and that null survives as null (`HistoryExportTests`, 7 tests incl. multi-row-group batching); wire shows `format=` and the active filters with no page params | MET |
| R12 | The export control states the row count before writing and cannot fire on nothing | button reads "Export N" from the live count and is disabled at 0 with the reason in its tooltip (`ExportControl.test.tsx`, 5 tests, mutation-checked); browser: download + toast on the working path, disabled state on the empty path | MET |
| R13 | A history row can be correlated with a standard tracing tool | every inference span carries `gen_ai.*` attributes and its `traceId`/`spanId` are persisted and shown, copyable, in the detail panel (`GenAiTelemetryTests`, 5 tests); verified in the browser | MET |
| R14 | A recorded per-token trace is viewable where the record is | the detail sheet's Tokens tab renders the trace through the same logprobs panel the Playground uses; events return in position order with parsed alternatives, absent traces state why, missing records 404 (`HistoryTraceTests`, 2 tests, 3 mutations observed red); browser-verified working and failed-call paths, console clean | MET |
| R15 | Replay shows the original and the replay side by side | the result carries both responses as text, so the comparison renders; the diff aligns by longest common subsequence, so an insertion no longer marks every word after it (`diff.test.ts`, 7 tests; `HistoryReplayTests`, 14 tests, 2 mutations observed red); browser-verified identical, diverging, failed-original and long-response cases, console clean | MET |
| R16 | A replay runs the request that was recorded | the model comes from the record unless overridden, out-of-range overrides are refused before any provider is contacted, and a record with no messages is refused rather than sent (`HistoryReplayTests`) | MET |
| R17 | A failed replay says what to do about it | the error names the model and the instance, and the client shows a 503's detail rather than "check the API log" (`mutationErrors.test.ts`); browser-verified against a model the instance does not serve | MET |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | Replays are themselves recorded as comparable runs | `ReplayRun` and its table exist with no writer, and `IReplayService` is a second replay implementation nothing injects. Both are named in ADR 013 as the runtime layer's designed home for replay, so removing them is an architecture decision rather than a fix, and they stay. What was fixed is the disagreement: `ReplayService` always took the model from the record, the live handler took it from the target instance, and the live handler now matches. A replay is still findable only as its own `history-replay` record — nothing links it to the original | this review |
