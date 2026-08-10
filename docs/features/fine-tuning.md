# Fine-Tuning

**Turn a dataset you curated in Prism into a training file another tool can read.**

Be clear about what this page is before you plan around it. It does two things: it converts a
dataset into one of four training-data formats and hands you the file, and it keeps a list of
LoRA adapters you have trained elsewhere. **It does not train anything.** There is no training
job, no hyperparameters, no learning rate, no progress bar, no GPU work of any kind. And a LoRA
adapter registered here is never loaded, never served and never used for inference — registering
one changes nothing about how any model behaves.

The sidebar entry promises more than the feature delivers. The export half is genuinely useful
and finished; the adapter half is a list.

Sidebar: **Fine-Tuning**. Page heading **Fine-Tuning**, subtitle *LoRA adapter management and
dataset export for fine-tuning*. Two tabs: **LoRA Adapters** and **Export Dataset**.

---

## Before you start

For the export tab you need a dataset with records in it, and you need its GUID. Unlike most
GUIDs in Prism this one is easy to get: open the dataset from the [Datasets](datasets.md) page
and copy it out of the browser address bar, which reads `/datasets/<guid>`.

You also need to know which of your dataset's columns hold the instruction, the input and the
expected output. The three mapping boxes default to `instruction`, `input` and `output`, and
most real datasets do not use those names. The dataset Prism seeds, *Sentiment Analysis
Samples*, has columns called `text`, `label` and `confidence` — export it with the defaults and
you get a file with zero records in it and a warning per row.

---

## Export a dataset

Open the **Export Dataset** tab.

| Field | Default | Notes |
|---|---|---|
| **Dataset ID** | empty | Required. Raw GUID, typed or pasted. Placeholder reads *Dataset GUID*. |
| **Export Format** | Alpaca | Four options, described below. |
| **Instruction Column** | `instruction` | The column whose value becomes the task or question. |
| **Input Column** | `input` | The column whose value becomes the additional context. Optional in every format. |
| **Output Column** | `output` | The column whose value becomes the target completion. |

Click **Export**. The result appears below: a line reading *Exported N records as
`<filename>`*, a **Download** link, a yellow warnings box if anything was dropped or altered,
and a preview of the first 3000 characters of the file.

Nothing is written to disk until you click **Download**. The preview is truncated; the download
is not.

Records are exported in the dataset's stored order.

### The formats

The exact shape matters, because these files go straight into trainers that are fussy about
them.

**Alpaca** — a pretty-printed JSON array. File `{dataset name}_alpaca.json`, content type
`application/json`.

```json
[
  {
    "instruction": "Classify the sentiment of this review.",
    "input": "The battery lasts about four hours.",
    "output": "negative"
  }
]
```

**ShareGPT** — a pretty-printed JSON array of conversation objects, each with a two-turn
exchange. File `{dataset name}_sharegpt.json`, content type `application/json`.

```json
[
  {
    "conversations": [
      { "from": "human", "value": "Classify the sentiment of this review.\nThe battery lasts about four hours." },
      { "from": "gpt", "value": "negative" }
    ]
  }
]
```

**ChatML** — plain text, not JSON. One block per record, separated by a blank line. File
`{dataset name}_chatml.txt`, content type `text/plain`.

```
<|im_start|>user
Classify the sentiment of this review.
The battery lasts about four hours.
<|im_end|>
<|im_start|>assistant
negative
<|im_end|>
```

**OpenAI JSONL** — one JSON object per line, no array wrapper, no indentation. File
`{dataset name}_openai.jsonl`, content type `application/jsonl`.

```
{"messages":[{"role":"user","content":"Classify the sentiment of this review.\nThe battery lasts about four hours."},{"role":"assistant","content":"negative"}]}
```

Note the difference in how the three conversational formats treat your columns. Alpaca keeps
`instruction` and `input` as separate fields, which is what Alpaca-style trainers expect.
ShareGPT, ChatML and OpenAI JSONL have nowhere to put a separate input, so they concatenate:
the user turn is the instruction, a newline, then the input. If the input column is empty the
user turn is the instruction alone. If you were relying on a trainer seeing those two pieces
separately, only Alpaca gives you that.

The filename uses the dataset's name verbatim, spaces and all. A dataset called *Sentiment
Analysis Samples* downloads as `Sentiment Analysis Samples_alpaca.json`.

### Read the warnings box

This is the part people skip and then regret. The exporter drops records quietly, and the
warnings box is the only place it says so. **A silently short export is the failure mode of this
page** — you get a valid file, it trains fine, and it contains a third of your data.

What produces a warning, and what each format does about it:

| Situation | Alpaca | ShareGPT / ChatML / OpenAI JSONL |
|---|---|---|
| Instruction column missing or blank | Record **skipped**, warning `Record N: missing 'instruction' field, skipped.` | Skipped **only if the input column is also blank**, with warning `Record N: no instruction or input, skipped.` Otherwise the record is kept and the user turn begins with a stray newline. |
| Output column missing or blank | Record **kept**, output emitted as `""`, warning `Record N: missing 'output' field.` | Record **kept**, output emitted as `""`, **and no warning at all.** |
| Column name does not exist in the dataset | Treated as blank, same as above. | Treated as blank, same as above. |

Two things follow. The *Exported N records* count is the number of records that made it into the
file, not the number in your dataset — compare it against the dataset's record count every time.
And on the three conversational formats, a column-name typo in **Output Column** produces a file
full of empty assistant turns with a completely clean warnings box. Check the preview.

### There is no split selection

The exporter reads every record in the dataset. It does not offer a split filter and it does not
look at the split label on each record, even though Prism records one.

**So every export includes your test split.** If you split a dataset into train, test and
validation on the Datasets page and then export it here, you get all three concatenated, and if
you feed that to a trainer you have trained on your own test set. Every evaluation you run
afterwards is worthless, and nothing about the resulting numbers will look wrong.

Until this page grows a split selector, the workaround is to keep the splits as separate
datasets: export the records you want from the Datasets page, create a training-only dataset,
and give this page that GUID. Splitting inside one dataset is fine for evaluation; it does not
protect you here.

### Size

The whole export is built as a single string in the API, returned as a single JSON response,
held as a string in the browser, and then copied again into a Blob for download. A dataset of a
few thousand short records is unremarkable. Hundreds of thousands of long records will be slow
and may fail in the browser rather than in the backend. There is no streaming, no chunking and
no server-side file.

---

## LoRA Adapters

The other tab keeps a list. Click **Register Adapter** to open the **Register LoRA Adapter**
dialog: **Name**, **Description**, **Instance ID**, **Adapter Path**, **Base Model**. Everything
except the description is required, and every one of them is a free-text field.

What happens when you click **Register** is that a row is written to the database. That is the
whole behaviour. Specifically:

- **The adapter path is never validated.** It is not checked for existence, not checked for
  being a LoRA directory, not even checked for being a plausible path. A typo is stored happily.
- **The instance ID is never checked.** It is not looked up, so a GUID belonging to nothing at
  all is accepted. Nothing ever uses it to do anything.
- **The Inactive label can never become Active.** The card shows *Inactive* in grey because the
  underlying flag is false and there is no code path anywhere in Prism that sets it to true. No
  endpoint, no button, no background job.
- **No inference call loads the adapter.** Not the Playground, not Agents, not Batch Inference,
  not Evaluation. The adapter table is read by this page and by nothing else.

Treat it as a notebook entry: a place to write down where you left an adapter and what it was
trained on, so that in three months you can find it. That is a real use, and it is worth doing.
It is not model management, and an adapter listed here has no effect on any result Prism
produces.

Deleting an adapter asks for confirmation and removes the row. There is no edit.

---

## Why a researcher would use this

The honest case is narrow and real: you curated a dataset in Prism — uploaded it, inspected the
columns, annotated rows, cut a split — and now you want a correctly shaped training file out of
it without writing a conversion script that you will get subtly wrong at 11pm.

The formats here match what the common trainers actually expect, down to the field names and the
role tokens, and the warnings tell you what was dropped on the way. Those two things together
are the value. Getting a clean file with a known record count and an explicit list of
exclusions beats a bespoke pandas snippet whose failure mode is silence.

Everything after the download happens somewhere else. Bring the resulting adapter back, register
it here so you remember where it is, and evaluate it by pointing Prism at a server that has the
adapter loaded.

---

## What this page will not do

- **It does not train.** No job, no queue, no hyperparameters, no metrics, no checkpoints.
- **A registered adapter does nothing.** See above — path unvalidated, instance unchecked,
  status permanently *Inactive*, never loaded for inference.
- **No split selection on export.** The whole dataset, test rows included.
- **No shuffling, no deduplication, no filtering, no train/validation ratio.**
- **No system-prompt column.** All four formats produce two-turn user/assistant exchanges. There
  is no way to emit a system message, and no way to export multi-turn conversations.
- **No token counting or length filtering.** Records longer than your context window are
  exported without comment.
- **No dataset picker.** The **Dataset ID** field is a raw GUID with no dropdown and no search.
- **Missing output is silent on three of the four formats.** Only Alpaca warns about it.
- **The export is not saved.** Navigate away before clicking **Download** and you run it again.
- **No adapter edit, no adapter activation, no upload of adapter weights.**

---

## See also

- [Datasets](datasets.md) — where the data comes from, and where the GUID is in the URL
- [Model Management](models.md) — registering the server that would actually serve an adapter
- [Experiments](experiments.md) — for comparing a base model against a fine-tuned one once both
  are being served

---

## Functional requirements

The page opens with an overlay stating that fine-tuning is not implemented. Each of its claims
was checked against the code and all hold: there is no training anywhere in the backend, no
inference path reads `LoraAdapter`, `IsActive` has no writer, and all four export formats are
implemented.

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | Prism trains models | **No.** No training code exists; the feature is four handlers — create, list, delete adapter, and export | `Prism.Features/FineTuning/` |
| P2 | Registering an adapter affects inference | **No.** Nothing outside the slice reads `LoraAdapter` | grep returns only the slice and migrations |
| P3 | The default column mapping works on the dataset Prism ships | **No.** The seeded dataset's columns are `text`/`label`/`confidence`; the mapping defaults to `instruction`/`input`/`output`, so the default export yields zero records | `DatasetsSeeder.cs:47-49`; defaults at `ExportFineTuneHandler.cs:74-76` |
| P4 | Export respects the train/test split | **No.** There is no split field on the request and no filter on the query, so a split dataset exports its test rows into the training file | `ExportFineTuneHandler.cs:65-69` |
| P5 | The record count says how much of the dataset was exported | **No.** It counts rows that reached the file; the shortfall is visible only by counting warnings | `ExportFineTuneHandler.cs:123` |
| P6 | A missing output column is always warned about | **No.** Alpaca warns; ShareGPT, ChatML and JSONL emit an empty assistant turn silently | `:114-118` versus `:140-144`, `:177-181`, `:211-215` |

P4 is the one with teeth: exporting a split dataset and training on the result means training on
your own test set, with nothing on screen to indicate it.

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | Arriving states that training is not implemented, before any form is usable | browser check: a portal overlay blocks the page until dismissed | MET |
| R2 | The notice does not imply the working part is broken | same check asserts it names dataset export as functional | MET |
| R3 | All four advertised formats produce output in that format | export the seeded dataset with a correct mapping; compare against the handler | MET |
| R4 | A mapping that matches nothing reports zero records rather than appearing to succeed | leave the defaults and export; "Exported 0 records" plus a warning per row | MET |
| R5 | Deleting an already-deleted adapter says so | 404 → the global mutation toast names it | MET |
| R6 | A dataset is chosen from a list; no GUID is typed | none — the field is a free-text "Dataset GUID", though `DatasetPicker` exists and is used by two other pages | **UNMET** |
| R7 | An export can be limited to one split | none — no control, no request field, no filter | **UNMET** |
| R8 | A record missing its output column warns in every format | none — three of four formats are silent | **UNMET** |
| R9 | Registering an adapter selects its server from a dropdown | none — free-text "vLLM instance GUID", validated only for non-emptiness | **UNMET** |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | Prism trains models | Out of scope for now; the overlay says so rather than the page implying otherwise. Sizing for reconsidering it is in `docs/plans/FUNCTIONAL_GAP_REVIEW.md` | decided 2026-08-09 |
| W2 | An adapter can be made active | Nothing loads adapters, so `IsActive` can never become true. The badge should be removed rather than the flag implemented | this review |
