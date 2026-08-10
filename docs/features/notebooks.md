# Notebooks

**Keep your analysis notebooks next to the runs they analyse, versioned, instead of loose in a
downloads folder.**

The page's subtitle says *Research notebooks with JupyterLite — Python in the browser*. Read the
next section before you believe it.

Sidebar: **Notebooks**.

---

## The Python part does not work yet

**The JupyterLite build is not in this repository.** The folder it is meant to live in,
`frontend/public/jupyterlite/`, contains two files — a build script and a Python helper module —
and none of the built application. Both **Open in JupyterLite** and the **Embed** panel point at
`/jupyterlite/lab/index.html`, which does not exist.

What you see depends on how you are running the frontend. Under the Vite dev server, any unknown
path falls back to the single-page app, so the button opens a new tab containing Prism, and the
Embed panel renders Prism inside an iframe inside Prism. On a static host with no such fallback,
both give you a 404.

**There is no Python execution anywhere in Prism today.** Not in the iframe, not on the server,
not in a kernel behind the API. Nothing on this page runs code.

What the page does do is worth having on its own, and it is described below: it stores, shows,
edits, versions and downloads real `.ipynb` files, with the notebook body held server-side. The
intended workflow today is to draft or store here and run in real Jupyter.

### What it would take to fix

This is a missing build step, not a missing design. The pieces are all present:

1. Install the build tools — `pip install jupyterlite-core jupyterlite-pyodide-kernel`.
2. Run `frontend/public/jupyterlite/setup.sh`, which calls `jupyter lite build`.
3. Make the built application available at the path the page requests. The script as written
   builds into `./output`, which would put the app at `/jupyterlite/output/lab/index.html`,
   while the page asks for `/jupyterlite/lab/index.html`. Either build into the folder directly
   or change the two URLs in the detail page to match — they disagree today, so following the
   script's own closing instructions is not quite enough.
4. Arrange for `workbench.py` to be part of the JupyterLite contents, so that `import workbench`
   resolves inside the kernel. Nothing does this automatically.

---

## Before you start

Nothing. No model, no instance GUID, no dataset. The page works with the API and the database
alone.

The list is empty on a fresh install — no notebooks are seeded.

---

## Create and find notebooks

**New Notebook** opens a dialog with two fields: **Name**, which is required and gates the
**Create** button, and **Description**, which is optional. Creating a notebook takes you
straight to its detail page.

A new notebook is not empty. Prism generates a valid nbformat 4 document (minor version 5) with
a Pyodide kernelspec, a markdown cell containing your notebook's title, and a starter code cell
of commented-out example calls. That means a notebook created here and downloaded immediately
opens cleanly in Jupyter.

> The commented example in the starter cell reads `workbench.chat('model-name', 'Hello!')`,
> which is not the real signature — `chat` takes an instance ID, a model and a prompt. Use the
> signatures listed further down.

The list page shows a card per notebook with its name, description, version, size in KB and the
date it was last edited. Each card carries a **Download** icon that fetches the `.ipynb`
directly without opening the notebook, and a trash icon that deletes it after a confirmation.
The **Search notebooks...** box matches names and descriptions, case-insensitively.

---

## Read a notebook

The detail page opens in **View Cells** mode. Under the title sits a metadata strip showing the
version, the size in KB, the kernel name and when it was last edited, and three buttons:
**Edit JSON**, **Download** and **Open in JupyterLite**.

Cells are rendered in order, each with a type label — `code` in blue, `markdown` in green — an
execution count where one is recorded, the source, and any stored output underneath. Text
outputs and `text/plain` representations are shown; images and rich MIME types fall back to
their raw JSON.

**This view is read-only.** You cannot add a cell, delete one, reorder them, change a cell type
or run anything. It is a reader.

---

## Edit a notebook

**Edit JSON** is the only editing surface. It swaps the cell list for a single monospaced
textarea containing the entire raw `.ipynb` document, with **Save** and **Cancel** underneath.
**Cancel** discards your changes and returns to the cell view.

Every **Save** increments the version number and updates the size and the last-edited timestamp.
There is no history and no way back to a previous version — the number counts saves, it does not
let you retrieve them.

> **Nothing validates the JSON on save.** Not the browser, not the API, not the database. Any
> text at all is accepted as the notebook body, and the save succeeds with no complaint.
>
> The consequence is quiet. Return to **View Cells** after saving something malformed and you
> get *No cells in this notebook* — which is precisely what a valid notebook with an empty cell
> list also shows. **There is no way to tell broken from empty from that screen**, and the
> download will hand you the same broken text with an `.ipynb` extension.
>
> Before hand-editing, click **Download** and keep the copy. If a notebook goes blank after a
> save, switch back to **Edit JSON** — the raw text is still there and still yours to fix.

Renaming is possible through the API but there is no field for it in the UI.

---

## Getting notebooks in and out

**Download** returns a genuine `.ipynb` file — the stored document, served as
`application/x-ipynb+json`, with spaces in the name replaced by underscores. It opens in real
JupyterLab, in VS Code, in nbconvert, in anything that reads notebooks. Since nothing here
executes code, this is the intended workflow: keep the notebook in Prism, run it in Jupyter,
paste the updated version back.

**There is no upload.** To bring in a notebook you already have:

1. Click **New Notebook** and give it a name.
2. On the detail page, click **Edit JSON**.
3. Select everything in the textarea and paste your file's contents over it.
4. Click **Save**.

Check the cell view afterwards. If it says *No cells in this notebook*, the paste was truncated
or the file was not what you thought — see the warning above.

---

## The `workbench` helper

`frontend/public/jupyterlite/workbench.py` is a small Python module that wraps the Prism API so
a notebook can pull data from the workbench it lives in. It imports `pyodide.http` at module
level, which means **it only runs inside a Pyodide kernel — that is, inside JupyterLite**. It
will not import in CPython, so you cannot use it from a local Jupyter installation, and since
the JupyterLite build is absent it cannot currently be used anywhere at all. It is also not
installed or copied into any environment automatically.

For when the build lands, this is what it provides. Every function except `help` is async and
must be awaited.

| Function | What it returns |
|---|---|
| `chat(instance_id, model, prompt, **kwargs)` | The assistant's reply as a string. Accepts `system_prompt`, `temperature`, `max_tokens` and `logprobs` as keyword arguments. |
| `logprobs(instance_id, model, prompt, top_logprobs=5)` | A list of per-token logprob records for the prompt, requested with a max-token limit of 1. |
| `get_experiment(experiment_id)` | An experiment as a dictionary. |
| `get_dataset(dataset_id)` | Dataset metadata as a dictionary. |
| `get_dataset_records(dataset_id, page=1, page_size=100)` | A paged result containing the records. |
| `list_models()` | Every registered inference instance. This is also the easiest way to find an instance GUID from inside a notebook. |
| `list_collections()` | Every RAG collection. |
| `rag_query(collection_id, query, top_k=5, search_type="Hybrid")` | Matching chunks with scores. `search_type` accepts `"Vector"`, `"Bm25"` or `"Hybrid"`. |
| `help()` | Prints the list above. The only synchronous function — call it without `await`. |

That list is complete and accurate as of the current file.

> **Known defect: `chat()` and `logprobs()` accept a `model` argument and never send it.** Both
> build a request body containing the instance ID, the message and the sampling options, and
> `model` is silently dropped. The call therefore runs against whatever model the instance
> resolves by default, not the one you named. On a single-model server this is invisible; on a
> server hosting several, you will get results from the wrong one with nothing to indicate it.

---

## Why a researcher would use this

A study generates artefacts in two places: the runs themselves, which live in Prism, and the
analysis of those runs, which usually lives in whatever notebook you had open at the time. Six
weeks later the runs are still findable and the notebook is `Untitled7 (3).ipynb` in a downloads
folder, possibly with a different set of cells than the one that produced the figure.

Storing the notebook alongside the runs fixes the association, and the version counter gives you
a rough sense of how much it has changed since you last looked. Downloading it to run it and
pasting it back is more friction than a real integration would be — but it is less friction than
reconstructing which version of the analysis matched which experiment.

The versioned, server-side copy is also the shareable one. A colleague looking at your
experiment can read the cells in the browser without installing anything.

---

## What this page will not do

- **No Python execution.** Not in the browser, not on the server. See the top of this page.
- **Open in JupyterLite and Embed do not work.** Under the dev server they render Prism inside
  itself; on a static host they 404.
- **The cells view is read-only.** No add, delete, reorder, retype or run.
- **Editing means editing raw JSON**, and nothing validates it on save. Broken and empty look
  identical afterwards.
- **No version history.** The counter goes up; previous versions are not kept.
- **No upload.** Paste into the JSON editor instead.
- **No rename in the UI**, though the API supports it.
- **No output rendering beyond text.** Images, HTML and plots stored in a notebook show as raw
  JSON in the cell view — download and open it in Jupyter to see them.
- **The `workbench` module is not installed anywhere**, will not import outside Pyodide, and
  drops the `model` argument in two of its functions.
- **No link between a notebook and an experiment, dataset or run.** The association is whatever
  you write in the description.

---

## See also

- [Experiments](experiments.md) — the runs a notebook usually exists to analyse
- [Datasets](datasets.md) — the GUIDs `get_dataset_records` wants
- [Model Management](models.md) — the instance GUIDs `chat` and `logprobs` want
- [History](history.md) — the record of every inference call, for offline analysis

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | A JupyterLite build is being served at `/jupyterlite/lab/` | **No.** The build is generated, not committed | `frontend/.gitignore` |
| P2 | A missing build fails visibly | **It did not.** Vite answers unknown paths with the SPA shell, so the iframe rendered Prism inside Prism | fixed 2026-08-09 |
| P3 | CI shipping the build means users receive it | **False.** This CI has no deploy step — it retains only test results and the Playwright report | `.github/workflows/ci.yml` |

P3 is why building in CI was necessary but not sufficient. Prism is a local tool; the person who
opens this page is running `dev.sh`, and a discarded CI artifact never reaches them.

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | Notebooks are stored server-side and versioned on save | manual: save twice, version counter increments | MET |
| R2 | A real `.ipynb` can be downloaded, without opening the notebook | per-card download button; `GET /notebooks/{id}/download` | MET |
| R3 | The JupyterLite build is verified to build | CI runs `setup.sh` and asserts `lab/index.html` exists | MET |
| R4 | The build is available to someone running locally | `npm run jupyterlite`, and `dev.sh` runs it on first start when `jupyter` is present | MET |
| R5 | A missing build is reported, never silently substituted | browser check: the page detects the SPA shell, disables Embed, refuses to open the tab, names the command | MET |
| R6 | A missing kernel does not imply storage is broken | same check asserts the notice says storing, versioning and downloading still work | MET |
| R7 | Saved notebook JSON is validated before it is stored | none — invalid JSON saves, and renders identically to an empty notebook | **UNMET** |
| R8 | An existing `.ipynb` can be uploaded | none — the only route in is create-then-paste through Edit JSON | **UNMET** |

### Future options for shipping the build

Recorded because R4 solves the local case only, and a deployed Prism has the same problem:

1. **A deploy step.** Nothing is published today. Pages, a container image or a release artifact
   would each let the CI build reach a user, and would make P3 true rather than false.
2. **Ship it in a release artifact and have `dev.sh` fetch it**, avoiding a Python toolchain on
   every developer machine. Heavier to set up, lighter for the reader.
3. **Leave it optional and local**, which is where it now stands: the page is honest when the
   build is absent, so a deployment without it is degraded rather than broken.

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | The notebook communicates with Prism over `postMessage` | The listener exists, nothing ever posts the message, and the received state is discarded. It is a stub, not an integration | this review |
