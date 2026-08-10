# Playground

**Chat with a model and see what it was thinking while it answered.**

The Playground looks like a chat window, and for the first minute that is all it is. The
difference shows up after the model replies: every token it produced can be coloured by how
confident it was, and clicking any one of them tells you what it nearly said instead.

Sidebar: **Playground**. Keyboard shortcut **Ctrl+Shift+P**.

---

## Before you start

You need a registered model instance. If the Models page is empty, go there first — Prism will
look for a local inference server and offer to connect it. See [Model Management](models.md).

**One thing decides how much of this page works:** whether your provider returns per-token
probabilities. vLLM does. Ollama does not. LM Studio does, but only five alternatives per
token.

Without them you get a perfectly good chat window and none of the analysis. No heatmap, no
entropy view, no Token Inspector, no perplexity. The buttons do not appear at all — so if this
page looks plainer than the screenshots, that is why.

---

## Send your first message

1. Pick an instance from the **Model / Instance** dropdown in the right-hand **Parameters**
   panel. Nothing sends until you do; the message box stays disabled.
2. Type in the box at the bottom. **Enter** sends, **Shift+Enter** starts a new line.
   Replies appear **newest first**, at the top — the opposite of a normal chat window, so the
   response you just generated is the one you are looking at rather than something you have to
   scroll down to find.
3. Watch it stream in. The Send button turns into a red square while the model is generating —
   click it to stop early. Whatever arrived before you stopped is kept.

A `~N tokens` counter appears as you type. It is a rough estimate — characters divided by four —
not a real tokenizer count. For an exact count, use the [Tokenizer](tokenizer.md).

---

## Read what the model was confident about

Under any assistant reply that carries probability data you get five buttons:

| Button | What you see |
|---|---|
| **Heatmap** | Every token coloured green through red by how likely the model thought it was. Green means it was sure. Red means it was guessing. |
| **Entropy** | Colours by how *spread out* the alternatives were. A token can be low-probability but unsurprising — entropy separates "the model picked an unlikely word" from "the model had no idea". |
| **Surprises** | Highlights only tokens the model gave under a 10% chance. The fastest way to find where a fluent answer stopped being grounded. |
| **View Logprobs** | Expands an analysis block under the message, with **Heatmap**, **Entropy** and **Surprise** tabs. Click a token in it to list that token's alternatives. |
| **Open in Panel** | Sends this message to the full-width Logprobs panel at the bottom of the page — the same three tabs, with more room. Useful for a long response. |

Hover any coloured token to see its exact probability, its log-probability, its entropy, and
its top five alternatives.

### Where hallucinations show up

A model that is confabulating usually still *sounds* fluent. What changes is the confidence
profile: proper nouns, dates and numbers that it invented tend to be low-probability tokens
sitting in otherwise green text. Turn on **Surprises** and read only what lights up. If a
citation, a figure or a name is red, treat it as unverified.

The opposite is also worth knowing: high confidence is not correctness. A model can be
confidently wrong about something it was trained on incorrectly. Confidence tells you how
strongly the training distribution favoured that token, nothing more.

### The Token Inspector

Click any token in a heatmap to open the inspector on the right. It shows that token's
logprob, probability, entropy and position, badges it as `Surprise` / `Confident` /
`Uncertain` / `Very Uncertain`, and lists the alternatives ranked by probability. The **←** and
**→** buttons walk through the response one token at a time, and the **Context** strip lets you
jump to any token five either side.

> **Entropy is measured over the alternatives you asked for, not the whole vocabulary.** With
> **Top Logprobs** at its default of 5, the highest entropy that can ever be reported is about
> 2.32 bits, because that is the entropy of five equally likely options. Raise the slider to 20
> and the same token will report a higher number for the same generation. The values are
> comparable within one setting and not across two.
>
> The `Confident` / `Uncertain` / `Very Uncertain` badges are cut at fixed thresholds of 0.5 and
> 1.5 bits, and those do **not** move with the slider. So raising **Top Logprobs** pushes
> measured entropy up against a fixed line, and more tokens get badged `Very Uncertain` without
> the model having become any less certain. Pick a setting and stay on it.

---

## Parameters

Everything here persists in your browser and survives a reload. **Reset to Defaults** puts them
back — and also clears the selected instance and the system prompt.

| Control | Default | Range | What it does |
|---|---|---|---|
| **Temperature** | 0.7 | 0–2 | Flattens the distribution. 0 is near-deterministic; above ~1.2 it starts picking genuinely unlikely tokens. |
| **Top P** | 0.9 | 0–1 | Nucleus sampling: only consider tokens inside the top 90% of probability mass. |
| **Top K** | 50 | 1–200 | Only consider the 50 most likely tokens. |
| **Max Tokens** | 2048 | 1–32768 | Hard cap on response length. If replies stop mid-sentence, this is usually why — check the finish reason under the message. |
| **Stop Sequences** | none | text, Enter to add | Generation halts when any of these appears. |
| **Frequency Penalty** | 0 | −2 to 2 | Penalises tokens by how often they have already appeared. |
| **Presence Penalty** | 0 | −2 to 2 | Penalises any token that has appeared at all. |
| **Logprobs** | on | toggle | Turning this off disables every analysis view on this page. |
| **Top Logprobs** | 5 | 1–20 | Alternatives recorded per token. See the entropy caveat above. |

For reproducible comparisons set **Temperature** to 0. Two runs at the same temperature above 0
will differ, and the difference is not evidence of anything.

---

## System prompts

The collapsible **System Prompt** strip sits above the chat. The **Library** button next to it
saves prompts you reuse and ships with five: General Assistant, Code Helper, JSON Extractor,
Research Analyst, Concise Responder. Hover any saved prompt to rename or delete it.

The library lives in your browser only. It is not shared, not synced, and clearing site data
removes it.

---

## Conversations

The left rail lists everything you have run. Search filters by title. Click a conversation to
load its full history; hover a row and click the trash icon to delete it, with an inline
confirm.

**New Conversation** starts a fresh thread. **Export** downloads the current one as JSON,
including the per-token probability data — useful if you want to do your own analysis, and the
only way to get logprobs out of this page.

---

## Statistics

A statistics column appears to the left of the parameters once a conversation has messages,
showing message count, token totals, average latency and throughput, then a per-response
breakdown with latency, time-to-first-token, throughput, perplexity and finish reason. The
**Stats** button in the header toggles it — it is on by default, so your first click hides it.

**Perplexity** is the exponential of the mean negative log-likelihood — roughly, "how many
equally likely options was the model choosing between, on average". Lower means more confident.

> Perplexity is not comparable across models with different tokenizers. A model that splits
> text into more, smaller tokens will generally report a lower perplexity on the same passage
> without being any better. Compare perplexity between runs on the same model, not between
> models.

---

## Comparing models side by side

The **Compare** button in the header opens Multi-Pane Comparison: up to four instances, one
prompt, sent to all of them at once. Add instances with the dropdown, type once, click **Send
All**.

Use it for "does the 8B model actually need to be a 70B", or for checking whether a quantised
build has drifted from the original.

Three things to know before you rely on it:

- It reuses whatever parameters you last set on the main Playground page. There are no
  per-pane controls, so all four panes get identical settings — which is what you want for a
  fair comparison, but it means you have to set them on the other page first.
- No logprobs and no heatmap here — the analysis views are single-pane only. Each pane does
  show a footer with its latency, token count and throughput, which is usually the comparison
  you came for.
- Reasoning models show their raw `<think>` blocks here. The single-pane view hides them.

---

## What this page will not do

- **No live heatmap while streaming.** Colouring appears once the message finishes.
- **No editing, deleting, regenerating or branching individual messages.** To explore an
  alternative path, use the [Token Explorer](token-explorer.md), which is built for it.
- **No conversation rename.** Titles are generated.
- **Export is JSON only.** Markdown and JSONL exist in the API but have no button.
- **Prompt tokens read 0 in the Stats panel.** Only completion tokens are recorded per message,
  so the "Total tokens" figure is completion tokens.
- **The pin icon is display-only.** Sorting honours it; nothing in the UI can set it.
- If **Export** appears to do nothing, it failed silently — there is no error toast on this
  path yet.

---

## See also

- [Token Explorer](token-explorer.md) — step through generation and force alternative tokens
- [History](history.md) — every Playground call is recorded there automatically
- [Model Management](models.md) — which provider gives you which capabilities

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | A server is registered and Online | No | Models P1 |
| P2 | TTFT is measured on every response | **Only on the streaming path.** Non-streaming calls record `TtftMs = null` | `OllamaProvider.cs`, `RecordingInferenceProvider.cs` |
| P3 | Token probabilities are returned when asked for | Only when the provider supports them and the switch is on | Token Explorer P1 |

P2 matters for the comparison view: a null TTFT is excluded rather than counted as zero.

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | A prompt can be sent and its response streamed | manual; `useStreamChat` SSE path | MET |
| R2 | Every response records TTFT and tokens/sec where measurable | `MessageStatsPanel` rows; `Message.ttftMs`, `tokensPerSecond` | MET |
| R3 | Those two are reported separately, never combined into one score | `PaneComparisonSummary.test.tsx` pins the split | MET |
| R4 | An unmeasured metric is never rendered as zero | same suite: zero and non-finite are treated as unset | MET |
| R5 | Two servers can be compared on one prompt, sent once | `/playground/compare`, shared input broadcast | MET |
| R6 | The comparison states how many responses each average covers | `PaneComparisonSummary` renders n per metric | MET |
| R7 | A pane's completion tally matches the panes on screen | tracked per pane id and counted against current panes | MET |
| R8 | Conversations survive a reload | persisted server-side; `useConversation` | MET |
