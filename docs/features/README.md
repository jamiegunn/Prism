# Feature guides

How to use each part of Prism. These are task-oriented: what the feature is for, the steps to
do the common jobs, what every setting actually means, and — stated plainly in each one — what
it will not do.

For how the code is put together, see [ARCHITECTURE.md](../../ARCHITECTURE.md) and the
[ADRs](../ADR/). For the machine-readable status of each module, see
[product-truth.yaml](../product-truth.yaml).

---

## Start here

If you have just installed Prism and want to get to something interesting quickly:

1. **[Model Management](models.md)** — connect an inference server. Nothing else works until
   you have. Prism will look for one on the usual local ports and offer to connect it.
2. **[Playground](playground.md)** — send a prompt and read the token-level confidence of the
   answer. This is the shortest path to seeing what Prism is for.
3. **[Token Explorer](token-explorer.md)** — force the model down a path it did not choose and
   watch what changes.

**One thing decides how much of Prism works for you: whether your provider returns per-token
probabilities.** vLLM does and supports everything. Ollama does not, which leaves the heatmaps,
entropy views and Token Explorer empty. [Model Management](models.md) has the full comparison.

---

## By what you are trying to do

### Understand a single generation

| Guide | Use it for |
|---|---|
| [Playground](playground.md) | Chat with confidence colouring, entropy, surprise highlighting and a per-token inspector |
| [Token Explorer](token-explorer.md) | Next-token distributions, stepping one token at a time, forcing alternatives, comparing branches |
| [Tokenizer](tokenizer.md) | How text splits into tokens, and how differently two models split the same text |

### Run something at scale

| Guide | Use it for |
|---|---|
| [Datasets](datasets.md) | Upload, inspect, validate and split the data you will run against |
| [Batch Inference](batch-inference.md) | Push a whole dataset through a model |
| [Evaluation](evaluation.md) | Score model outputs against references and compare models |
| [Experiments](experiments.md) | Parameter sweeps, run comparison, and exporting results |

### Build and test a prompt or pipeline

| Guide | Use it for |
|---|---|
| [Prompt Lab](prompt-lab.md) | Versioned prompt templates with variables and few-shot examples |
| [Structured Output](structured-output.md) | Constrain generation to a JSON schema and validate the result |
| [RAG Workbench](rag-workbench.md) | Ingest documents, chunk them, and debug retrieval quality |
| [Agent Builder](agents.md) | ReAct agents with tool use and a readable reasoning trace |

### Look back at what you ran

| Guide | Use it for |
|---|---|
| [History](history.md) | Every inference call from every module, searchable, taggable, replayable |
| [Analytics](analytics.md) | Usage, token totals and latency percentiles across the last 30 days |
| [Notebooks](notebooks.md) | Store and version the `.ipynb` files that analyse your runs |

### Prepare training data

| Guide | Use it for |
|---|---|
| [Fine-Tuning](fine-tuning.md) | Export a dataset as Alpaca, ShareGPT, ChatML or OpenAI JSONL |

### Organisation

| Guide | Use it for |
|---|---|
| [Workspaces](workspaces.md) | A workspace selector that does not yet scope anything — read before relying on it |

---

## Honesty about state

These guides describe what the code does today, not what it is meant to do. Where a feature is
incomplete, the guide says so at the point where you would hit it rather than in a footnote.
The short version:

- **Evaluation** and **Batch Inference** have working background workers but no create form —
  both guides give the exact `curl` you need.
- **Batch Inference** pause and cancel work; **resume and retry do not**.
- **Fine-Tuning** exports training data correctly. It does not train, and a registered LoRA
  adapter never reaches an inference call.
- **Notebooks** stores and versions `.ipynb` files. The JupyterLite build is not in the
  repository, so nothing executes in the browser.
- **Workspaces** is a dropdown that does not filter anything yet.
- **Analytics** has a Cost tab that does not report real costs.

If a guide and the product disagree, the guide is wrong — please open an issue.
