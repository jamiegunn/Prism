# Agents

**Give a model a few tools, watch it reason its way through a question, and keep the trace.**

An agent in Prism is a model that has been told it may call tools, in a loop, until it has an
answer. What you get back is not only the answer but every thought, every tool call and every
observation that led there — stored, replayable, and awkward to argue with.

Sidebar: **Agents**. Page heading **Agent Builder**.

---

## There is no YAML

**Other documents in this repository describe an agent workflow defined by a YAML config
block. That format does not exist in the running product.** The project plan describes it; the
implementation does not have it. There is no config file to write, no directory to drop agent
definitions into, and no import.

Agents are created by filling in a form in the **Create Agent Workflow** dialog, and everything
about them lives in the database from that moment on. If you came here looking for the config
file, stop looking — there is not one.

---

## Before you start

You need a registered model instance, and you need its GUID as text. The **Instance ID** field
in the create dialog is a plain input with the placeholder *Instance GUID*, and there is no
dropdown to pick from. The Models page does not display GUIDs, so read one from the API:

- Open `http://localhost:5173/api/v1/models/instances` with the app running and copy the `id`.
- Or use the Swagger UI at `http://localhost:5000/swagger` in a development build.

**Model** is the model string your inference server expects, typed by hand alongside it.

An agent that is going to do anything interesting also wants something to search. The
`rag_query` tool needs at least one RAG collection to exist. Everything else works with no
further setup.

Prism seeds one workflow, **Research Assistant**, on a fresh install. Read the caution about its
run history further down before you take it at face value.

---

## Create a workflow

Click **New Workflow** to open the **Create Agent Workflow** dialog.

| Field | Default | Notes |
|---|---|---|
| **Name** | empty | Required. |
| **Description** | empty | Shown on the workflow card. |
| **Instance ID** | empty | Required. Raw GUID, typed. |
| **Model** | empty | Required. Model string, typed. |
| **System Prompt** | `You are a helpful AI research assistant.` | Your instructions. The ReAct format instructions and the tool list are appended to this automatically, so write about the task rather than about the output format. |
| **Pattern** | `ReAct` | Dropdown offering `ReAct` and `Sequential`. See the warning below. |
| **Max Steps** | 10 | Hard stop on loop iterations. |
| **Token Budget** | 8000 | Hard stop on total tokens across the run. |
| **Tools** | none checked | A checkbox per registered tool, with its description. |

> **`Sequential` is not implemented.** Selecting it stores the value, shows it on the workflow
> card and in the detail header, and then runs ReAct anyway. There is no separate executor. The
> label is the only thing that changes.

**Temperature is fixed at 0.7 and has no control in the dialog.** The value is held in the form
but never exposed, and the API takes whatever the form sends. The one exception is the seeded
**Research Assistant** workflow, which was created directly in the database at 0.3. If you need
a deterministic agent, this feature cannot currently give you one.

---

## How a ReAct run actually works

Understanding the loop makes the trace readable, and the trace is the reason to use this page.

Prism builds a system prompt from your text plus a fixed instruction block that tells the model
to answer in exactly this shape:

```
Thought: <reasoning about what to do next>
Action: <tool_name>
Action Input: <input to the tool>
```

and, when it is ready to stop:

```
Thought: <final reasoning>
Final Answer: <the answer>
```

The list of tools you ticked, with their descriptions, is appended so the model knows what it
may call.

Each iteration then goes: send the conversation, parse the reply for those four markers, and if
an `Action` was named, run that tool with the `Action Input` and append the result to the
conversation as `Observation: …`. Repeat. The loop ends when the model emits a **Final Answer**,
when **Max Steps** iterations have passed, or when the accumulated token count reaches the
**Token Budget**. Each individual call is capped at 1024 tokens or whatever is left of the
budget, whichever is smaller.

Parsing is done with regular expressions over plain text, which has two consequences worth
knowing. A model that drifts from the format — wrapping its answer in JSON, or writing
*Action:* in the middle of a sentence — will be mis-parsed rather than rejected. And a reply
that matches none of the markers is stored as a bare thought, so the loop burns a step and
continues.

**Why the trace matters.** A wrong answer from a plain chat model is a black box: you can see it
is wrong and guess why. A wrong answer from an agent has a paper trail. You can see that step 2
searched for the wrong phrase, that step 3 got back a chunk about something else, and that step
4 confidently built on it. The failure is attributable to a specific tool call with a specific
input. That is the difference between "the model hallucinated" and "the retrieval returned the
wrong document and the model trusted it", and only one of those is a finding you can act on.

---

## The tools

Ticking a tool makes it available to the model; the model decides whether to call it.

| Tool | What it does | Input format |
|---|---|---|
| `calculator` | Evaluates an arithmetic expression and returns the number. Supports `+ - * / %` and parentheses. | The bare expression, e.g. `(2 + 3) * 4`. Input is checked against a restricted character set — digits, whitespace, `+ - * / % . ( )` — and anything else is rejected outright, so no function names, no variables, no `^`. |
| `echo` | Returns its input unchanged. | Any text. Useful for testing that the loop and the trace work before you wire up anything real. |
| `api_call` | Performs an HTTP GET and returns `Status: <code>` followed by the response body. 30-second timeout; the body is truncated at 4000 characters with `... [truncated]` appended. | An absolute `http` or `https` URL. Anything else is rejected. |
| `rag_query` | Searches a RAG collection and returns the top 5 chunks, each with its source filename and score. Hybrid search, fixed at top-5. | `collection_id|query`, or a bare `query`. |

`rag_query` deserves a note on its fallback. If the input has no `collection_id|` prefix, the
tool picks a collection for you — and despite the tool's own description saying "first available
collection", the code orders by creation date descending, so it uses the **most recently
created** collection. On an install with more than one collection, an agent that omits the
prefix is searching whichever corpus you happened to ingest last. Put the GUID in the prompt if
it matters.

### Read this before enabling `api_call`

> **`api_call` has no URL allow-list, no blocklist, and no network policy.** It will fetch any
> absolute HTTP or HTTPS URL the model asks for, from the backend process, using the backend's
> network position.
>
> That includes `http://localhost:…` and other loopback services, anything on your private
> network, the Docker bridge, your database's admin UI if it has one, and cloud instance
> metadata endpoints such as `169.254.169.254` — which on an unpatched cloud VM hand out
> credentials to anyone who asks.
>
> Two rules follow. **Do not enable `api_call` on a machine whose network reach you care
> about.** And **do not feed an agent untrusted text while `api_call` is enabled** — a document
> the agent retrieves or a question a colleague pastes in can instruct it to fetch a URL and
> report back what it found, and the loop will comply. The observation is written into the run
> record, so exfiltrated data ends up stored as well as returned.
>
> On a laptop with a local model and no secrets, this is fine. On a shared or cloud-hosted
> deployment, treat the checkbox as an outbound proxy with no rules attached.

### Hardening this later

Leaving `api_call` open is a deliberate choice for a local research tool, not an oversight — an
allow-list you have to edit before you can fetch a paper is a tool nobody uses. But it is the
wrong default the moment Prism runs anywhere other than your own machine, and the change is
worth making before that day rather than after it.

The ordering below is by protection per unit of work. The first two are most of the benefit.

**1. Reject private address space, after resolution.** The check that matters is against the
resolved IP, not the hostname: blocking `169.254.169.254` by string does nothing against a DNS
name that resolves to it, and a name that resolves twice — once for your check, once for the
fetch — defeats a naive guard entirely. Resolve once, verify the address is public, then connect
to that address. Deny loopback, RFC1918, link-local, and IPv6 equivalents. This closes the
metadata-endpoint and internal-service cases together, and needs no configuration from anyone.

**2. Do not follow redirects.** A permitted public URL that 302s to `169.254.169.254` walks
straight through an address check applied only to the original request. Either disable redirects
and let the model see the 302, or re-run the address check on every hop.

**3. Make it opt-in per workflow, with the reach stated.** The tool checkbox already exists;
what it lacks is a scope. A workflow that only needs one documentation site should say so, and
the common case — an agent that needs no network at all — should be the default.

**4. Log the URL, not just the observation.** Runs record what came back but not always what was
asked for. A fetched-URL audit line per step turns "did this agent exfiltrate anything" from an
inference into a query.

**5. Cap calls per run.** The step limit bounds reasoning turns, not fetches. A run that makes
forty requests to one host is either broken or being used as a scanner, and neither should be
silent.

**6. Strip inbound credentials.** The tool should never forward Prism's own auth headers, cookies
or bearer tokens onto an arbitrary host. Today it constructs a bare client, which is the right
behaviour — it is worth a test so it stays that way.

A reasonable end state is deny-by-default with a per-workflow allow-list, the address check from
(1) applied regardless of the list, and an explicit "unrestricted" mode that is obvious in the UI
and refuses to run when Prism is not bound to loopback. That last part is what stops a laptop
default silently becoming a server default.

---

## Running an agent

Click a workflow card to open its detail page. Under the title you get a strip showing the
pattern, the model, `Max N steps`, the token budget and the version, then three tabs:
**Run Agent**, **Run History** and **Trace View**.

### Run Agent

Type into the textarea and click **Run Agent**. The run streams: as each step completes it
appears as a card under **Execution Trace**, so you watch the agent think in near real time.

Each card has a header — the step number, then either the tool name, or **Final Answer** in
green, or **Thinking** — with that step's token count and latency on the right. Click the header
to collapse it. The body shows whichever of these the step produced:

| Section | Contents |
|---|---|
| **THOUGHT** | The model's reasoning for this step. |
| **ACTION** | Rendered as `tool_name(input)`. |
| **OBSERVATION** | What the tool returned, in a scrollable block. |
| **FINAL ANSWER** | The answer, when the loop terminates. |
| **ERROR** | Set on inference failures and on budget or step-limit terminations. |

When the run finishes, a summary bar appears below the cards with the status, the step count,
the total tokens, the total latency, and the final output repeated in full.

### Run History

A row per previous run: the input truncated to fit, a coloured status pill (green **Completed**,
red **Failed**, amber for anything else), then the step count, tokens, milliseconds and
timestamp.

**Clicking a row takes you back to the Run Agent tab, not to the Trace View.** It loads that run
into the same panel the live stream uses, so you see its step cards and summary bar. Switching
to **Trace View** afterwards then shows that run's timeline — the trace tab reads from whatever
run is currently loaded, which is why it says *Run an agent first, then select a run to view its
trace* until you have done one of those two things.

### Trace View

The same steps drawn as a vertical timeline, one dot per step, each labelled **Thought**, **Tool
Call**, **Response** or **Error** and colour-coded to match. Steps start collapsed; click one to
expand its content, its tool input and its tool output.

The timeline is the better view for reading a long run end to end, and the step cards are the
better view for reading tool output in detail. They show the same data.

---

## Why a researcher would use this

Tool use turns a language model from something that answers from memory into something that goes
and looks. That is worth having, but it also multiplies the ways a result can be wrong: bad
retrieval, bad tool input, correct tool output misread, or a fine chain of reasoning that ran
out of steps one call short of the answer.

What this page gives you is that every one of those failures is on the record. The steps are
stored as part of the run, alongside the input, the answer, the token cost and the latency, so a
run you did last week can be reopened and read rather than re-run. When you report that an agent
answered 60% of your questions correctly, you can also say which tool call broke the other 40%.

The same property makes it useful for prompt work. Change the system prompt, run the same
question, and compare traces rather than answers — the point where the two runs diverge is
usually the thing your edit actually changed.

---

## What this page will not do

- **A failed run shows you nothing.** If the request errors, the **Run Agent** button stops
  spinning and the page stays exactly as it was. The error is written to the browser console and
  nowhere else. If a run appears to do nothing, open the developer console — that is where the
  message went.
- **The Run History tab does not refresh after a run.** The run is saved, but the list is not
  re-fetched. Navigate away and back, or reload, to see it.
- **A workflow cannot be edited after creation.** There is an update endpoint in the API and no
  UI that calls it. To change a model, a tool selection or a system prompt, create a new
  workflow.
- **There is no cancel button.** Once a run starts, it runs until it finishes, hits **Max
  Steps**, or exhausts the **Token Budget**. Closing the tab abandons the stream.
- **The seeded Research Assistant's example run is fabricated.** Its three steps, its
  observations about the Transformer paper, its 450 tokens and its 3200ms were written into the
  seeder by hand. No model produced them and no tool was called. Delete it before you use run
  counts for anything.
- **An unknown tool name is not an error.** If the model invents a tool, the observation reads
  `Error: Unknown tool 'x'. Available tools: …` and the loop continues with that text as its
  observation, which usually recovers but costs a step.
- **Budget and step-limit terminations look like success.** Both produce a final-answer step, so
  the **Trace View** paints them as a green **Response** and the summary bar can read
  *Completed*. The give-away is the **ERROR** line on the last step card reading *Token budget
  exceeded* or *Max steps exceeded*, and a final answer that apologises instead of answering.
  Check the last step before you record a result.
- **No sub-agents, no parallel tools, no human-in-the-loop.** One model, one tool per step, in
  sequence.
- **The search box on the workflow list matches names and descriptions**, case-insensitively —
  it is the run history that has no search at all.

---

## See also

- [Model Management](models.md) — registering the instance whose GUID you need
- [History](history.md) — each model call inside a run is recorded there, tagged `agents`
- [Playground](playground.md) — for working out the prompt before you wrap it in a loop

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | The seeded run's trace came out of a model | **No.** Every thought, observation, token count and latency is a string literal in the seeder | `AgentsSeeder.cs:38-83` |
| P2 | The seeded workflow can be run as shipped | **No on any machine without a GPU.** It is bound to the seeded vLLM instance on :8000, which `dev.sh` only starts under `--gpu` | `AgentsSeeder.cs:92-93`, `ModelsSeeder.cs:69-72` |
| P3 | …and can be repointed at a different server | **No.** There is no edit UI; the only recovery is delete and recreate | `useUpdateWorkflow` has no callers |
| P4 | Choosing "Sequential" changes how the agent runs | **No.** It is stored, echoed on the header, and never read — the executor always runs ReAct | `RunAgentHandler.cs:135`; no reader of `workflow.Pattern` |
| P5 | A run that failed is recorded as failed | **No.** A provider failure becomes a step carrying `Error`, and only an error on a *final answer* step sets `errorMessage` — so the run is stored Completed and badged green | `ReActExecutor.cs:130-139`, `RunAgentHandler.cs:186-214` |
| P6 | Workflows are scoped to the current project | **No.** `ProjectId` is never populated | `CreateWorkflowHandler` |
| P7 | Each agent step is recorded in History and Analytics | Yes — every step is a real call tagged `agents` | `ReActExecutor.cs:123` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | Creating a workflow never asks for a raw instance GUID | New Workflow → server dropdown | MET |
| R2 | Create is blocked until a server and model are chosen | fill only a name; Create stays disabled | MET |
| R3 | A finished run appears in Run History without a reload | run, switch tabs; `invalidateQueries` in `finally` | MET |
| R4 | A failed run surfaces to the user rather than the button just stopping | run with the inference server down; a toast fires | MET |
| R5 | Selecting a past run shows its full step trace | click the seeded run | MET |
| R6 | The inline "The run did not finish" panel appears on failure | none — the JSX is nested inside `runResult`, which is null on failure, so it is unreachable | **UNMET** |
| R7 | An SSE `error` event is reported | none — the endpoint answers 200 with `event: error`, so `!response.ok` never fires and the reader handles only `step` and `finished` | **UNMET** |
| R8 | A run whose model calls all failed is not badged Completed | none — see P5 | **UNMET** |
| R9 | A step that errored shows its error text in the trace | none — the mapper sets `type: 'error'` but never maps `s.error` into the body | **UNMET** |
| R10 | `AgentRun.errorMessage` is shown somewhere | none — the only reference is the type declaration | **UNMET** |

R6–R10 are one failure wearing five hats: the run path was hand-rolled inline rather than using
the `useRunAgent` hook that already exists and already handles errors and cache invalidation.

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | `api_call` is restricted to an allow-list | Deliberately open for a local research tool; the risk and a staged hardening path are set out above | decided 2026-08-09 |
| W2 | The Sequential pattern runs a different executor | The option should be removed rather than implemented — a dropdown that silently does nothing is worse than one choice | this review |
