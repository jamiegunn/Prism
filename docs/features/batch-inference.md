# Batch Inference

**Push a whole dataset through one model, once, and keep every result.**

Batch Inference is the unglamorous workhorse: no scoring, no comparison, no analysis. It takes
each record in a dataset, sends it to a model as a single user message, and stores what came
back along with the latency and token counts. Every call also lands in [History](history.md) and
[Analytics](analytics.md) — though batch calls are recorded without a module label, so in both
places they appear under `unknown` rather than under Batch Inference. Search by model name to
find them.

Sidebar: **Batch Inference**.

---

## There is no create button

Like [Evaluation](evaluation.md), this page lists jobs and controls them but cannot start one.
The API is the only entry point.

```bash
curl -X POST http://localhost:5000/api/v1/batch \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "3f1c2a10-8b4e-4f6a-9d21-7c5e0b9a1234",
    "splitLabel": null,
    "model": "llama3.1:8b",
    "promptVersionId": null,
    "parameters": null,
    "concurrency": 1,
    "maxRetries": 3,
    "captureLogprobs": false
  }'
```

Send every field. The body is deserialised into a record with no defaults, so omitted fields
become nulls and zeroes rather than sensible values.

`datasetId` comes from `GET /api/v1/datasets`. `splitLabel` restricts the run to one split, or
`null` for the whole dataset. `maxRetries` controls how many times the *whole job* is re-queued
if it crashes — not how many times an individual record is retried; there is no per-record
retry. Because the runner skips records that already succeeded, a re-queued job resumes rather
than paying for the same work twice.

The response is `201` with the job, which appears on the page immediately.

---

## Before you start

You need a dataset with records ([Datasets](datasets.md)) and at least one registered inference
instance ([Model Management](models.md)).

As with Evaluation, **the runner does not let you choose the instance** — it takes the first
registered one it finds and sends everything there. The `model` you name is passed through to
that instance, so it has to be a model that instance can serve.

The prompt is read from the first non-empty of `input`, `prompt`, `question`, `instruction`,
`text` on each record. Nothing else is looked at. A record with none of those fields is sent as
an empty prompt rather than skipped.

---

## The jobs list

Five filter buttons across the top: **All**, **Queued**, **Running**, **Completed**, **Failed**.

> **There is no Paused filter and no Cancelled filter.** A job you pause disappears from every
> view except **All**, which reads exactly like the job vanishing. It has not; switch to **All**
> to find it again.

Each job is a card. **The card title is the model name**, because jobs have no name of their
own — two runs of the same model against different datasets are indistinguishable on this page.
Under the title: records completed out of total, failed count, tokens used, and the concurrency
setting. A progress bar appears while the job is Queued or Running.

**The list does not auto-refresh.** Reload the page to see progress. Clicking one of the control
buttons does refresh the list as a side effect, which can make it look as though refreshing
works when it does not.

> **Known display bug: progress renders as a percentage of a percentage.** The stored value is
> already 0–100 and the page multiplies by 100 again, so a half-finished job reads `5000%`. The
> `completed / total` record count next to it is right.

---

## The buttons

Four icon-only buttons appear on the right of a card, with no tooltips and no labels. Which ones
you get depends on the status:

| Icon | Appears when | What it does |
|---|---|---|
| Pause (two bars) | Running | Stops the job at the next record boundary. |
| Play (triangle) | Paused | Sets the badge back to Queued. See the warning below. |
| Cancel (circled X) | Queued, Running or Paused | Stops the job permanently. |
| Retry (circular arrow) | Completed or Failed, and only if some records failed | See the warning below. |

Pause and cancel genuinely work. The worker re-reads the job's status from the database before
every single record, so a pause or a cancel takes effect within one record rather than at the
end of the run. Results already produced are kept, and the tokens you stopped spending stay
unspent. This is the one control on the page that does what it says.

> **Resume does not work.** Clicking Play flips the badge from Paused back to Queued and nothing
> else happens. Getting a job running again requires a worker to claim it, and claiming happens
> from a queue entry that resume never creates. The job sits at Queued forever.
>
> **Retry Failed does not work either, and it destroys information on the way.** It sets the
> job back to Queued — same dead end — and before it does, it resets the failure count to zero
> and erases the recorded error message from every failed record. You are left with a job that
> will never run again and no record of why it failed the first time.

Treat **Pause** as "stop". If you want the remaining records processed, submit a new job with
the same body; the runner skips records that already have a successful result, so you pay only
for what is left.

Do not click **Retry Failed** unless you have already read the errors, which means exporting the
results or reading the backend log first.

---

## Concurrency does nothing

`concurrency` is accepted by the API, stored on the job, and displayed on the card. **The runner
ignores it and processes records strictly one at a time.**

This matters beyond the wasted setting: the cost and time estimator divides its time estimate by
the concurrency you gave it, so a job submitted with `"concurrency": 8` is estimated as eight
times faster than it will be. Setting it to 1 at least makes the estimate self-consistent.

`captureLogprobs` is real but provider-dependent: it asks the provider for per-token
probabilities and stores them alongside a computed perplexity. **On Ollama it produces nothing**
— no logprobs, no perplexity, no error — because Ollama does not return them. vLLM does. See
[Model Management](models.md) for which provider gives you what.

`parameters` is stored and never applied. There is no temperature control on a batch job.

---

## Estimating before you run

There is a cost estimator, and like everything else here it is API-only:

```bash
curl -X POST http://localhost:5000/api/v1/batch/estimate \
  -H "Content-Type: application/json" \
  -d '{
    "datasetId": "3f1c2a10-8b4e-4f6a-9d21-7c5e0b9a1234",
    "splitLabel": null,
    "model": "gpt-4o-mini",
    "concurrency": 1
  }'
```

It returns the record count, an estimated token total, an estimated duration in minutes, and a
cost in USD or `null`.

Understand what you are being told, because every step is an approximation:

- It reads **only the first 50 records** and averages their prompt length. A dataset whose long
  records are at the end will be badly underestimated.
- Tokens are estimated at **four characters per token**. Reasonable for English prose,
  optimistic for code, JSON or non-Latin scripts.
- It assumes **the completion is the same length as the prompt**. This is a guess and nothing
  more.
- Time assumes **one record per second per unit of concurrency**, which bears no relation to
  your hardware, and concurrency does not work anyway.
- Cost is only computed for a fixed list of hosted models: the `gpt-4` family (including
  `gpt-4-turbo`, `gpt-4o`, `gpt-4o-mini`), `gpt-3.5-turbo`, and `claude-3-opus`, `-sonnet` and
  `-haiku`. Version suffixes such as `gpt-4-0613` match by prefix. **Anything else returns
  `null` rather than zero**, which is the honest answer — an unpriced model is not a free one.

Use it to tell a ten-minute job from a ten-hour one. Do not use it to plan a budget.

---

## Getting the results out

There is no download button on the page. The endpoint:

```bash
curl -o batch.jsonl \
  "http://localhost:5000/api/v1/batch/<job-id>/download?format=jsonl"
```

`format` is required and must be `csv`, `json` or `jsonl`. Omitting it returns a 400.

All three formats carry the same fields: record ID, input, output, tokens used, latency, and
perplexity where logprobs were captured.

> **Only successful records are exported.** Failed records are filtered out of every format.
> Their error messages are visible nowhere in the UI and nowhere in the download, so if a run
> failed on 12 records out of 500, the only place the reason exists is the backend log — and
> clicking Retry Failed deletes it from the database as well.

For paginated browsing rather than a file, `GET /api/v1/batch/{id}/results` accepts a `status`
filter and returns failures too.

---

## Why bother

Running a dataset through a model once, completely, with every call recorded, is the boring
foundation under most of the interesting work. It gives you outputs to read, a token total you
did not have to estimate, and a latency distribution over hundreds of real prompts rather than
the three you tried by hand. Because every call goes through the same recording path as the rest
of Prism, the run shows up in [History](history.md) for inspection and in
[Analytics](analytics.md) for aggregate latency and throughput — filed under `unknown`, as
above.

If you also want the outputs scored against references, that is [Evaluation](evaluation.md)
instead — same shape of job, with metrics attached.

---

## What this page will not do

- **No way to create a job from the UI.** API only.
- **No choice of inference instance**, and no per-job parameters — `parameters` is discarded.
- **No auto-refresh.** Reload to see progress.
- **The progress readout is wrong and then disappears.** The percentage is multiplied by 100,
  so a job a tenth of the way through reads 1000%. It is only rendered while the job is Queued
  or Running, so you never see the completed value — a finished job shows no progress at all.
- **`concurrency` has no effect**, and makes the time estimate optimistic by its own factor.
- **Resume is broken.** Play returns the job to Queued and nothing runs it.
- **Retry Failed is broken and lossy.** It zeroes the failure count and erases the errors.
- **Paused and Cancelled jobs are only visible under the All filter.**
- **No job names.** Cards are titled by model.
- **No download button**, and failures are never exported.
- **`captureLogprobs` produces nothing on Ollama**, without saying so.

---

## See also

- [Datasets](datasets.md) — building the input and naming the prompt column
- [Evaluation](evaluation.md) — the same run with scoring attached
- [History](history.md) — every individual call from a batch, inspectable
- [Analytics](analytics.md) — aggregate tokens and latency
- [Model Management](models.md) — which provider returns logprobs
