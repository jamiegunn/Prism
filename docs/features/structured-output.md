# Structured Output

**Make a model return JSON that fits a schema you wrote — and find out honestly whether it did.**

The page has two halves. On the left you keep a list of JSON Schemas. On the right you point one
of them at a model, send a prompt, and get back the model's output with a verdict attached: it
either satisfied the schema or it did not, with a list of exactly which parts failed.

The verdict is the product here. The JSON is easy; knowing whether you can trust a thousand more
like it is the hard part.

Sidebar: **Structured Output**. Page heading **Structured Output**, subtitle *JSON schema guided
decoding and validation*.

---

## Before you start

You need a registered model instance. If the Models page is empty, go there first — see
[Model Management](models.md).

**You will also need that instance's GUID as text, and the page has no dropdown.** Both the
**Instance ID** and **Model name** boxes are plain text inputs that you type or paste into. The
Models page does not print the GUID anywhere, so fetch it from the API:

- With the app running, open `http://localhost:5173/api/v1/models/instances` in a browser tab
  and copy the `id` of the instance you want. The dev server proxies that path to the backend.
- Or open the Swagger UI at `http://localhost:5000/swagger` in a development build and run
  **GET /api/v1/models/instances** there.

The **Model name** is the model string your server expects — the same one you would put in an
OpenAI-style request body, such as `meta-llama/Llama-3.1-8B-Instruct` or `mistral:7b`. It is not
validated until the call reaches your inference server.

**One thing decides how strong the guarantee is:** whether your provider supports guided
decoding. vLLM does. Ollama, LM Studio and every other OpenAI-compatible server do not, and on
those the schema becomes a polite request rather than a constraint. This is explained in full
below, and it matters more than anything else on the page.

The page starts empty on a fresh install. Nothing is seeded.

---

## Create a schema

Click **New Schema** to open the **Create JSON Schema** dialog.

| Field | Required | Notes |
|---|---|---|
| **Name** | Yes | The only field that gates the **Create** button. This is what the search box matches against. |
| **Description** | No | Shown under the name in the schema card. Not searchable. |
| **JSON Schema** | Yes | A monospaced textarea, prefilled with a working example. |

The prefilled example is a small object schema, and it is the whole of the template library:

```json
{
  "type": "object",
  "properties": {
    "name": { "type": "string" },
    "age": { "type": "integer" }
  },
  "required": ["name"]
}
```

Edit it into the shape you actually want. The dialog parses what you typed before sending it; if
it is not valid JSON you get a browser alert reading *Invalid JSON schema* and nothing is saved.
That check is a JSON parse, not a schema check — a document that parses but means nothing as a
schema will save happily and misbehave later.

Write your schema against the subset the validator understands. The table further down says
which keywords are enforced, which are refused outright, and which are quietly ignored.

Saved schemas appear as cards down the left column, most recently updated first, under a
**Search schemas...** box. Each card shows the name, the description underneath it, a `v{n}`
badge, and a trash icon that deletes the schema after a browser confirmation. Selecting a card
outlines it and loads it into the panel on the right. With no schemas saved you get an empty
state reading *No schemas yet*.

---

## Run a schema against a model

Click a schema card in the left column. The right-hand panel switches from *Select a schema to
test* to the test panel, which shows the schema pretty-printed and read-only, then:

1. Paste the instance GUID into **Instance ID** and the model string into **Model name**.
2. Type a prompt in the textarea below them.
3. Click **Run Structured Inference**. The button reads *Running…* while the call is in flight.

> **The button lights up as soon as the prompt box has text**, before you have filled in the
> instance or the model. Clicking it in that state does nothing at all — no error, no spinner,
> no request. If the button seems dead, one of the two boxes above it is empty.

Only your prompt is sent as a user message. There is no system prompt field, no conversation, no
multi-turn. Each run is independent.

Every run is recorded in [History](history.md) with a source module of `structured-output`, so
you can go back and look at the raw call later even though this page keeps nothing.

---

## Reading the result

Below the button you get a status line, then the detail.

| Element | Meaning |
|---|---|
| Green tick, **Valid JSON** | The output parsed and satisfied every keyword the validator enforces. |
| Red cross, **Validation Failed** | Something failed, or the schema used a keyword the validator refuses to guess at. |
| `1234ms · 567 tokens` | Wall-clock latency of the whole call, and prompt plus completion tokens added together. |
| Red box | One line per validation error, each prefixed with the path it applies to. |
| Code block | The parsed JSON, pretty-printed. If the output could not be parsed, the raw text appears here instead so you can see what the model actually said. |

### The red box can appear under a green badge

When the provider does not support guided decoding, the result carries an explanatory note:

```
Note: <instance name> does not support guided decoding, so the schema was requested by
instruction rather than enforced during generation.
```

That note is added to the same list the validation errors live in, and the page renders that
list in a red-bordered box whenever it is non-empty. **So a perfectly valid run on Ollama shows
a green "Valid JSON" badge with a red box underneath it.** Read the first line before you
conclude anything went wrong. If the only line in the box starts with `Note:`, nothing failed.

---

## Constrained decoding versus asking nicely

This distinction is the reason the page exists, and it is worth being precise about.

On **vLLM**, the schema is sent as `guided_json`. The server compiles it into a grammar and
masks the token distribution at every step, so tokens that would break the schema have zero
probability of being sampled. Output that violates the schema is not unlikely — it is
unreachable. If the model has nothing sensible to say it will produce schema-shaped nonsense,
but it will produce schema-shaped nonsense.

On **Ollama**, **LM Studio** and **OpenAI-compatible** servers, Prism does not send the schema as
a constraint. Instead it prepends a system message:

```
Respond with a single JSON document and nothing else. No prose, no code fences.
It must conform to this JSON Schema:
<your schema>
```

and sets a JSON-mode response format. On OpenAI-compatible servers that response format nudges
the output towards being parseable JSON; it says nothing about your fields. On Ollama it is
dropped entirely, so the system message is the only thing doing any work. In all three cases the
model is being *asked*, and the validator afterwards is what tells you whether it complied.

For a research claim the difference is not cosmetic. "The model produced conforming output in
100% of runs" means one thing when conformance was structurally impossible to violate and
something quite different when it was a behaviour you observed. If you are measuring a model's
ability to follow a format, guided decoding destroys the measurement — the vLLM number is a
property of the decoder, not the model. If you are extracting data and you want it clean, guided
decoding is what you want. Decide which experiment you are running before you pick the provider.

---

## What the validator checks

The validator is a deliberate subset of JSON Schema. Its stated principle is that a partial
check reported as a pass is worse than an honest refusal, and it behaves accordingly.

### Enforced

| Keyword | Behaviour |
|---|---|
| `type` | Standard type names. An array of names passes if any one matches, and the error reads `expected type 'string or null'`. An unrecognised type name matches nothing, so a typo in your schema fails every document. |
| `integer` | Accepts `1.0` as well as `1`. The test is mathematical, not lexical: any number with no fractional part counts. |
| `enum` | Deep equality against each listed value, so objects and arrays in an `enum` work as expected. |
| `required` | Checked on objects. Reports one error per missing property. |
| `properties` | Recursive. Every property present in both the schema and the document is validated in full, at any depth. |
| `additionalProperties: false` | Reports one error per unexpected property. Only the literal `false` triggers it; a sub-schema value is ignored. |
| `minItems` / `maxItems` | Array length. |
| `items` | Recursive, applied to every element. Only the single-schema form works — see the note below. |
| `minLength` / `maxLength` | String length in .NET characters, which is UTF-16 code units. Emoji and other astral characters count as two. |
| `minimum` / `maximum` | Inclusive numeric bounds, compared as doubles. |

### Refused with an error

| Keyword | Behaviour |
|---|---|
| `$ref`, `allOf`, `anyOf`, `oneOf`, `not`, `if`, `patternProperties`, `dependentSchemas` | Each produces `schema uses '<keyword>', which this validator does not support. The result cannot be trusted either way.` |

This is by design, and it is worth understanding what it costs you. A schema containing any of
these keywords reports **Validation Failed on every single run**, no matter how good the output
is, because the validator will not pretend to have checked something it did not check. If you
need composition or references, flatten the schema by hand before you save it.

The refusal is emitted at the path where the keyword sits, so a nested `anyOf` gives you
`$.address: schema uses 'anyOf'…` and points straight at the branch to rewrite.

### Not implemented

`pattern`, `format`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf`, `uniqueItems`,
`const`, `propertyNames`, `default`.

These are neither enforced nor refused. They sit in your schema, they are sent to vLLM (which
does honour several of them during guided decoding), and Prism's own verdict ignores them
completely. A document with a `pattern` violation will be reported valid by this page.

Anything else outside both lists behaves the same way: silently ignored. `contains`,
`minProperties`, `maxProperties`, `then`, `else` and `$defs` are all in that category.

One subtlety worth knowing: tuple-form `items` — an array of schemas rather than one schema —
falls into the ignored bucket rather than the refused one, because the validator skips anything
whose schema node is not an object. Positional array validation therefore passes vacuously.

### How errors read

Errors are path-prefixed with a JSON-path-like string rooted at `$`:

```
$.author: missing required property 'name'.
$.citations[0].year: expected type 'integer' but found 'string'.
$: unexpected property 'notes' (additionalProperties is false).
```

When a value has the wrong type, the validator stops descending at that node. A string where an
object was expected gives you one clear error rather than a cascade of complaints about every
property the string does not have. This makes the error list readable, but it also means fixing
one type error can reveal a second layer of errors on the next run.

---

## Settings you cannot change

| Setting | Value | Note |
|---|---|---|
| **Temperature** | 0.1 | Fixed server-side. Low, because the point is conformance rather than variety. |
| **Max tokens** | 2048 | Fixed server-side. A schema that produces longer documents will be truncated, which shows up as a JSON parse error. |

The API accepts both as optional overrides, but this page never sends them, so there is no way
to change them from the UI.

---

## Why a researcher would use this

The case for the page is extraction at volume. You have several hundred abstracts, transcripts
or ticket bodies, and you want the same six fields out of each one so they can go into a table.
Writing the schema once and running it over the corpus gives you rows instead of prose.

The validator is what makes that a measurement rather than a chore. A schema violation is not a
nuisance to be silently patched over — it is a signal that this particular document confused the
model, and the error path tells you which field it choked on. Counting failures per field across
a corpus is a real result. So is comparing failure rates between two models on the same schema,
provided you run both on the same provider so that the constraint story is identical.

The schema the create dialog pre-fills is deliberately trivial. Real use starts when your schema encodes the
distinctions your study actually cares about.

---

## What this page will not do

- **Schemas cannot be edited after creation.** There is no Edit button and no update endpoint.
  The `v{n}` badge on every card reads `v1` forever, because nothing ever increments it. To
  change a schema, create a new one and delete the old.
- **No import and no export.** Schemas are typed into the dialog and read out of the panel.
  There is no file upload, no download, and no way to move a schema between installs other than
  copying the text.
- **No library and no templates** beyond the one prefilled example in the create dialog.
- **The search box is case-sensitive and matches names only.** Searching `invoice` will not find
  a schema called *Invoice Fields*, and a word that appears only in the description will never
  match.
- **Nothing is saved on this page.** Results vanish when you select a different schema or reload.
  The underlying inference call is in [History](history.md); the verdict, the errors and the
  parsed JSON are not stored anywhere.
- **One prompt, one turn, no system prompt.** There is no batch mode here. Running one schema
  over many inputs means clicking **Run Structured Inference** once per input, or calling
  `POST /api/v1/structured-output/schemas/{id}/infer` yourself.
- **The failure mode of a bad instance GUID is a raw error.** A GUID that does not exist returns
  a not-found from the API and the panel shows nothing useful.

---

## See also

- [Model Management](models.md) — which provider gives you guided decoding, and where instances
  are registered
- [History](history.md) — every structured inference call is recorded there automatically
- [Datasets](datasets.md) — where extracted records usually want to end up
- [Playground](playground.md) — for exploring a prompt before you commit it to a schema run
