# Prompt Lab

**Design, version, and test prompts with variables and few-shot examples.**

The Prompt Lab treats a prompt as an artefact with a history rather than as text you retype
into a chat box. Each template carries numbered versions, and each version bundles the system
prompt, the user template, the declared variables and the few-shot examples together. When you
test a template you are testing one specific version, which means a result you write down later
points at an exact piece of text.

Sidebar: **Prompt Lab**.

---

## Before you start

Browsing templates works with nothing configured. Testing one needs a registered model
instance — see [Model Management](models.md).

**Read this before anything else: both editors on this page are read-only.** The System Prompt
and User Template panes look like editable code editors, they are Monaco, they have a cursor,
and they will not accept a keystroke. The only way to change prompt text is the **New Version**
dialog. This is deliberate — versions are immutable once written — but it is the single most
confusing thing about the page, and people spend a while clicking into the editor before they
work it out.

The second thing to know is in [Creating a template](#creating-a-template): a template you
create through the UI cannot declare variables, and a template with an undeclared `{{variable}}`
cannot be tested. Read that section before you create anything.

---

## The layout

Three regions, no tabs.

The **left rail** searches and filters. The **centre** shows the selected template's content.
The **right column** holds the variable inputs on top and the test panel underneath. Nothing
appears in the centre or right until you pick a template.

---

## Find a template

The search box at the top of the left rail (**Search templates...**) filters by name. Below it
sits a row of category chips — **All**, plus one chip per category that exists across your
templates. Clicking an active chip clears it.

The chips are built from the templates currently listed, not from every template you have, so
selecting one collapses the row to that single chip. You cannot hop straight from `extraction`
to `development`; go back through **All** first.

Each row shows the template name and its current version as `v{n}`, with the description and
first three tags underneath. Prism ships with two seeded templates: **Structured Data
Extractor** (category `extraction`, at v2) and **Code Review Assistant** (category
`development`, at v1).

Your selected template and category chip persist in the browser across reloads.

---

## Read a template

The centre header shows the name, the category badge and the tag badges, and on the right:

| Control | What it does |
|---|---|
| **Diff** / **Hide Diff** | Opens the version comparison panel. Only appears when the template has more than one version. See the warning below — it does not work. |
| **Fork** | Copies the currently selected version into a brand-new template. |
| Version dropdown | Switches which version you are looking at. Any notes recorded with a version show next to its number. |
| **New Version** | The only way to change prompt text. |

The body below has up to three sections. **System Prompt** appears only if the version has one.
**User Template** is always there. **Few-Shot Examples (N)** appears only if the version carries
examples, and lists each one as an Input/Output pair with its optional label.

---

## Change a prompt

Click **New Version**. The dialog opens pre-filled with the current version's system prompt and
user template, so you edit rather than start over. There is a **Notes** field — use it. The note
is what shows in the version dropdown, and "tightened the JSON instruction" is worth a great
deal more six weeks later than "v4".

Saving creates the next version number and switches you to it. Nothing is overwritten; every
earlier version stays readable through the dropdown.

> **Creating a new version strips the template's variable declarations.** The dialog only sends
> the system prompt, the user template and the notes. If you make a v3 of the Structured Data
> Extractor, v3 will have no declared `text` or `fields` variables, the Variables panel will go
> empty, and testing v3 will fail with `Undeclared variables in template: text, fields`. The
> earlier versions are untouched and still testable. There is no way around this from the UI.

---

## Variables

The **Variables (N)** panel at the top right renders one text input per declared variable, with
a badge showing its type (`string`, `number`, `boolean`) and a red `required` marker where the
variable is mandatory. A variable's default value shows as the input's placeholder, and its
description sits above the box.

If the version declares nothing, the panel reads "No variables declared."

Variable values are not saved with the template. They live in the page and reset when you switch
templates — which is what **Input Sets** are for.

---

## Test a version

The **Test** panel underneath the variables does the actual inference.

**Quick Test (single)** is a dropdown of your registered instances plus a play button. Pick one,
click play, and the result appears in the Results stack below.

**Compare (multi)** is a checkbox list of every instance. Tick two or more and click **Test N
Instances**. They run *sequentially*, not in parallel — the button reads "Running N..." and each
model waits for the one before it. Four instances against a slow local model is a genuine wait.
If one instance fails, you get an error notification for that one and the rest carry on.

| Control | Default | Range | Notes |
|---|---|---|---|
| **Temperature** | 0.7 | 0–2, step 0.1 | Set to 0 if you want to compare two prompt versions and attribute the difference to the prompt. |
| **Top P** | 0.9 | 0–1, step 0.05 | |
| **Max Tokens** | 2048 | 1–8192, **step 64** | The step starts from 1, so the reachable values are 1, 65, 129 and so on. 2048 is not one of them. Once you drag this slider you cannot return to exactly 2048 without clearing the stored settings. |

The instance, temperature, top-p and max-tokens choices persist in your browser. The **Results**
stack does not. Each result card shows the instance name, the model, latency, total tokens,
throughput and the output text; **Clear All** empties the stack, the X on a card removes one.
Navigating away from Prompt Lab loses all of them. If a result matters, copy it out before you
leave.

### Input sets

The **Input Sets (N)** button saves the current variable values under a name you type in the box
next to it. Click the button to list what you have saved, click a name to load it back, click
the trash icon to remove it.

Input sets are stored in your browser and never sent to the server. They are not shared with
anyone, they are not part of the template, and clearing site data deletes them. They are a
convenience for re-running the same three test cases, not a fixture library.

---

## Fork a template

**Fork** copies the currently selected version — system prompt, user template, **variables** and
few-shot examples — into a new template as its v1. The new template is named `{name} (fork)`,
keeps the original's category and description, and gains a `forked` tag. Its v1 notes record
what it was forked from. You are switched to it immediately.

Fork is the only operation in this UI that preserves variable declarations. If you want a
working, variable-bearing template of your own to test against, forking a seeded one is the
way to get it.

---

## Creating a template

**New Template** in the top right opens **Create Prompt Template**.

| Field | Required | Limit |
|---|---|---|
| **Name** | yes | 200 characters |
| **Category** | no | 100 characters |
| **Tags (comma-separated)** | no | split on commas, whitespace trimmed |
| **Description** | no | 2000 characters |
| **System Prompt (optional)** | no | — |
| **User Template** | yes | uses `{{variable}}` syntax |

> **The dialog cannot declare variables, and this breaks testing.** The form has no field for
> them and sends none. So if you write `Summarize the following text: {{text}}` — which is the
> dialog's own placeholder text — the template is created successfully and then every attempt to
> test it fails with:
>
> ```
> Undeclared variables in template: text
> ```
>
> There is no UI anywhere that adds a variable declaration to an existing template.
>
> **What to do about it.** Two options, both real:
>
> 1. **Write templates with no placeholders.** Put the full literal prompt in the User Template.
>    Everything on the page works: versioning, forking, testing, multi-instance comparison. You
>    lose parameterisation.
> 2. **Create the template outside the UI.** `POST /api/v1/prompts` accepts a `variables` array
>    alongside `userTemplate`, with a name, type, required flag, default and description for
>    each. Templates created that way show up in the left rail immediately and their Variables
>    panel works properly. This is currently the only way to get a parameterised template of
>    your own, and remember that creating a v2 of it through the UI will strip the declarations
>    again.
>
> In practice: the UI supports templates with no variables. Everything else goes through the API.

---

## Comparing versions

> **The Diff panel does not work.** It opens, shows a version dropdown and "vs v{current}", and
> then asks the server for a comparison at an address that does not exist. You get "Loading
> diff..." followed by "Select a version to compare.", and no diff, whichever versions you pick.
> This is a wiring fault, not a data problem — the two versions are both intact and both
> readable through the version dropdown. To compare them today, switch between them and read.

---

## Why this matters for research

Two things here are worth the friction.

The first is attribution. A number in your notes that says "GPT-OSS-20B got 71% on the
extraction task" is worth very little if the prompt that produced it was a paragraph in a chat
window you have since edited. A number that says "v3 of Structured Data Extractor" points at a
specific, immutable, timestamped piece of text that you or a reviewer can read back. That is the
whole argument for versioning prompts, and it costs you one dialog per change.

The second is few-shot examples. Examples are part of the prompt — they change the output
distribution as surely as the instruction does — and they are usually the part that gets pasted
in ad hoc and lost. Here they are stored inside the version, so "the prompt" and "the three
examples we were using at the time" cannot drift apart. Notice, though, that as with variables
you can only put examples into a version through the API.

---

## What this page will not do

- **No editing of prompt text in place.** New Version or nothing.
- **No variable declarations from the UI**, on create or on new version. See above.
- **The Diff panel never loads.** See above.
- **No rename, re-tag, re-categorise or delete** of a template. Both operations exist in the API
  and neither has a button. A template created by mistake stays in the list.
- **No A/B testing UI.** The API can run a matrix of prompt variations across instances and
  parameter sets and write the results into an experiment. There is no screen for it at all.
- **No streaming.** A test blocks until the full response arrives. There is no partial output and
  no way to stop early.
- **No logprobs.** The test path does not request them and the result cards do not show them. For
  token-level analysis use the [Playground](playground.md).
- **"Save as run" is not wired up.** The test call can attach its result to an experiment as a
  run, but the Test panel never sends the experiment. This is why the Experiments run table's
  empty state tells you to "create runs from the Prompt Lab" when you cannot — use the parameter
  sweep on the [Experiments](experiments.md) page instead.
- **Test results vanish on navigation.** They are not written to History.

---

## See also

- [Experiments](experiments.md) — parameter sweeps, run comparison and export
- [Playground](playground.md) — free-form chat with token-level confidence analysis
- [Model Management](models.md) — registering the instances the test panel needs

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | A page called an editor lets you edit | **No.** Both editors are read-only; changes go through New Version | `TemplateEditor.tsx:144,166` |
| P2 | Writing `{{text}}` in the create dialog declares a variable | **No.** The dialog sends no `variables` and nothing extracts them from the body, so the template saves and every test then fails with "Undeclared variables" — including for the dialog's own placeholder | `CreateTemplateDialog.tsx:58-66`; `TemplateRenderer.cs:44-55` |
| P3 | New Version preserves what the current version declared | **No.** It pre-fills the prompt text but sends no variables or few-shot examples, so v2 of a working template is untestable | `VersionSelector.tsx:41-47`; `CreateVersionHandler.cs:51-52` |
| P4 | Fork preserves variables | **Yes** — and it is the only UI path that keeps a parameterised template working | `ForkTemplateHandler.cs:43-56` |
| P5 | Test uses the model named on the template | **No** — it uses the selected instance's model, so the same version tests different models depending on the dropdown. Correct, but unstated | `TestPromptHandler.cs:99` |
| P6 | Compare (multi) runs instances in parallel | **No** — an awaited loop, one at a time | `TestPanel.tsx:133-156` |

P2 and P3 together mean the variables feature is reachable only by forking something that already had them.

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | Selecting a template shows its latest version's prompt, variables and examples | click a seeded template | MET |
| R2 | Template search filters as you type, with no Apply step | type "code" | MET |
| R3 | Testing shows output, latency, tokens and the model that produced it, and results persist for comparison | run a Quick Test | MET |
| R4 | A failed test states why | select an offline instance and run | MET |
| R5 | Input Sets survive a reload and reload values in one click | save a set, reload, apply | MET |
| R6 | Fork produces a template that is immediately testable | fork a seeded template and test it | MET |
| R7 | A template created with `{{variable}}` in its body can be tested | none — "Undeclared variables in template" | **UNMET** |
| R8 | Creating a new version leaves a parameterised template testable | none — variables are not carried forward | **UNMET** |
| R9 | Diff shows the differences between two versions | none — the client calls `/versions/diff`, the route is `/diff`; 404, and the panel shows its placeholder rather than an error | **UNMET** |
| R10 | A persisted category filter can always be cleared | none — the "All" badge only renders when the filtered result yields categories, so a stale category strands the list | **UNMET** |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | Two prompt versions can be A/B tested from this page | `useAbTest` and a complete backend handler exist with no UI. Either build it or delete the hook and record the endpoint as API-only — the tour no longer claims it | this review — flagged, not decided |
