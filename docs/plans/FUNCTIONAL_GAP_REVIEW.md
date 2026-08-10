# Functional Gap Review — plan

**Status:** Phases 0-4 done. Phase 5 (tour reconciliation) is all that remains.
**Trigger:** writing a guided tour for all fifteen tabs required reading each page against its
code. Six tours had to be rewritten because the page did not do what its name implied, and two
already-shipped tour steps described capabilities that do not exist. A tour is only a symptom:
the same gaps mislead every reader.

---

## What this plan is for

Two different problems got tangled together and need separating.

**Documentation already tells the truth.** `docs/features/evaluation.md` opens with "There is no
New Evaluation button". `docs/product-truth.yaml` exists precisely so claims cannot outpace
reality. `docs/assumptions.md` records what has been proven versus asserted. None of that is the
gap.

**Nothing states, per page, what the page is supposed to do in a form that can be falsified.**
So "is this page finished?" has no answer, and a missing capability is indistinguishable from a
deliberate omission. That is the gap this plan closes.

The end state: every tab has a numbered set of functional requirements, each one falsifiable by
a named check; every requirement is marked MET, UNMET or WITHDRAWN against the code as it stands;
each UNMET is either fixed or explicitly withdrawn with a reason; and the tour for that tab is
re-read against the result.

---

## How a requirement is written

Three rules, and they exist because breaking any one of them is how a requirements document
becomes decoration.

**1. Falsifiable.** A requirement names an observation that would prove it false. "The page is
intuitive" cannot fail. "Submitting the form with an empty Instance ID shows an error within
500 ms" fails the moment it silently does nothing.

**2. Verified by a named check.** Each requirement carries the command, test name or manual
click-path that decides it. A requirement nobody can run is an opinion.

**3. Presuppositions stated separately.** A requirement usually assumes something it does not
itself assert — that a provider is registered, that the dataset has an output column, that the
server supports guided decoding. Those assumptions get their own line, because most of the gaps
found so far are *unstated presuppositions that turn out to be false on a normal install*.

### Template

```markdown
## <Tab name>

**Purpose (one sentence, what a researcher gets that they cannot get elsewhere).**

### Presuppositions
| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | A provider is registered | No — Models page is empty | ModelsPage.tsx:68 |

### Requirements
| # | Requirement (falsifiable) | Verified by | Status |
|---|---|---|---|
| R1 | Starting an evaluation is possible without leaving the UI | click-path: /evaluation → New | UNMET |

### Withdrawn
| # | Requirement | Why withdrawn | Decided by |
```

Status is one of **MET**, **UNMET**, **WITHDRAWN**. Nothing else — "partial" is how a gap hides.

---

## Where requirements live

Extend the existing `docs/features/<tab>.md` rather than adding a parallel tree. Those files are
already per-tab, already honest, and already the thing someone reads. A new
`## Functional requirements` section goes at the foot of each, so the prose guide stays the front
door and the falsifiable list sits behind it.

`docs/product-truth.yaml` gains a `requirements_met: <n>/<total>` field per module, so the matrix
reports progress without duplicating the detail.

---

## Known gaps to seed the review

Gathered while researching the tours. This is an input, not the output — each becomes a
requirement that is currently UNMET, or a withdrawal with a reason. Nothing here has been fixed.

### Whole-product

| # | Gap | Evidence |
|---|---|---|
| G1 | Mutations fail silently — no global error handler; only queries have defaults | `app/providers/QueryProvider.tsx:3-10` |
| G2 | Several pages take a raw instance **GUID** as free text while every other page uses a dropdown | `structured-output`, `agents` |
| G3 | `dev.sh` starts a second API when the port is taken rather than noticing another Prism is already running **against the same database**. Four stale APIs were found fighting over one row, each with its own 30s health-check writer | observed 2026-08-09 |

### Per tab

| Tab | Gap | Kind |
|---|---|---|
| Models | "New Instance" hidden when zero instances — the users who need it most cannot reach it | reachability |
| Models | Detail panel is `lg:block`; on a narrow window selecting a card does nothing visible | responsive |
| Token Explorer | Predict against a provider without logprobs fails silently — spinner stops, nothing appears | silent failure |
| Token Explorer | Top-p / top-k sliders are visualisation-only; nothing on screen says so | misleading |
| History | Search needs Enter while every other filter applies on change | inconsistency |
| Prompt Lab | Prompt editors are read-only, on a page called an editor | misleading |
| Prompt Lab | `useAbTest` exists and is called from nowhere — no A/B UI | dead capability |
| Experiments | Sweep runs N inference calls sequentially inside one HTTP request | blocking |
| Experiments | Detail panel hidden below `lg` | responsive |
| Datasets | Split filter and page number are global and never reset between datasets — produces an empty table with no stated cause | state leak |
| Datasets | `useUpdateRecord` unused; records are read-only | dead capability |
| Evaluation | **No way to start an evaluation from the UI at all** | missing entry point |
| Evaluation | List never refreshes; card progress bars are frozen | staleness |
| Evaluation | No error or not-found state — a bad id shows "Loading..." forever | missing state |
| Evaluation | `CalibrationPlot` imported nowhere; per-record drill-down endpoint has no UI | dead code |
| Batch | **No way to create a batch job from the UI at all** | missing entry point |
| Batch | List never polls, on a page whose purpose is progress | staleness |
| Batch | No detail route; results and download endpoints unreachable | missing entry point |
| Batch | Cost estimator endpoint exists, `useEstimateBatchCost` unused | dead capability |
| Analytics | Cost column is a string heuristic on the model name; the real priced figure is returned by the API and never rendered | wrong data |
| Analytics | No date range control despite the API supporting `from`/`to` | missing control |
| RAG | Seeded collection has null embeddings — vector and hybrid return nothing on a cold install | seed defect |
| RAG | Search shows no error and no empty state; a failed embed looks identical to no matches | silent failure |
| RAG | Tab is "Search & RAG" but there is no generation; `useRagPipeline` unused | misleading |
| Structured Output | Run with a blank field is a silent no-op; inference errors never surface | silent failure |
| Agents | `Sequential` pattern is stored, displayed, and never read | decorative control |
| Agents | Seeded run's trace is hand-written fiction presented as a real run | misleading seed |
| Agents | Failures are `console.error` only; run history does not refresh after a run | silent failure |
| Agents | `api_call` tool accepts any URL from the backend's network position | **security** |
| Fine-Tuning | Prism trains nothing; adapters are inert rows, `IsActive` has no writer | scope question |
| Fine-Tuning | Export silently drops records when the column mapping does not match, and includes the test split | data correctness |
| Notebooks | JupyterLite build absent; the dev server serves Prism inside Prism rather than 404ing | broken feature |
| Notebooks | Save validates nothing; broken and empty notebooks are indistinguishable | data correctness |

### Decisions taken (2026-08-09)

**2. Notebooks — ship the JupyterLite build.** In scope. The page is otherwise sound; only the
runtime is missing.

**3. Evaluation and Batch — build the create UI.** In scope. Both backends are complete and
tested; this is frontend work against existing endpoints.

**4. Agents `api_call` — stays open, documented.** Correct for a local research tool. The risk
and the operational rules were already written up in `docs/features/agents.md`; a **Hardening
this later** section now sets out the path — resolve-then-check against private address space,
no redirect following, per-workflow scope, URL audit lines, per-run call caps, and never
forwarding Prism's own credentials. The end state proposed there is deny-by-default with an
explicit unrestricted mode that refuses to run when Prism is not bound to loopback, so a laptop
default cannot silently become a server default.

**1. Fine-Tuning — decided 2026-08-09: keep the tab, mark it unimplemented.** The removal
recommendation below was not taken; the intent for the feature is undecided, so rather than
delete a tab that might be wanted, the page now states on arrival that fine-tuning is not
implemented. It says which half is missing (training, and adapters that nothing reads) and which
half works (dataset export), so it does not hide a finished tool behind a blanket notice. The
sizing below stands for whenever the question is revisited.

*Original recommendation, retained for when training is reconsidered:*

The instruction was "in scope, or if it is too large we remove the tab", so here is the sizing.

*Training is disproportionate.* Prism installs as .NET, Node and Docker. Every LoRA trainer worth
using is Python — PEFT, Unsloth, MLX-LM — so training means adding a Python toolchain, an
environment to manage, a trainer script to ship, progress streaming, and artefact handling. The
durable job system already exists (`Prism.Common/Jobs`, with leases and a Redis queue) so the
orchestration half is cheap, but the toolchain half is a new dependency for the whole product.

*And it would not work on this machine.* Adapters only become useful if inference can load them.
vLLM serves LoRA adapters per request; vLLM needs CUDA and cannot run on Apple Silicon. Ollama
takes an `ADAPTER` only at model-creation time via a Modelfile, not per request. So on the
hardware this project is being developed on, both training and adapter-serving are unavailable —
we would be building a tab that its author cannot use.

*What is actually real here is Export*, and it is finished: four formats, a preview, a record
count, warnings. It has nothing to do with adapters. It belongs beside the dataset it exports,
where the dataset id is already in the URL — which also removes the hand-typed GUID (G2).

So: fold Export into the dataset detail page, delete the inert adapter register (`IsActive` has
no writer and no inference path reads `LoraAdapter`), and remove the sidebar entry. Smaller than
either alternative, and nothing real is lost. **Reversible** — if training is wanted later it
arrives as a job type, which is the part that is already built.

Override this if the intent was always to train; it is a project, not a gap, and should be
planned as one.

---

## Phasing

Ordered by "would a newcomer hit this", not by effort.

**Phase 0 — cross-cutting, one change each, unblocks the rest. DONE.**
G1 surfaced via a MutationCache handler that skips mutations handling their own errors; G2
replaced both GUID boxes with a shared `InstancePicker`; G3 now stops stale APIs *and* stale
Vite servers, the latter being worse because Vite fixes its proxy target at launch.

**Phase 1 — silent failures. DONE.** Token Explorer now explains a failed predict and names the
logprobs cause; RAG search distinguishes "did not run" from "matched nothing" and points at BM25;
Structured Output cannot present an enabled button that does nothing, shows inference errors, and
no longer files the guided-decoding advisory as a validation error beside a green tick; Agents
surfaces a failed run and refreshes its history afterwards.

Found while verifying: **`npx tsc --noEmit` checks nothing in this repo.** The root
`tsconfig.json` is `"files": []` plus references, so it exits 0 regardless. `tsc -b --noEmit` is
the real check and is what the pre-commit gate runs — which is why nothing broken was committed,
but any typecheck claim made with the plain form was worthless.

**Phase 2 — missing entry points. DONE for Evaluation and Batch.** Both now have a create
dialog, built on the hooks that already existed and were called from nowhere. A shared
`DatasetPicker` supplies dataset and split by name, so neither form needs a GUID. Both lists poll
while anything is running, which they never did — on Batch especially, where there is no detail
page, the list was the only place progress could be seen and it never moved. Evaluation's detail
page no longer shows "Loading..." forever on a bad id.

Verified by driving both dialogs in a browser: the evaluation ran to Completed and the batch job
finished 6/6 records.

Still open in this phase: Models' "New Instance" button is hidden on an empty install.

**Phase 3 — wrong or stale data. DONE except Fine-Tuning.** Analytics now renders the cost the
backend actually priced, keeping null ("no pricing recorded") apart from zero ("priced, and
free") instead of guessing from the model name, and gained the window control the API always
supported. The Datasets split filter no longer follows you onto a dataset that has no such split.
Evaluation/Batch polling landed with Phase 2. Models' register control is no longer hidden from
the people with nothing registered.

Fine-Tuning's export mapping is deliberately untouched pending decision 1 — the recommendation
is to move Export to Datasets, and fixing it in place first would be work thrown away.

**Phase 4 — scope decisions, now settled.** Notebooks: ship the JupyterLite build (decision 2).
Fine-Tuning: fold Export into Datasets, drop the adapter register and the sidebar entry
(decision 1, pending override). Note the tour test asserts every sidebar destination has a tour,
so removing a tab means removing its tour in the same change — the suite will say so.

**Phase 5 — tour reconciliation.** Re-read all fifteen tours against the fixed reality. Anything
a tour apologises for that is now fixed gets rewritten; the anchor tests already fail if a
region moves.

Each phase ends with the requirements table for the affected tabs updated, and
`product-truth.yaml` counts refreshed.

---

## Definition of done, per tab

1. `docs/features/<tab>.md` has a Functional requirements section following the template.
2. Every requirement is MET, or UNMET with an issue reference, or WITHDRAWN with a reason.
3. Every presupposition states whether it holds on a cold install, with evidence.
4. Every MET requirement names a check that actually runs.
5. The tab's tour has been re-read against the table and contains no apology for a fixed gap.
6. No dead capability remains undeclared: a hook or endpoint with no UI is either wired up,
   deleted, or listed as WITHDRAWN.

---

## What this plan deliberately does not do

It does not promise all fifteen tabs at once. The inventory above is roughly forty items, four
of which are projects rather than fixes. Attempting them together would produce the same
"claims outpacing reality" this repo already has a file to prevent.
