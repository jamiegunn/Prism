# Evaluation Suite

**Score the same dataset across several models, with a scoring method you wrote down.**

The Evaluation Suite turns "this model feels better" into a number you can put in a table. You
point it at a dataset, name the models you want compared, name the metrics you want computed,
and it runs every record through every model and averages the scores.

Sidebar: **Evaluation**. Two tabs: **Evaluations** and **Leaderboard**.

---

## There is no "New Evaluation" button

Read this before anything else. The Evaluations tab lists runs, searches them, and opens them.
It does not create them. There is no button, no dialog, no menu item anywhere in Prism that
starts an evaluation. The only way in is the HTTP API.

```bash
curl -X POST http://localhost:5000/api/v1/evaluation \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Summarisation - 8B vs 70B",
    "datasetId": "3f1c2a10-8b4e-4f6a-9d21-7c5e0b9a1234",
    "splitLabel": "test",
    "projectId": null,
    "models": ["llama3.1:8b", "llama3.1:70b"],
    "promptVersionId": null,
    "scoringMethods": ["exact_match", "rouge_l", "length_ratio"],
    "config": null
  }'
```

Send every field, including the nulls. The request body is deserialised straight into a record
with no defaults, so an omitted `models` or `scoringMethods` produces a server error rather than
a helpful message.

`datasetId` comes from `GET /api/v1/datasets`. `splitLabel` is optional — pass `null` to
evaluate the whole dataset, or a label such as `"test"` to restrict it to one split.

A successful call returns `201` with the new evaluation, which then appears on the Evaluations
tab within a few seconds. Names are not unique and nothing stops you creating the same run
twice.

---

## Before you start

You need two things.

**A dataset with records.** See [Datasets](datasets.md). If the dataset or split you name has
no records, the API refuses the request with a validation error rather than creating an empty
run.

**At least one registered inference instance.** See [Model Management](models.md).

The second one has a catch worth understanding. **The runner does not let you choose which
instance to use — it takes the first one it finds, with no ordering, and sends every request
there.** The `models` you list are model *names* passed through to that one instance. If you
have three instances registered and only one of them serves `llama3.1:70b`, the run has roughly
a one-in-three chance of working, and which way it goes is not something you control. With
several instances registered, make sure every model name you list can be served by every one of
them.

---

## Name your dataset columns so they are found

Dataset records are schemaless JSON, so the runner guesses which field is the prompt and which
is the reference answer. It takes the first non-empty value it finds, in this order:

| Role | Field names tried, in order |
|---|---|
| Prompt sent to the model | `input`, `prompt`, `question`, `instruction`, `text` |
| Reference answer | `expected`, `output`, `answer`, `completion`, `target`, `reference` |

Anything else is ignored. A column called `gold`, `label`, `response` or `ground_truth` will not
be found.

> **A missing reference field makes the `contains` scorer report a perfect score on every
> record.** When no reference is found the scorers are handed an empty string, and "does the
> output contain an empty string" is true for every output ever produced. The run finishes,
> the chart fills in, the leaderboard shows `contains: 1.000`, and none of it means anything.
>
> This is the single most dangerous failure mode on this page, because it looks like success.
> If you see a `contains` average of exactly 1.000, check your column names before you believe
> it. The other scorers fail loudly in the same situation — `exact_match`, `rouge_l` and `bleu`
> all report 0.000 — so a run scoring 1.000 on `contains` and 0.000 on everything else is
> almost certainly a naming problem, not a model result.

---

## Scoring methods

Every scorer returns a value between 0 and 1. Ask for as many as you like; each becomes its own
column.

| Method | What it measures | When it is the right choice |
|---|---|---|
| `exact_match` | The output equals the reference, case-insensitive and trimmed. | Classification labels, multiple choice, short factual answers. Useless on free text. |
| `contains` | The reference appears somewhere in the output. | Checking that a required fact, label or identifier is present in a longer answer. |
| `rouge_l` | F1 over the longest common subsequence of whitespace tokens. | Summarisation, where the reference is one acceptable phrasing among many. |
| `bleu` | Smoothed 1- to 4-gram precision with a brevity penalty. | Translation, and anything else where word order carries the meaning. |
| `length_ratio` | Shorter length divided by longer length, in characters. | Detecting a model that is systematically padding or truncating. Says nothing about correctness. |

Two things the API will not tell you:

- **`llm_judge` exists in the code but is not wired up.** Requesting it does nothing — the name
  is dropped and no judge column appears. There is no LLM-as-judge scoring in this build.
- **Unknown method names are dropped silently too.** A typo such as `rougel` or `exact-match`
  produces a run with one fewer column and no error anywhere in the UI. Check the Configuration
  panel on the detail page against what you actually asked for.

---

## Watching a run

The Evaluations tab shows one card per run, with the status badge, the model count, the scoring
methods, and a progress bar. Click a card to open the detail page.

**The list does not refresh itself.** The detail page does: while a run is Pending or Running it
polls every three seconds. To watch progress, open the run rather than sitting on the list.

> **Known display bug: progress is rendered as a percentage of a percentage.** The backend
> already stores progress as 0–100 and the page multiplies by 100 again. A run that is one
> tenth done reads `1000%`; a finished run reads `10000%`. The `completed / total` record count
> beside it is correct — read that instead.

---

## Reading the results

The detail page has two tabs.

**Summary** is where the answer is. A grouped bar chart shows one group per scoring method and
one coloured bar per model, on a y-axis fixed to 0–1 so runs are visually comparable. Below it,
a table gives **Model**, **Records**, **Avg Latency**, **Tokens** (prompt / completion),
**Errors** and **Scores**.

Errors are worth a glance every time. A record whose inference call failed is recorded with its
error and excluded from the averages, so a model that failed on the hard half of your dataset
can post a better average than one that answered everything.

**Model Details**, despite the name, contains no per-model detail. It has a Configuration panel
listing the models, scoring methods, split and timestamps, and an Error panel that appears only
when the evaluation itself recorded an error. For per-record output you need the export.

---

## The Leaderboard tab

A table across all completed evaluations: **#**, **Model**, **Evaluation**, **Records**,
**Avg Latency**, **Scores**.

> **The trophy on row 1 does not mark the best model.** The page asks the API for the
> leaderboard without naming a scoring method, and in that case the API sorts by evaluation
> date. Row 1 is the model from your most recent completed run. The API can sort by score, but
> nothing in the UI asks it to.
>
> Read the score badges in the right-hand column and rank them yourself. Treat the numbers in
> the `#` column as row numbers.

The other thing to keep in mind is that rows from different evaluations are not comparable.
Two runs against different datasets, or the same dataset with different scoring methods, sit
side by side in this table with nothing distinguishing them but the evaluation name.

---

## Cancelling

A **Cancel** button appears on the detail page while a run is Pending or Running.

> **Cancel is cosmetic.** It sets the row's status to Cancelled and stamps a finish time. The
> background worker does not read that status and carries on to the last record, then overwrites
> the status with Completed when it finishes. Every token you were trying not to spend is spent
> anyway.
>
> If you have started a run you genuinely need to stop, stopping the backend process is the only
> thing that works. On restart the job is reclaimed and resumes, skipping records already
> scored, so plan accordingly.

---

## Things the API accepts and ignores

`config` and `promptVersionId` are both stored on the evaluation and neither is ever read by the
runner. The request that goes to the model carries the model name and a single user message
containing the raw prompt field — nothing else.

That means **you cannot set a temperature for an evaluation.** Whatever the provider's default
is, that is what you get, and it is very unlikely to be 0. Two runs of the same evaluation will
not produce identical scores. If reproducibility matters, use the [Playground](playground.md)
with temperature 0, or accept that small score differences between runs are noise.

Likewise, there is no prompt templating. `promptVersionId` does not pull a template from
[Prompt Lab](prompt-lab.md); the prompt is the dataset field verbatim. If you want instructions
around it, put them in the dataset.

---

## Exporting the per-record results

The only way to see individual outputs is the export endpoint. There is no button for it.

```bash
curl -o results.csv \
  "http://localhost:5000/api/v1/evaluation/<evaluation-id>/results/export?format=csv"
```

`format` is required and must be `csv` or `json`. Omitting it returns a 400, not a default.
Add `&model=llama3.1:8b` to restrict the export to one model.

The CSV columns are:

```
Model,RecordId,Input,Expected,Actual,LatencyMs,PromptTokens,CompletionTokens,Error
```

followed by one column per score key, sorted alphabetically. The JSON form carries the same
fields plus perplexity, as an array of objects.

Reading a sample of the `Actual` column against `Expected` is the fastest way to find out
whether a low ROUGE score means the model was wrong or means it phrased the right answer
differently.

---

## Why bother

A number you can defend is the point. "The 8B model felt about as good" is not a finding;
"ROUGE-L 0.41 against 0.44 on the same 500-record test split, with the scoring method recorded"
is one, and it survives you changing your mind next month.

Be honest with yourself about what the metrics measure, though. ROUGE and BLEU count surface
overlap between strings. A correct answer worded differently from your reference scores badly,
and a fluent wrong answer that reuses the reference's vocabulary scores well. They are only
meaningful when the reference is genuinely the answer you want and there are not many equally
good ways of writing it. Where that does not hold, use them to rank models against each other
on the same data — which is what they are good at — rather than as an absolute measure of
quality.

`length_ratio` in particular measures nothing about correctness. It is there to catch a model
that has started emitting one-word answers or three-paragraph preambles, which is a real
problem worth catching, and nothing more.

The summary also says how sure it is. Every mean score with at least two items carries a 95%
Student-t confidence interval in its badge, and with two or more models a **Model comparisons**
table runs a paired two-sided t-test per metric over the items both models scored — mean
difference, its 95% CI, the t statistic and p-value. Read the CI of the difference before the
means: an interval that straddles zero means the data on hand does not establish a difference,
however the bar chart looks. On a handful of items the intervals are wide — sometimes wider
than the 0–1 score scale, which is the interval being honest, not broken. Two models that give
identical answers show a dash for t and p (undefined, not zero), and the implementations are
differential-tested against scipy.stats.

---

## What this page will not do

- **No way to start a run from the UI.** API only, as above.
- **No choice of inference instance.** The runner takes an arbitrary registered one.
- **No temperature, no sampling parameters, no prompt template.** `config` and
  `promptVersionId` are accepted and discarded.
- **No error surfaced when a run fails at the infrastructure level.** If no instance is
  registered, or the endpoint is unreachable in a way that breaks the whole job, the evaluation
  stays on **Running** indefinitely with an empty Error panel. Nothing in the UI will ever tell
  you it died. Check the backend logs.
- **Cancel does not stop the run.**
- **The leaderboard trophy is chronological, not a ranking.**
- **No re-run and no delete.** Export (CSV/JSON) and the per-record view now exist.

---

## See also

- [Datasets](datasets.md) — creating the dataset and naming the columns correctly
- [Batch Inference](batch-inference.md) — the same shape of run, without scoring
- [Analytics](analytics.md) — token and latency totals across everything, including these runs
- [Model Management](models.md) — registering the instance the runner will pick

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | A dataset exists with expected values to score against | Yes — `DatasetsSeeder` creates one with train/test/val splits | `DatasetsSeeder.cs:27-50` |
| P2 | At least one model is reachable to run the records through | No — nothing is registered until you do it | Models P1 |
| P3 | Scoring is string comparison unless `llm_judge` is chosen | True, and `llm_judge` costs an inference call per answer | `Domain/Scorers/LlmJudgeScorer.cs` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | An evaluation can be started without leaving the UI | browser check: `/evaluation` → New Evaluation → ran to `Completed` | MET |
| R2 | The dataset is chosen by name, never by GUID | `DatasetPicker` lists name and record count | MET |
| R3 | Every scorer the backend implements is offerable, not only ones already used | dialog lists all six from `Domain/Scorers` | MET |
| R4 | The cost of the run is stated before it is started | dialog states records x models, and flags the judge's extra call | MET |
| R5 | A running evaluation's progress advances without user action | list polls at 3s while anything is Running or Pending | MET |
| R6 | An unloadable evaluation reports the failure rather than waiting forever | detail page renders the error and a way back; previously "Loading..." was the only branch | MET |
| R7 | The leaderboard ranks results across evaluations, not within one | `GET /evaluation/leaderboard`; each row names its evaluation | MET |
| R8 | Which records a model got wrong can be inspected | the Records tab pages through per-record results with scores, expected/actual, and the error text for failed rows; first caller of the per-record endpoint | MET |
| R9 | Results can be exported | CSV and JSON buttons on the detail page header; `format` omitted no longer 400s (nullable binder fix) | MET |
| R10 | BLEU and ROUGE-L are reference-correct, citable numbers | line-by-line ports of sacrebleu 2.6.0 (13a, exp smoothing, case-sensitive) and rouge-score 0.1.2 (LCS F1); differential tests agree to 1e-9 on 29 published-reference pairs incl. empty/single-token/no-overlap/clipping/unicode/tab cases, plus invariants and hand-worked examples (`BleuRougeDifferentialTests`, mutation-checked) | MET |
| R11 | Every score carries the definition that produced it | scorer definitions (tokeniser, smoothing, scale, reference version) are recorded on the evaluation at run time and rendered under the summary and on each badge's tooltip | MET |
| R12 | Corpus BLEU is computed from pooled statistics, never presented as the mean of sentence scores | `corpus_bleu` badge computed by summing per-segment n-gram statistics (sacrebleu corpus definition, differential-tested incl. summed lengths and BP); the chart is labelled "means of per-item (sentence-level) scores" | MET |
| R13 | Calibration (ECE, Brier) is computed from stored logprobs and reachable | evaluations request logprobs when the provider supports them and store them; the Calibration tab shows the reliability diagram with server-computed ECE (10 bins, stated) and Brier, from chosen-token probabilities; hand-computed fixture asserts ECE 0.055 / Brier 0.17825 exactly, invariants prove ECE 0 for perfect calibration and 1 for maximal overconfidence (`CalibrationMetricsTests`, mutation-checked); browser-verified | MET |
| R14 | When calibration cannot be computed, the tab states which prerequisite is missing | the tab distinguishes "no successful results", "no logprobs recorded", and "no exact_match label", with counts; browser-verified on an all-failed evaluation | MET |
| R15 | Requesting an unknown scorer fails loudly at start, not silently at run time | `StartEvaluationHandler` validates names against the registered set (+`llm_judge`) and returns 400 naming the valid ones | MET |
| R16 | The runner uses the default instance, not an arbitrary row | selection orders by `IsDefault`, then online status — previously it took the first row and ran the whole evaluation against a dead seeded endpoint | MET |
| R17 | `llm_judge` actually judges | constructed per run with the provider and a judge model (the model under test — stated in its recorded definition); previously unregistered and silently dropped | MET |
| R18 | Mean scores state their uncertainty | 95% Student-t CI per metric per model wherever ≥2 items were scored, rendered in the score badge; absent for one item, never zero-width; differential-tested against scipy.stats.t.interval to 1e-9 incl. df=1 and df=999, invariants for symmetry/containment/nesting (`StatisticalMetricsTests`, 26 tests, mutation-checked incl. the Bessel correction); browser-verified | MET |
| R19 | Model differences are tested, not eyeballed | paired two-sided t-test per metric over dataset items both models scored (pairing by record id, failed calls excluded — both mutation-checked); mean Δ, CI of Δ, t, p vs scipy.stats.ttest_rel to 1e-9; zero-variance pairs report undefined (dash), not p=0; deep-tail p-values computed via the survival function after an adversarial pass showed 2·(1−CDF) cancels to ~3 digits (`EvaluationStatisticsTests`); browser-verified defined and degenerate paths | MET |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | ~~Calibration plots are shown for probabilistic scorers~~ | **Reinstated and MET** as R13 — the runner now stores logprobs and the Calibration tab is `CalibrationPlot`'s first caller | research-capabilities change |
