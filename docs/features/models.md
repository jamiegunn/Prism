# Model Management

**Connect Prism to an inference server, and find out which half of the application that server
will let you use.**

Every other page in Prism runs against an *instance* — one inference server, serving one model,
at one endpoint. This page is where instances are registered, checked and removed. It is also
where you find out, before you waste an afternoon, that the provider you have chosen does not
return the data the feature you came for is built on.

Sidebar: **Models**. Page heading **Model Management**.

---

## First run: let it find your server

With no instances registered, the page shows a discovery screen headed **Connect a model to get
started** instead of an empty list.

It probes three addresses in parallel, allowing three seconds each:

| Address | Provider |
|---|---|
| `http://localhost:8000` | vLLM |
| `http://localhost:11434` | Ollama |
| `http://localhost:1234/v1` | LM Studio |

Anything that answers gets a card: its suggested name, an emerald **Full introspection** or
amber **Chat only** badge, the endpoint, the model it reports, and a **Use this** button that
registers it in one click. If Prism already knows that endpoint the button is disabled and reads
**Already added**. Under each card is a plain-language note about what that provider will and
will not give you — worth reading rather than skipping, because it is the difference between
heatmaps working and not.

If nothing answers, the screen names the three ports it tried and gives you the shortest path to
a working setup: install Ollama, run `ollama serve`, pull a model. It also tells you, correctly,
that if token-level introspection is what you came for then Ollama is not the answer and you
want vLLM.

**Search again** at the bottom re-runs the probe after you have started something.

> **On a fresh development database you will not see this screen.** A seeder inserts two
> fabricated instances — *Local vLLM (Llama 3.1 8B)* and *Local Ollama (Mistral 7B)* — pointing
> at localhost addresses that may have nothing behind them. Because instances exist, discovery
> never runs.
>
> Worse, the seeded Ollama row is recorded as supporting logprobs, which Ollama does not. Until
> you correct it, features will offer you analysis views that cannot possibly work against that
> instance.
>
> Fix it one of two ways: remove both seeded rows and reload the page to get the discovery
> screen, or select each one and click **Probe Capabilities**, which replaces the seeded
> guesses with what the endpoint actually reports.

---

## Register a server yourself

**Register Instance**, top right, opens a dialog headed **Register Inference Instance**.

| Field | Notes |
|---|---|
| **Name** | Free text, up to 100 characters. This is what appears in every instance dropdown in the application, so make it say which model it is. |
| **Endpoint URL** | Must be a complete URL. `localhost:8000` is rejected; `http://localhost:8000` is accepted. |
| **Provider Type** | vLLM, Ollama, LM Studio or OpenAI Compatible. Pick wrong and capabilities will be wrong. |
| **Tags** | Comma-separated, optional. |
| **Set as default instance** | Makes this the default and clears the flag on whichever instance held it. |

On save, Prism health-checks the endpoint and asks it what model it is serving, filling in the
model ID, context length and capability flags from the answer. If the endpoint is unreachable
the instance still registers — offline, with capabilities filled in from the provider type
alone.

Note what is missing: there is no model picker. Prism takes whatever the server reports. If your
server can hold several models, register it and then use **Swap Model** to choose.

---

## What each provider actually gives you

This is the table to read before you decide which server to run.

| | vLLM | Ollama | LM Studio / OpenAI-compatible |
|---|---|---|---|
| **Logprobs** | Yes, up to 20 alternatives | **No** | Yes, up to 5 |
| **Tokenize / detokenize** | Yes | No | No |
| **Guided decoding** | Yes | No, not through this feature | No |
| **Live metrics** | Yes (GPU, KV cache, queue) | No | No |
| **Model swap** | No | **Yes** | No |
| **Streaming** | Yes | Yes | Yes |

Ollama is the easiest thing to install and the least useful thing to introspect. It will chat
perfectly well and it will leave the heatmap, the entropy view, the Token Inspector, the whole
of the Token Explorer's prediction and branching tabs, and both tokenizer tabs empty or
erroring. If you are here to look inside a model, run vLLM.

The one thing Ollama does that vLLM does not is hot-swap the loaded model without a restart.

---

## The Capability Matrix

Above the instance grid sits a table with one row per instance: **Provider**, **Tier**, then
tick-or-cross columns for **Logprobs**, **Tokenize**, **Guided**, **Stream**, **Metrics**,
**Swap**, **Multi** and **Tools**. Hover any column heading for what it means. **Refresh**
re-reads the stored values.

Tier is a summary of the row:

| Tier | Means |
|---|---|
| **Research** | Logprobs, tokenize, guided decoding and metrics all present. In practice this means vLLM and nothing else. |
| **Inspect** | Logprobs or tokenize, but not the full set. |
| **Chat** | Neither. Text in, text out. |

> **Most of this table is not measured.** Only **Tokenize**, the health check and **Metrics**
> are verified by an actual call to your endpoint. **Logprobs**, **Guided**, **Stream** and
> **Swap** are copied from a fixed table of what each provider type normally does. A tick in
> those columns means "servers of this type usually support this", not "we checked yours".
>
> **Tools** and **Multi** are hardcoded to false for every provider. Their crosses carry no
> information — do not read them as "your server cannot do this".

A small amber question mark next to a provider name means the last probe reported a problem;
hover it for the message.

---

## Reading the instance cards

Each registered instance is a card in the grid: name, a status dot, the provider badge, a
**Default** badge where applicable, the endpoint, the model ID, and capability badges along the
bottom. Where the provider supplies metrics, the card also shows GPU utilisation and a KV cache
gauge.

| Dot | Meaning |
|---|---|
| **Online** (pulsing green) | Last health check succeeded |
| **Degraded** (amber) | Answering, but not healthy |
| **Offline** (red) | Last health check failed |
| **Unknown** (grey) | Never checked — newly registered or seeded |

Health checks run automatically for every instance every 30 seconds, starting ten seconds after
the backend boots. Metrics refresh every five seconds. So after restarting the stack, allow up
to half a minute before an instance is marked Online — some features, notably history replay,
will not offer you an instance until it is.

Click a card to open its detail panel.

---

## The detail panel

> **The detail panel is hidden on narrow windows.** It lives in a right-hand column that
> disappears below the `lg` breakpoint. Every button described below goes with it — including
> **Remove** and **Probe Capabilities** — and nothing tells you they exist. If the actions have
> vanished, widen the browser window.

The panel gives you five things:

**Details** — endpoint, provider, model, GPU config, max context, the default badge and the
tags.

**Live Metrics** — GPU utilisation, GPU memory, KV cache, active requests, requests per second
and queue depth. vLLM only; everything else shows *Metrics not available for this provider*.

**Capabilities** — a tick list of seven flags: Logprobs, Streaming, Metrics, Tokenize, Guided
Decoding, Multimodal and Model Swap. Note this is one short of the matrix, which also has a
**Tools** column; the detail panel omits it.

**Last Health Check** — a timestamp, plus the error text if the last check failed.

**Actions** —

| Button | What it does |
|---|---|
| **Health Check** | Runs a check immediately rather than waiting for the 30-second cycle. |
| **Probe Capabilities** | Re-tests the endpoint and rewrites the stored capability flags and tier. This is the button that repairs a seeded or misregistered instance. |
| **Swap Model** | Reveals a model ID box and a **Swap** button. **Ollama only** — it does not appear for other providers. |
| **Remove** | Turns into **Confirm Remove**. There is no undo. |

Note that **Probe Capabilities** overwrites the **Last Health Check** timestamp. That field
really means "last time we touched this endpoint", whichever button did it.

---

## Why this page matters to your results

An instance in Prism is a claim about provenance. Every inference recorded in
[History](history.md) points at the instance that produced it, and "which model, on which
server, with which capabilities" is the part of an experiment you cannot reconstruct afterwards
from the output alone.

Two consequences worth acting on. First, name instances so the name identifies the model and the
build — `vllm-llama31-8b-awq` survives being read three months later; `Local vLLM` does not.
Second, probe an instance before you start a run rather than after, because the capability
flags decide silently whether perplexity and per-token traces get recorded at all.

---

## What this page will not do

- **You cannot edit a registered instance.** No rename, no endpoint change, no tag edit, and no
  way to move the default flag to a different instance after the fact. Remove and re-register is
  the only path, and it is not a rename — it is a new instance with a new ID.
- **You cannot choose a model at registration time.** Prism takes whatever the endpoint reports.
  Ollama users who want a specific model must register first, then **Swap Model**.
- **Removing an instance leaves history behind.** Records in [History](history.md) keep pointing
  at the deleted instance ID. The runs are not lost, but the instance they name no longer
  resolves, and replaying them against the same configuration is no longer possible.
- **Discovery only knows three ports.** A server on a non-standard port, or on another machine,
  must be registered by hand.
- **The capability matrix is mostly declared, not tested** — see the warning above.
- **"Last Health Check" is really "last probed"**, because Probe Capabilities overwrites it.
- **There is no bulk anything.** One instance at a time.

---

## See also

- [Playground](playground.md) — what logprob support changes there
- [Token Explorer](token-explorer.md) — the page that needs vLLM most
- [Tokenizer and Compare](tokenizer.md) — vLLM-only, both tabs
- [History](history.md) — where every call against every instance is recorded
