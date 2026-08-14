# Tokenizer and Compare

**See the actual units a model reads, count them exactly, and check whether two models even
agree on what a word is.**

Models do not read characters and they do not read words. They read tokens, and every model
family draws those boundaries differently. That difference decides how much of your context
window a document consumes, what an API call costs, and whether a perplexity number from one
model can be honestly compared with one from another.

This is its own page — sidebar **Tokenizer** — with two tabs, **Tokenize** and **Compare**.

It used to be two tabs on the Token Explorer, sharing that page's left rail: a prompt, a
temperature, top-p, top-k and a Predict button, none of which tokenization uses, and Compare
carried its own server selector besides, so the screen showed two of them. Counting tokens is a
different question from what the model would say next, and it now has the room to be one. The
prediction and branching views are documented in [Token Explorer](token-explorer.md).

---

## Before you start

**Only vLLM implements tokenization.** This is not a degraded-experience caveat, it is a hard
stop. Ollama returns

> Tokenization is not supported by Ollama.

LM Studio and generic OpenAI-compatible endpoints return the same sentence, but with the name
**you gave the instance** rather than the product name — so it reads like *"Tokenization is not
supported by lmstudio-local."* Both tabs on this page are unusable without at least one registered vLLM instance.
See [Model Management](models.md).

The Tokenizer tab uses the instance selected in the **Model / Instance** dropdown in the
left-hand panel, shared with the rest of the Token Explorer page. The Compare tab has its own
instance picker.

---

## Tokenize some text

Type or paste into **Text to tokenize** and click **Tokenize**, or press **Ctrl+Enter**. The box
starts pre-filled with whatever is in the page's main prompt field, which is convenient after a
run of predictions when you want to know how long the prompt actually was.

Five badges appear above the result: the token count, the character count, the byte count, the
ratio in characters per token, and the model the tokenizer belongs to.

**The ratio is the number to watch.** English prose on a modern tokenizer runs around 4
characters per token. If your text comes back at 2, something in it is being shredded — usually
identifiers, numbers, non-Latin script, or heavily indented code. That is a context-budget
problem before it is anything else.

Below the badges, each token is drawn as its own block, alternating violet and emerald so the
boundaries are unmissable. Hover any block for its text, its numeric ID, its byte length, its
hex bytes and its Unicode codepoints.

Special tokens — `<|begin_of_text|>`, `<|eot_id|>`, `<s>`, `</s>`, `[CLS]`, `[SEP]`, `[PAD]`,
`[UNK]`, `[MASK]` — are drawn with an amber border and tagged **Special token** in the tooltip.
These are the model's own control markers rather than anything you wrote, and seeing them
appear in text you pasted is normally a sign that something upstream has already applied a chat
template.

### Three ways to look at the same tokens

The **Text / IDs / Bytes** toggle above the token blocks re-renders them in place: as visible
text with whitespace made explicit, as numeric vocabulary IDs, or as hex bytes.

The **Copy** button next to it copies whichever view you are on, and the format changes with it:

| View | What Copy puts on the clipboard |
|---|---|
| **Text** | The token texts joined with `\|` — a readable record of exactly where the boundaries fell |
| **IDs** | The numeric IDs, space-separated |
| **Bytes** | The hex bytes, space-separated |

The pipe-joined text form is the one to paste into notes. It is the only artefact this page
produces that survives outside the browser.

---

## Estimate what it would cost

The **Token Cost Estimator** below the tokens takes an **Input price ($/1M tokens)**, default
0.15, and an **Output price ($/1M tokens)**, default 0.60, and shows what your token count
would cost at each rate.

> **This is not a request cost.** It applies your one token count to both prices and reports
> both answers. A real API call charges you for the prompt at the input rate *and* the
> completion at the output rate, and those are two different token counts. Read the two figures
> as "this text as a prompt" and "this text as a response", not as a total, and never add them
> together.

The line of "common rates" under the boxes is hardcoded into the page. It was accurate when it
was written and it is not fetched from anywhere. Check the provider's own pricing page before
you put a number in a budget.

---

## Go the other way: detokenize

The **Detokenize — Token IDs to text** box at the bottom accepts token IDs separated by spaces
or commas and returns the text they decode to. **Ctrl+Enter** runs it. **Use IDs from above**
fills the box with the IDs from your most recent tokenize result, which is the quick way to
confirm a round trip.

This is how you read a token ID that turned up somewhere else — in a log, in a model config, in
a paper's appendix — without guessing.

> **There is no validation of the IDs.** Anything numeric is sent through. An ID outside the
> model's vocabulary, or a vocabulary ID from a *different* model, will not produce an error;
> it will produce plausible-looking garbage. If detokenized text looks like mojibake, suspect
> the IDs before you suspect the tokenizer.

---

## Compare tokenizers across models

The **Compare** tab answers one question: does the same text cost the same on two different
models.

Select two or more instances from the row of buttons at the top — selected ones turn violet —
type your text, and click **Compare** (or **Ctrl+Enter**).

The **Comparison summary** table gives one row per instance: **Instance**, **Model**,
**Tokens**, **Chars/Token** and **Bytes**. The lowest token count is highlighted in emerald and
labelled **(fewest)**. Below the table, each model gets its own card showing where it drew the
boundaries. Hovering a token here shows its ID, byte length, hex and codepoints — one field
fewer than the Tokenizer tab, which also shows the token text. Compare does no special-token
detection either, so `<|endoftext|>` gets no amber highlight the way it does on the Tokenizer
tab.

Instances that cannot tokenize do not sink the comparison. They come back as a red row carrying
the provider's error message, and the models that succeeded still render normally. With one
vLLM instance and one Ollama instance selected you will get one real row and one red one.

Comparisons are most revealing on the content that is least like English prose. Try a code
snippet, a URL, a table of numbers, or a paragraph of a non-Latin script — differences that are
invisible on ordinary prose become factors of two.

---

## Why a researcher should care

Three things follow from token boundaries, and the third one is the one that quietly invalidates
results.

**Cost.** Price is quoted per token, so a tokenizer that splits your domain's vocabulary into
three pieces where another splits it into one costs three times as much for identical text.

**Context budget.** A 8k context is 8k tokens, not 8k words. The same document may fit on one
model and overflow on another, and the failure mode is silent truncation at the front.

**Perplexity comparability.** Perplexity is the exponential of the mean negative log-likelihood
*per token*. Change what counts as a token and you change the denominator. A model with a finer
tokenizer spreads the same uncertainty across more, individually easier predictions and reports
a lower perplexity on the same passage without understanding it any better. Comparing perplexity
between two models with different tokenizers measures their tokenizers at least as much as it
measures the models — and the direction of the bias is not even consistent, because it depends
on the text.

The Compare tab is how you check before you make that mistake. If two models return the same
token count on a representative sample of your text, their perplexities are at least roughly
comparable. If one returns 40% more tokens, the comparison is not one you can report. Compare
perplexity within a model — across prompts, across checkpoints, across quantisations — and not
across tokenizer families.

---

## What these tabs will not do

- **No export.** Copy to clipboard is the only route out, and only for the token list — the
  summary table, the cost estimate and the comparison cards cannot be saved.
- **Results are lost on reload.** Nothing on either tab persists.
- **No history.** Each tokenize replaces the last; there is no way to hold two results from the
  same model side by side.
- **The comparison is per-model, not per-token-alignment.** You see where each tokenizer drew
  its boundaries, but the cards are not aligned against each other, so on long text you are
  eyeballing it.
- **The cost estimator does not know about your registered instances.** It will happily price a
  local model you are running for free.
- **Detokenize accepts any number.** No vocabulary-range check, no error on invalid IDs.
- **Compare requires at least two instances selected**; with one selected the button stays
  disabled and a small amber note asks for another.

---

## See also

- [Token Explorer](token-explorer.md) — the Predictions, Step Through and Branches tabs on this
  same page
- [Model Management](models.md) — registering the vLLM instance these tabs require
- [Playground](playground.md) — where the perplexity caveat bites in practice
