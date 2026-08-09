# Token Explorer

**Stop the model mid-sentence, look at everything it was about to say, and make it say something
else.**

The Playground shows you what a model produced. The Token Explorer shows you what it *nearly*
produced, and lets you overrule it. You give it a prompt, it returns the ranked list of
candidate next tokens with their probabilities, and from there you can walk the generation
forward one token at a time or force a different token and watch where the answer goes instead.

Sidebar: **Token Explorer**.

The page has five tabs. **Predictions**, **Step Through** and **Branches** are covered here.
**Tokenizer** and **Compare** are a different job — counting and comparing token boundaries —
and have their own guide: [Tokenizer and Compare](tokenizer.md).

---

## Before you start

You need a registered instance that returns per-token probabilities. See
[Model Management](models.md).

**In practice that means vLLM.** The three tabs described on this page are built entirely on
logprobs. On Ollama, **Predict Next Token** and **Step (Greedy)** fail outright with

> The inference provider did not return logprobs data. Ensure the model supports logprobs.

Branch exploration fails differently and more quietly: it returns a *successful* branch with no
tokens in it and no perplexity, so you get an empty entry in the Branches tab rather than an
error. If branches keep coming back empty, this is why.

LM Studio and generic OpenAI-compatible endpoints do return logprobs, but only five
alternatives per token instead of twenty, so the ranked list will be five rows long no matter
where you put the **Top Logprobs** slider.

One more provider caveat, and it is the subtle one. Branch exploration works by handing the
model a partial assistant turn and asking it to keep going. vLLM supports that directly. On
other providers the forced token is likely to be read as a finished assistant message, so the
model replies *to* it rather than continuing *from* it — the branch will look wrong rather than
fail, which is worse. Treat branch results from anything other than vLLM as unreliable.

---

## The left panel

Everything on the left applies to every tab. It persists in your browser and survives a reload.

| Control | Default | What it does |
|---|---|---|
| **Model / Instance** | none | Which registered instance to ask. Nothing runs until this is set. |
| **Prompt** | empty | The text the model is predicting from. **Ctrl+Enter** runs a prediction. |
| **Temperature** | 0 | 0 is greedy and deterministic. Raising it flattens the distribution you are shown. |
| **Top-p (visualization)** | 0.9 | Display only — see the warning below. |
| **Top-k (visualization)** | 50 | Display only — see the warning below. |
| **Top Logprobs** | 20 | How many candidates to request per position. vLLM caps at 20. |
| **Enable Thinking** | off | Lets a reasoning model emit its `<think>` block before answering. Leave it off: with it on, your predictions are reasoning-scaffold tokens, not answer tokens. It is also honoured only by vLLM; on other providers the toggle does nothing at all. |

**Predict Next Token** runs the prediction and clears any step history. **Reset All** returns
every control to its default and wipes the selected instance, the prompt, the predictions, the
steps and the branches.

> **Top-p and Top-k do not affect generation.** The "(visualization)" in their labels is
> literal. Neither value is sent to the model. They dim the rows that fall past the cutoff in
> the prediction list and they feed the Sampling Analysis panel on the right — that is the whole
> of their effect. Sliding Top-k to 1 will not make the model behave greedily, and sliding
> Top-p to 1.0 will not widen anything. The only parameter here that changes what the model
> actually does is **Temperature**. This catches everybody once.

---

## Predictions: what was it about to say

Set an instance, type a prompt, click **Predict Next Token**. You get the candidate tokens
ranked by probability, each with a bar, the exact percentage and the raw log-probability.

Whitespace is made visible, because whether the model wanted `" Paris"` or `"Paris"` is
frequently the interesting part: a space shows as `␣`, a newline as `↵`, a tab as `⇥`. Hover any
row for the exact probability to four decimal places, the log-prob to six, the running
cumulative total and the rank.

The thin violet line running down the bars is the **cumulative probability** — where it sits on
each row tells you how much of the mass has been accounted for by that point. Rows past your
Top-p or Top-k cutoff are dimmed. Under the list you get the token count each cutoff would
admit, and the total probability captured by all the candidates you asked for.

That last number matters. If the top 20 tokens only account for 60% of the mass, 40% of what
the model might have said is not on screen at all.

**Clicking any row creates a branch.** There is no separate branch button, and no confirmation
— the click sends a generation request that forces that token and continues from it, and drops
the result in the Branches tab. Worth knowing before you click a row out of curiosity.

---

## Step Through: walk the generation forward

The Step Through tab shows the prompt followed by whatever tokens have been committed so far,
each coloured by its log-probability, with the ranked candidate list underneath.

**Step (Greedy)** commits the top-ranked token and predicts again. Do it repeatedly and you are
watching greedy decoding happen at human speed.

Click any candidate in the list below instead and you commit *that* token — the model is forced
down a path it would not have chosen. Forced tokens are marked with a dotted violet underline
so you can tell your interventions from the model's own choices at a glance, and the counter
below the sequence reads `N tokens generated` alongside `M forced`.

**Undo** removes the last token and restores the candidate list from the position before it.
**Clear** discards the whole sequence and leaves the prompt alone.

This is the tool for the question "would it still have said that". Generate normally until the
model produces a claim you doubt, undo back to the token where it committed, force the
runner-up, and step forward again. If the answer survives the substitution, the claim was not
resting on that token. If it collapses, you have found the hinge.

---

## Branches: the counterfactual experiment

A branch is one complete answer to "what if it had picked this instead". You force a starting
token, the model generates a continuation from there, and you keep the result next to the other
branches for comparison.

Create branches by clicking rows in the Predictions tab, or by forcing tokens in Step Through
and branching from the resulting position. Each branch records the forced token, the full
continuation with per-token colouring, and a perplexity figure for the branch as a whole.

Three views, toggled top-right:

| View | What it gives you |
|---|---|
| **Tree** | Every branch as one line hanging off the prompt, with the first 60 characters of its continuation and its perplexity. The fastest way to see whether two token choices actually led anywhere different. |
| **List** | One card per branch with the continuation rendered token by token. Hover any token for its probability and the five alternatives the model preferred at that position. |
| **Diff** | Two branches side by side, aligned by position, with differing tokens highlighted and the first divergence position called out. |

**Diff** only appears once you have two or more branches, and compares exactly two at a time —
pick which with the Left and Right dropdowns. Position 0 is the forced token, so it always
differs; the number reported as "first divergence" is the first position after that where the
two continuations parted company. A high divergence position is the informative case: it means
two different starting tokens reconverged, and the choice you were worried about did not matter.

**Clear All** removes every branch.

> **Branches are capped at 50 tokens and there is no way to change it.** The limit is fixed on
> the server and no control on this page exposes it. Long branches will stop mid-sentence. Plan
> your prompts so the interesting divergence happens early.

### Reading perplexity across branches

Each branch carries a perplexity value — roughly, how surprised the model was by its own
output. A forced token the model disliked usually produces a branch with visibly higher
perplexity, because the model spends the next few tokens recovering from a start it would not
have chosen.

That recovery is the thing to look at. A model that can absorb an unwanted first token and
arrive at the same answer was not depending on it. A model whose answer flips is telling you
the answer was one sampling decision deep.

---

## Sampling Analysis

The right-hand column recomputes on every prediction. It describes the distribution you are
currently looking at.

| Statistic | What it tells you |
|---|---|
| **Effective Vocab** | How many candidates have a probability above 1%. A small number means the model has essentially decided. |
| **Entropy** | Shannon entropy of the returned distribution, in bits. Low is certain, high is spread out. |
| **Top-p Coverage** | How many tokens it takes to reach your Top-p setting, and the mass they carry. |
| **Top-k Effect** | The probability mass inside your Top-k. Near 100% means the cutoff is discarding nothing that mattered. |
| **Max Probability** | The single most likely token and its probability. |
| **Model Confidence** | A label derived from Max Probability: Very High above 0.8, High above 0.5, Moderate above 0.2, Low above 0.1, Very Low below that. |
| **Distribution Shape** | A stacked bar of the top 20 candidates, with everything else in grey. One dominant block means certainty; a row of slivers means the model is genuinely torn. |

> **Entropy is computed over the candidates you requested, not the full vocabulary.** At **Top
> Logprobs** 20 the ceiling is about 4.32 bits; at 5 it is about 2.32. The numbers are
> comparable between two prompts at the same setting and meaningless between two different
> settings.

Below the statistics the panel names the model and the input token count. Note that after an
**Undo** the input token count reads 0 — the stored prediction is restored from history and
that field is not part of what was kept.

---

## What this page will not do

- **Nothing here can be exported.** No JSON, no CSV, no copy button on a prediction table or a
  branch. Screenshots and retyping are the options.
- **Predictions, steps and branches are lost on reload.** Only the prompt and the parameters
  persist. A page refresh in the middle of a branching session discards the session.
- **There is no cancel.** Once a branch request is in flight you wait for it. A 50-token
  continuation on a large model on a busy GPU can take a while and the page gives you no way out
  but a reload, which loses the rest of your work.
- **Undo zeroes the input token readout.** The token itself is removed correctly; only that one
  number in the Sampling Analysis panel is wrong until the next prediction.
- **Enable Thinking is vLLM-only.** On every other provider the toggle changes nothing.
- **Branch depth is fixed at 50 tokens**, and branches cannot be branched from — you cannot
  fork a branch at position 12 and explore from there. Use Step Through if you need to control
  the path token by token.
- **The Diff view compares two branches, not three.**

---

## See also

- [Tokenizer and Compare](tokenizer.md) — the other two tabs on this page
- [Playground](playground.md) — chat with heatmaps, for reading a whole response at once
- [History](history.md) — every prediction, step and branch is recorded there
- [Model Management](models.md) — why this page needs vLLM
