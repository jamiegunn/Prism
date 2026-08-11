# Experiments

**Group runs under a written hypothesis, sweep parameters, and export the results.**

Experiments is where a question you are asking a model turns into something you can put in a
paper. A project holds experiments, an experiment holds runs, and an experiment carries a
hypothesis you wrote down before you had any results. The mechanism that produces runs is a
parameter sweep: one prompt, several parameter values, one run per combination.

Sidebar: **Experiments**. Three pages — the project list, a project's detail page, and an
experiment's detail page.

---

## Before you start

You need a registered model instance before you can create any runs. See
[Model Management](models.md).

Two things to understand up front. **A parameter sweep is currently the only way to create a run
from the UI**, so an experiment without a sweep is an empty container. And **a sweep runs
synchronously**: the browser sits on the request until every combination has finished. There is
no progress bar, no cancel and no partial result. Size your sweeps accordingly — the section on
sweeps has the numbers.

---

## Projects

The landing page lists projects as cards. Each shows the name, the description, the number of
experiments, the creation date, and an **Archived** badge where relevant.

**Search projects...** filters by name. The **Show archived** checkbox is off by default and its
setting persists in your browser, so archived projects are hidden until you ask for them.

**New Project** opens **Create Research Project**: a **Name** (required, up to 200 characters)
and a **Description** (optional, up to 2000). That is the whole model. Projects exist to group
experiments and nothing else.

Inside a project you get **Archive** and **New Experiment**. Archiving takes effect immediately
and returns you to the project list, where the project is now hidden unless **Show archived** is
ticked. There is no un-archive.

---

## Experiments

Experiment cards inside a project show the name, a status badge, the description, the hypothesis
and the run count.

| Status | Badge | What it means |
|---|---|---|
| Active | green | Accepting runs. **Sweep** and **Complete** are available. |
| Completed | blue | You have declared it finished. The **Sweep** button disappears, so no further runs can be added from the UI. |
| Archived | grey | Filed away. No status buttons remain. |

**New Experiment** opens **Create Experiment**:

| Field | Required | Limit |
|---|---|---|
| **Name** | yes | 200 characters |
| **Description** | no | 2000 characters |
| **Hypothesis** | no | 2000 characters |

### Write the hypothesis first

The Hypothesis field is optional in the form and load-bearing in practice. Fill it in before you
run anything.

The reason is that a parameter sweep will always produce differences. Four temperatures against
one prompt will give you four different outputs with four different latencies and four different
token counts, and if you go looking for a story in them afterwards you will find one. Writing
"outputs below T=0.3 will be more consistent on the extraction task" before you press Run turns
the sweep into a test of a claim rather than a search for one. It also makes the difference
visible when the sweep comes back and the answer is no — which is the result you most want to
have recorded honestly, and the one that is easiest to quietly rewrite in your head.

It shows on the experiment card and on the detail page in italics, and it goes into the JSON
export. One sentence is enough.

---

## The experiment detail page

The header carries the name, the status badge, the description, the hypothesis, and the action
buttons:

| Button | When it appears | What it does |
|---|---|---|
| **Complete** | Active only | Marks the experiment Completed. This removes the **Sweep** button. |
| **Sweep** | Active only | Opens the Parameter Sweep dialog. |
| **Archive** | Any status except Archived | Files the experiment. No way back. |
| **CSV** | always | Downloads every run in the experiment as CSV. |
| **JSON** | always | Downloads every run in the experiment as JSON. |

Below the header sits the stats strip, then the run table on the left with a detail or comparison
panel on the right.

---

## Running a parameter sweep

**Sweep** opens the **Parameter Sweep** dialog. It builds the cartesian product of the parameter
values you list and executes one run for each.

| Field | Required | Default |
|---|---|---|
| **Instance** | yes | none selected |
| **Prompt** | yes | empty |
| **System Prompt (optional)** | no | empty |
| **Temperature** | — | pre-populated with `0`, `0.3`, `0.7`, `1.0` |
| **Top-P (optional)** | no | empty, which the server reads as `[1.0]` |
| **Max Tokens (optional)** | no | empty, which the server reads as `[2048]` |
| **Capture logprobs** | no | off; when on, records the top 5 alternatives per token |

Each of the three value lists works the same way: type a number, press Enter or click **+**, and
it appears as a chip. Values are de-duplicated and sorted. The X on a chip removes it. You can
strip the temperature list back to a single value if temperature is not what you are varying.

The footer keeps a live count — "N parameter combinations will be executed sequentially." — and
the button reads **Run Sweep (N)**. When it finishes you get a completed/total summary and a
failure count if any combination errored.

Runs are named for their position and temperature — with the default temperature list that is
`Sweep #1 (T=0.00)`, `Sweep #2 (T=0.30)`, `Sweep #3 (T=0.70)`, `Sweep #4 (T=1.00)` — and are
tagged `sweep` plus a batch tag of the form `sweep-batch:20260809142317`.

> **Keep the combination count modest.** Every combination is a full, non-streaming inference
> call, and all of them happen inside the single HTTP request that the dialog opened. Nothing is
> written to the database until the last one returns. In practice this means:
>
> - No progress. The button spins and that is all the feedback there is.
> - No cancel. Closing the dialog or the tab does not stop the server.
> - No partial results. If the request times out, **you lose every run in the sweep**, including
>   the ones that had already completed.
>
> Four temperatures against a local 7B model is comfortable. Four temperatures times three
> top-p values times three max-token values is 36 sequential generations in one request, and
> that will hit the timeout. Split large grids into several smaller sweeps.

> **The batch tag is stamped per run, at the moment that run is created.** A sweep that takes
> more than a second — which is all of them — will produce several different `sweep-batch:`
> timestamps within the same sweep. Treat the tag as "roughly when", not as a reliable batch
> identifier, and use the run names and creation times to reconstruct a sweep. Tags are visible
> in the run detail panel and in the JSON export.

---

## Reading the results

### The stats strip

Above the run table, and computed **only over runs with status Completed**:

| Card | Notes |
|---|---|
| **RUNS** | Completed out of total, with a failure count underneath when any run failed. |
| **AVG LATENCY** | Mean, with the min–max range underneath. |
| **AVG THROUGHPUT** | Mean tokens per second, with the range. Only appears when at least one run recorded throughput. |
| **TOTAL COST** | Only rendered when the total is above zero, which — see the limitations — never happens. |

Underneath, when it renders, is a **Metric / Mean / Median / Std Dev / Min / Max** table with a
row per custom metric plus built-in rows for `perplexity` and `total_tokens`.

> **The metric table only renders if at least one run carries a custom metric.** The perplexity
> and total_tokens rows live inside that same block, so if none of your runs has a custom metric
> the whole table is hidden — including statistics it could perfectly well have computed from
> data that is right there. Sweep-created runs never have custom metrics, so for a sweep this
> table is always absent. Export to JSON and compute from that.

> **The stats strip reads the 100 most recently created runs only.** The run table underneath
> pages through everything; the summary above it does not. On an experiment with more than 100
> runs the averages are over a recent subset, not the whole experiment, and nothing on screen
> says so.

### The run table

Columns: a selection checkbox, **Name**, **Model**, **Status**, **Latency**, **Tokens**,
**Cost**, **Created**, and a trash icon. **Model**, **Latency**, **Tokens**, **Cost** and
**Created** are sortable — click the header to sort, click again to reverse. Sorting runs on the
server across all runs, not only the page you are looking at. Default is newest first, 50 rows
per page, and your sort choice persists in the browser.

Clicking a row opens the run detail panel on the right: parameters, metrics, tags, the input and
the output.

### Comparing runs

Tick two or more checkboxes and a bar appears with a **Compare** button. The comparison panel
replaces the detail panel and has two parts.

**Parameter Differences** is a table with one row per parameter and one column per run — but only
rows where the runs actually differ. Comparing four runs from a temperature sweep gives you a
one-row table showing the temperatures, which is exactly what you want; comparing two identical
runs gives you no table at all.

Below it, a bar chart per metric: latency, prompt tokens, completion tokens, total tokens,
tokens per second, perplexity, cost, plus any custom metrics. Metrics where every run is empty
are skipped.

---

## Export

Both buttons download the entire experiment's runs, ignoring the current page and any sorting.

**CSV** has this header:

```
Id,Name,Model,Status,PromptTokens,CompletionTokens,TotalTokens,LatencyMs,TtftMs,TokensPerSecond,Perplexity,Cost,FinishReason,CreatedAt
```

Look at what is missing: **the input, the output, the parameters, the tags and the custom
metrics**. A CSV export of a temperature sweep does not contain the temperatures. It is a
performance summary, useful for a latency plot and useless for reproducing anything.

**JSON** contains all of it — input, output, system prompt, the full parameter object, metrics,
tags, error text and every numeric field the CSV has.

**Use JSON if you intend to reproduce, publish or archive.** Use CSV only when you want to paste
timings into a spreadsheet and you still have the JSON somewhere.

---

## Why this matters for research

A hypothesis written before the run, a parameter sweep that varies one thing at a time, and a
JSON export that contains the prompt and the parameters alongside the numbers: that is the
smallest artefact you can hand someone that lets them argue with you properly. They can see what
you expected, what you varied, what came back, and what settings produced it.

Take the ordering seriously. The hypothesis field is worth nothing if you fill it in after
reading the results — at that point it is a caption, not a prediction. Write it when you create
the experiment, leave it alone, and let it be wrong sometimes.

---

## What this page will not do

- **The Cost column is always empty.** Nothing in Prism computes a cost for a run. The field
  exists, the column exists, the sort works, the export writes it out, and it is null on every
  run the application creates. The **TOTAL COST** stat card is hidden for the same reason.
- **Perplexity is only present on the seeded demo runs.** Sweep runs do not record it, even with
  **Capture logprobs** on.
- **Deleting a run reports failure and succeeds anyway.** You get a red "Delete failed" toast,
  the row stays on screen, and the run is gone from the database. Refresh the page to confirm.
  This affects the run table only.
- **No rename or edit** of a project or an experiment. Both are supported by the API and neither
  has a button. A typo in an experiment name is permanent.
- **No un-archive**, for projects or experiments.
- **No way to create a single run from the UI.** Sweep with one temperature value is the closest
  you can get. The run table's empty state suggests creating runs from the Prompt Lab; that path
  is not wired up — see [Prompt Lab](prompt-lab.md).
- **No tag filter.** Runs are tagged and the tags are visible, but nothing in the UI filters or
  searches by them.
- **No sweep history.** Once the dialog is closed the summary is gone; the runs are the only
  record that a sweep happened.

---

## See also

- [Prompt Lab](prompt-lab.md) — versioned prompts to sweep against
- [Datasets](datasets.md) — seeded, recorded train/test/val splits
- [Model Management](models.md) — registering the instances a sweep runs on

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | The run table's own advice, "Create runs from the Prompt Lab or API", is accurate | **No.** Prompt Lab never sends `saveAsRunExperimentId`, so Sweep is the only UI path that creates runs | `RunTable.tsx:152`; `TestPanel.tsx:101-110` |
| P2 | A sweep's completed runs survive an interruption | **No.** Runs are held in a list and saved only after the last combination returns, so a dropped request loses even the ones that succeeded | `RunSweepHandler.cs:152` |
| P3 | "Capture logprobs" produces something readable here | **No.** It writes a token trace to History but sets neither `LogprobsData` nor `Perplexity` on the run, so the Perplexity card stays empty | `RunSweepHandler.cs:93-133` |
| P4 | The Cost column measures something | **No.** `Run.Cost` is only ever set by an endpoint nothing calls; every row reads `-` | `CreateRunHandler.cs:57` |
| P5 | Run selection is scoped to the experiment you are viewing | **No.** It lives in a global store and is not cleared on navigation, so the banner follows you and Compare then errors | `experiments/store.ts:34` |
| P6 | CSV export contains what the page shows | **Yes** — the CSV now carries `param.*` columns, `metric.*` columns (the union across runs; a run missing a metric leaves an empty cell, never 0), tags, input, output, system prompt and error | `ExportRunsHandler.ExportCsv` |
| P7 | Archiving is reversible | **No.** The endpoint accepts Active; the UI only ever sends Completed or Archived | `ExperimentDetailPage.tsx:86-118` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | A project and an experiment with a hypothesis can be created without leaving the UI | click-path from `/experiments` | MET |
| R2 | The hypothesis is shown on the experiment it belongs to | open a seeded experiment | MET |
| R3 | A sweep produces one recorded run per combination, named and tagged | run a 4-value temperature sweep | MET |
| R4 | The sweep states the number of generations, and that they are sequential, before you commit | open the dialog and read the footer | MET |
| R5 | Comparing runs shows only the parameters that differed | tick two runs, Compare | MET |
| R6 | A metric that was not measured is drawn as absent, not as zero | compare a seeded run with a sweep run on perplexity | MET |
| R7 | JSON export contains input, output, parameters, tags and metrics | `GET .../runs/export?format=json` | MET |
| R8 | A bad experiment or project id says not found rather than loading forever | navigate to a zero GUID | MET |
| R9 | Deleting a run removes its row and reports success | fixed by the 204 handling in `apiClient` | MET |
| R10 | An interrupted sweep keeps the runs that had already completed | none — see P2 | **UNMET** |
| R11 | Capturing logprobs makes a perplexity figure visible on the run | none — see P3 | **UNMET** |
| R12 | CSV export contains the parameter that was swept | a sweep's temperatures appear as the `param.temperature` column | MET |
| R13 | Runs export in an MLflow-compatible shape and survive MLflow's own importer | `format=mlflow` emits `Run.to_dictionary()`-shaped documents; an executed notebook replays them through `MlflowClient` into a real tracking store (mlflow 3.15.1) and reads them back, asserting params and metrics survive and an absent metric stays absent | MET |
| R14 | Omitting `?format=` returns the JSON default rather than a 400 | the binder parameter is nullable now; the `?? "json"` default is reachable | MET |
| R13 | Selecting runs in one experiment leaves no selection banner on another | none — see P5 | **UNMET** |
| R14 | An archived experiment can be returned to Active | none — the endpoint supports it, the UI never sends it | **UNMET** |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | A run carries its own token trace | The trace is already recorded against the History record for the same call; duplicating it on the run is the thing to decide against | this review |
