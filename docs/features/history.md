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

Three more labels exist that the dropdown has no option for at all: `history-replay`,
`structured-output` and `evaluation-judge`.

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

Then **Request** and **Response** tabs holding the complete JSON for each, with a copy button on
both. The request JSON is the authoritative record of the call: every parameter as it was
actually sent, not as the UI displayed it.

The **Tags** editor sits under that. Type and press Enter to add, click the × on a badge to
remove. Tags are lowercased and duplicates are dropped, and every change saves immediately —
there is no save button and no undo.

At the bottom: **Replay**, **Copy Request**, **Copy Response**.

---

## Replay a call

**Replay** re-runs the stored request against an instance you choose.

Pick a **Target Instance**, optionally open **Parameter Overrides** to change **Temperature**,
**Top-P** or **Max Tokens**, and click **Replay**. Anything you do not override is taken from
the original request exactly as recorded.

> **Only instances currently marked Online appear in the dropdown.** Health checks run every 30
> seconds, so after restarting the backend the list can be empty for up to half a minute even
> though everything is running. Wait and reopen the dialog.

> **Known bug: the comparison view does not render.** The replay itself works — the call is
> made, the response is generated, the record is written to history. What fails is the
> side-by-side comparison screen that should appear afterwards, because the API returns the
> original record as a structured object where the page expects a plain string. Expect the
> dialog to break at that point. To see the result, close it and find the new record in the
> table.

When it is fixed, the comparison shows original and replay responses side by side with
word-level differences highlighted, a diff summary line, and a metrics table comparing model,
latency, prompt tokens and completion tokens with deltas.

Two things about replay to keep in mind regardless:

**Replaying against a different instance silently changes the model.** The model is resolved in
this order: your explicit override, then the target instance's own model, then the model from
the original request. So replaying a Llama run against a Mistral instance runs Mistral. The only
signal is a small amber **changed** badge in the comparison table — the one that currently does
not render.

**Replays are recorded under the source `history-replay`.** That value is not one of the options
in the **Source** dropdown, so you cannot filter for your replays. Type `history-replay` into
the **Search** box to find them.

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

## What this page will not do

- **Nothing exports.** No CSV, no JSON download, no report. Copy Request and Copy Response put
  one record on the clipboard at a time, and that is the entire escape route for data you may
  well want in a paper.
- **Nothing deletes.** No record deletion, no bulk selection, no retention policy, no purge.
  History grows without limit and there is no UI to trim it.
- **Failed calls show no error.** A red cross in the Status column and an empty response body.
  The message is captured *and returned by the API* — the page simply never renders it. So the
  reason is one `curl` away (`/api/v1/history/{id}`, field `errorMessage`) even though no screen
  will show it to you.
- **Per-token traces are recorded but not viewable.** Calls that returned logprobs get a full
  token-by-token trace written to the database. No screen in the application displays it. To
  read one you go to the database directly.
- **Perplexity and TTFT are blank for providers without logprobs.** Not zero — blank. This is
  correct behaviour, not a fault.
- **The replay comparison is broken** — see above.
- **Several sources cannot be filtered for**, replays among them — see the table above.
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
| P2 | Every record carries the label its Source filter uses | **No.** Batch and Evaluation set no `SourceModule` and persist as `unknown`; sweeps persist as `experiments-sweep` | `BatchJobHandler.cs:232`, `EvaluationJobHandler.cs:242`, `RunSweepHandler.cs:96` |
| P3 | The Source dropdown's options correspond to labels that exist | **No.** Measured live: `playground` 11, `token-explorer` 32, `unknown` 12; `experiments`, `batch-inference`, `prompt-lab`, `rag` and `agents` all zero. Labels that are written have no option and vice versa | `history/utils.ts:48-57` |
| P4 | The seeded rows are reachable through the filters | **No.** The seeder writes `Playground` and `TokenExplorer` capitalised; the filter is exact equality | `HistorySeeder.cs:42`, `SearchHistoryHandler.cs:82` |
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
| R8 | Clicking a tag badge filters by that tag | none — `Tags.Contains` on a jsonb-converted list is untranslatable and returns 500 | **UNMET** |
| R9 | Every Source option returns rows when that module has run | none — see P2/P3 | **UNMET** |
| R10 | A failed call's error message is visible | none — stored and returned, rendered nowhere | **UNMET** |
| R11 | Replay shows the original and the replay side by side | none — the DTO returns an object where the client's type declares a string, so the diff call fails | **UNMET** |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | Replays are themselves recorded as comparable runs | `ReplayRun` and its table exist with no writer, and `IReplayService` is a second replay implementation nothing injects. Two paths that disagree is worse than one | this review |
