# Datasets

**Upload a file, look at what actually landed, and cut a reproducible split.**

A dataset in Prism is a list of records with a detected schema, optionally partitioned into
train, test and validation splits. The point of the page is not storage — you already have the
file — it is that the split is recorded, seeded and exportable, so an evaluation you run against
`test` is one somebody else can run again.

Sidebar: **Datasets**. Two pages: the list and a dataset's detail page.

---

## Before you start

Nothing to configure. Have the file ready and know two things about it.

**The format is decided by the file extension and nothing else.** No sniffing, no content
inspection. A JSON file named `.txt` is parsed as CSV and produces nonsense rather than an error.

**The schema is detected from the first record only.** Make sure the first row of your file has
every column, populated. The section on schema detection explains what happens when it does not,
and the consequences reach as far as your exports.

---

## Uploading

**Upload Dataset** opens a dialog with three fields:

| Field | Required | Notes |
|---|---|---|
| **Name** | yes | |
| **Description** | no | |
| **File (CSV, JSON, JSONL)** | yes | The picker accepts `.csv`, `.json` and `.jsonl`. The selected file name and size show underneath. |

### What each format needs

**CSV.** The first line is the header. The parser is hand-rolled: it handles quoted fields
containing commas, and that is about the extent of it. Escaped double quotes inside a quoted
field are not unescaped, and surrounding whitespace is trimmed. Empty fields become nulls. A
file with only a header line and no data rows is rejected as "File contains no records."

> A row with fewer fields than the header **silently loses its trailing columns**. It is not
> padded with nulls and it is not reported as malformed — the record is stored with only the
> columns it had. This is the failure mode to watch for, because a ragged CSV loads perfectly
> happily and the damage only shows up later as missing values in the Statistics tab.

**JSON.** Must be a top-level array of objects:

```json
[{"text": "...", "label": "positive"}, {"text": "...", "label": "negative"}]
```

A single object fails. `{"data": [...]}` fails. Both produce "Failed to parse file: ...".
Nested values inside a record are kept, but they are stored as objects and every view of them —
the records table, the CSV export — shows them stringified.

**JSONL.** One JSON object per line. Blank lines are skipped.

**Parquet is not supported.** It appears in the format list and on the format badge, and
uploading one produces a parse error. The file picker will not offer it to you in the first
place.

### Size

There is no configured limit in Prism, but the underlying request body cap is about 30 MB. Below
that, uploads work. Above it, the request fails with an unhandled error rather than a useful
message. Split large files or load them through the API.

### The dialog does not tell you what happened

> **On success the dialog stays open. On failure the dialog stays open.** There is no toast
> either way, and a parse failure looks exactly like nothing having happened.
>
> The one signal you get is that **the Name, Description and file fields clear themselves when
> the upload succeeds**. If they still hold your text, it failed.
>
> Either way: close the dialog and look at the list. A new card means it worked. No new card
> means the file did not parse, and the usual cause is a JSON file that is not a top-level array,
> or a CSV with a single line.

---

## The dataset list

Cards show the name, a format badge, the description, the record count, the file size, the
column count, and a badge per split with its record count. **Search datasets...** filters by
name. Clicking a card opens it.

Prism seeds one dataset, **Sentiment Analysis Samples**, already split.

---

## The detail page

The header shows the name, the description, the format badge and a summary line reading
`N records · N columns · v{version}`. The version number increments each time you re-split.

| Button | What it does |
|---|---|
| **Split** | Opens the split dialog. |
| **CSV** | Downloads the records as CSV. |
| **JSON** | Downloads the records as JSON. |
| trash (red) | Deletes the dataset. A native browser confirm asks "Delete this dataset and all its records?" |

### The split filter

Once a dataset has splits, a filter row appears: **All**, plus a button per split showing its
record count.

> **This filter also controls the export buttons.** With `test` selected, **CSV** and **JSON**
> download the test split only — same button, same filename convention, quietly one third of the
> data. It is easy to export a subset by accident and not notice until the row count looks wrong.
> Check the filter before you export, and check it again afterwards.
>
> The filter is also **global**. It is not per-dataset: select `train` here, navigate to another
> dataset, and that one opens filtered to `train` as well. Reloading the page clears it.

### Records tab

Fifty records per page, with pagination underneath showing the range and total.

Columns: `#` (the record's position in the original file, 1-based), a **Split** column that
appears only when the filter is on **All**, then one column per schema column. Values that are
objects or arrays are stringified into the cell; nulls and missing keys show as `—`.

Because the columns come from the schema, a column that exists in your records but not in the
schema is not shown here at all.

### Schema tab

A three-column table: **Column**, **Type**, **Purpose**.

Types are inferred from the first record's values: `string`, `number`, `boolean`, `array`,
`object`. **Purpose** is a slot for marking a column as the input, the label, or metadata — and
nothing in the upload path sets it, so for anything you upload it reads `—` on every row. Only
the seeded dataset has purposes filled in.

### Statistics tab

The validation report comes first.

It checks every schema column for nulls and missing keys, and grades the result: above 50%
missing is an **error**, above 10% a **warning**, anything else an informational note. It flags
values whose type does not match the detected column type. It lists columns that appear in your
records but not in the schema. And where splits exist, it counts records that were left without
a split assignment. A clean dataset gets a single green line instead: "Dataset passes all
validation checks".

This report is the fastest way to catch a ragged CSV or a bad first row. A column showing 40%
nulls that you know is fully populated in the source file means the parse went wrong, not the
data.

Below it: **Total Records**, a **Splits** count, a **Split Distribution** bar chart, and
**Column Statistics** — unique and null counts per column, with the most frequent values.

> **Column Statistics is broken in two visible ways.** Every entry renders with a blank column
> name, so you get a list of unlabelled boxes, and the top-values chips never appear even though
> the server computes them. The unique and null counts to the right of each blank name are
> correct, in schema order, so you can read them off against the Schema tab. The validation
> report above is unaffected and is the more useful of the two anyway.

---

## Splitting

**Split** opens a dialog with three ratio boxes — **Train** at 0.7, **Test** at 0.2,
**Validation** at 0.1, each a number input from 0 to 1 in steps of 0.05 — an optional **Random
Seed**, and a live total readout. The **Split** button stays disabled until the total is exactly
1.0.

> **Give it a seed if you want the split to be reproducible.** Left blank, the shuffle is seeded
> from the clock. The split is still recorded and still exportable, but it is not one you or
> anybody else can regenerate. Type a number — 42 is fine — and write it down next to the
> results.

Three things about how splits behave:

- They are **always named `train`, `test` and `val`**. The names are not configurable.
- There are **always three of them**, even when a ratio is 0. Setting Validation to 0 gives you
  a `val` split with 0 records rather than no `val` split.
- **Re-splitting replaces the previous split entirely.** The old partition is discarded, every
  record is reassigned, and the dataset's version number goes up by one. The previous split is
  not recoverable, so export before you re-split if the old one produced results you care about.

---

## Export

Both export buttons respect the split filter. Filenames are the dataset name, with the split
appended when one is selected.

**CSV** writes the schema column names as the header, in schema order, and one row per record.
Values are stringified and properly quoted — commas, quotes and newlines inside a value are
escaped. A record missing a column exports an empty cell.

**JSON** writes an indented array of the raw record objects, exactly as stored, keys and all.

Because CSV is built from the schema, **any column that is not in the schema is dropped from the
CSV export** while surviving in the JSON. If the validation report told you about columns "found
in records but not defined in schema", those are the columns you are about to lose. Export JSON.

JSONL exists in the API and has no button.

---

## Why this matters for research

An evaluation number is only meaningful against a named subset of a named dataset. "We got 84%
on the test set" is an anecdote if the test set was whatever 20% the code happened to pick that
afternoon; it is a result if the split is stored, labelled, versioned and produced by a seed you
recorded.

That is the whole value proposition of this page, and it costs you one dialog and one integer.
Set the seed, note it in the experiment's hypothesis or description on the
[Experiments](experiments.md) page, and export the JSON alongside your results.

---

## What this page will not do

- **Column Statistics shows blank column names and no top values.** See above.
- **Deleting a dataset is immediate and silent.** The native confirm is the only prompt, there
  is no undo, and you are returned to the list with no message. Every record and split goes
  with it.
- **No editing or annotating records.** Both exist in the API, neither has a UI. The records
  table is read-only.
- **No rename or re-description** of a dataset after upload, and no way to set a column's
  **Purpose**.
- **The record count is not recalculated.** It is the number of rows parsed at upload and is not
  revisited when you split.
- **The CSV export drops columns missing from the schema**, which in a ragged file means the
  columns that appear only in later records. JSON keeps them.
- **No JSONL export button.**
- **No preview before upload.** You find out what the parser made of your file by loading it and
  reading the Statistics tab.
- **No upload progress** for large files, and no useful error above roughly 30 MB.

---

## See also

- [Experiments](experiments.md) — where a split earns its keep
- [Evaluation](evaluation.md) — running a dataset against a model

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | Splits are reproducible | **Not by default.** The seed field is optional and empty, and the handler falls back to an unseeded `Random`. Two splits with identical ratios differ unless you type a seed | `SplitDatasetDialog.tsx:24`; `SplitDatasetHandler.cs:60` |
| P2 | The Statistics tab names each column and lists its top values | **No.** The backend sends `columnName` and a map of top values; the frontend reads `column` and an array — so names render blank and top values never appear. No crash; it just says less than it claims | `DatasetStatsDto.cs:22-27` vs `datasets/types.ts:60-71` |
| P3 | The records table shows the whole record | **Not necessarily.** It renders only the columns in the schema, and the schema is inferred from row 0 alone, so later rows with extra keys are hidden | `UploadDatasetHandler.cs:55,191-198` |
| P4 | An empty grid means there are no datasets | **Not when the API is down** — there is no error branch | `DatasetsPage.tsx:41-56` |
| P5 | The Purpose column means something | **Not for uploads.** Schema detection never sets it, and there is no UI to set it, so every uploaded dataset shows "—" | `UploadDatasetHandler.cs:191-198` |
| P6 | `sizeBytes` is how much storage this uses | No — it is the uploaded file's length; records live in Postgres | `UploadDatasetHandler.cs:65` |
| P7 | A split filter follows you between datasets | No, deliberately — it is cleared on mount and again when the label does not exist here | fixed 2026-08-09 |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | A cold install shows the seeded dataset with its record count and split badges | open `/datasets` | MET |
| R2 | Searching by a case-insensitive substring narrows the grid | type `sentiment` | MET |
| R3 | Selecting a split shows exactly the count its badge claims | click `test (2)`; read "Showing 1–2 of 2" | MET |
| R4 | Opening another dataset while filtered to a split it lacks shows all its records | filter one, open another | MET |
| R5 | Exporting while filtered to a split downloads only that split | filter, export, open the file | MET |
| R6 | Deleting a dataset returns to the list without an error | trash → confirm; fixed by the 204 handling in `apiClient` | MET |
| R7 | A column more than half null is reported as an error, 10–50% as a warning | `ValidateDatasetTests` | MET |
| R8 | A failed dataset request says so rather than "No datasets yet" | none | **UNMET** |
| R9 | The Statistics tab names each column and lists its top values | none — see P2 | **UNMET** |
| R10 | A successful upload confirms on screen | none — the dialog only clears its fields and cannot close itself | **UNMET** |
| R11 | A dataset can be renamed, or a column's purpose set | none — `useUpdateDataset` and its endpoint exist with no callers | **UNMET** |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | Records can be edited in place | `useUpdateRecord` has no callers and the table is read-only by design | this review |
| W2 | Records can be annotated | The endpoint, handler and migration exist with no frontend at all. Either build it or drop the columns — the schema currently carries fields nothing populates | this review — flagged, not decided |
