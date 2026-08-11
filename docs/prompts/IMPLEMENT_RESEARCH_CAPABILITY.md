# Prompt: implement a research capability in Prism

You are implementing one capability from `docs/plans/RESEARCH_CAPABILITIES.md` in the Prism
repository. Read `CLAUDE.md` first and follow it; it governs architecture and style.

This prompt governs **method**. It is deliberately demanding, because everything it asks for was
written after a defect that got past a green test suite.

---

## The standard

A capability is done when **a person can reach it in the running app**, its numbers are **proved
rather than asserted**, and you have **twice tried to break your own work and failed**.

Three failures to design against, all of which have already happened here:

- **Finished work nobody can reach.** `useAbTest`, `useRagPipeline`, `useUpdateWorkflow` and
  `CalibrationPlot` are complete, tested, and wired to nothing. A backend without a UI is not a
  feature; it is a liability that reads as a feature.
- **A number that agrees only with itself.** Hand-rolled BLEU passes its tests and cannot be
  compared to any published result.
- **A test that never tested anything.** An integration test here passed in 20 ms with no server
  running, because its skip path and its success path were indistinguishable.

---

## Before you write anything

**Read the code, not the docs.** Documentation in this repository is unusually honest and still
goes stale. Before implementing, confirm by reading:

- what the endpoint actually returns, field by field, including nullability
- what the frontend type actually declares — these are hand-written and have drifted from the
  DTOs at least twice
- whether the capability already exists and is simply unreachable, in which case wiring it up is
  the whole job

**State your assumptions in writing before coding**, in the form used by the requirements tables:
each one, whether it holds on a cold `./dev.sh` install, and the file:line that proves it. Most
defects here were unstated assumptions that turned out false, not untested code.

---

## Proving a number

This is the part that is not negotiable. "The test passes" means the code agrees with itself.

For every metric, statistic or aggregate, provide **all three**:

**1. Reference vectors.** Values from a published source or a reference implementation, cited in
the test by name and version. At least 20 cases where a reference exists, and they must include
the degenerate ones: empty input, single element, no overlap, perfect overlap, ties, and the
shortest input the metric is defined for. Agreement to a stated tolerance (1e-9 unless the
reference documents otherwise).

**2. Invariants that hold for all inputs.** Properties, not examples. Entropy of a uniform
distribution over n outcomes is exactly log₂(n). Recall@k is non-decreasing in k. nDCG of the
ideal ranking is exactly 1. A perfectly calibrated set has ECE 0. Identical strings score 1.0.
Where the space is large, generate inputs rather than enumerating them.

**3. A worked example in the test.** For anything hand-computable, do the arithmetic in a comment
and assert the exact figure. A reviewer must be able to check the maths without running anything.

Additionally:

- **State the definition.** BLEU is not one number: tokeniser, smoothing method and
  corpus-versus-sentence all change it. Record which was used, next to the score, in the UI.
- **Corpus statistics are not means of sentence statistics.** If you present one as the other,
  that is an error, not a simplification.
- **Floating point:** assert with an explicit tolerance, never `==`. Say why the tolerance is
  what it is.

---

## Surfacing it

The capability must be reachable by clicking, and correct when the data is missing.

- It appears on the page a researcher would look on, not a new one, unless the plan says otherwise.
- **Missing is never zero.** A metric that was not computed renders as absent and is excluded
  from averages, comparisons and "best" determinations. This repository has already shipped a
  chart that drew a missing measurement as zero and a cost column that invented `$0.00`.
- **A failure says what failed.** Not a spinner that stops. Not an unchanged panel.
- The control is disabled when it cannot work, rather than being clickable and doing nothing.
- If the capability has a prerequisite the install may not meet, the UI states the prerequisite.

---

## Verification protocol

Run these exactly. Several obvious-looking commands do not do what they appear to.

```bash
# Backend
dotnet build backend/Prism.sln              # warnings are errors; XML docs are required
dotnet format backend/Prism.sln --verify-no-changes
export PRISM_TEST_DB="Host=localhost;Port=5438;Database=prism_test;Username=postgres;Password=postgres"
PRISM_REQUIRE_OLLAMA=1 dotnet test backend/Prism.sln

# Frontend
cd frontend
npx tsc -b --noEmit          # NOT `npx tsc --noEmit` — see traps
npm run lint
npm test

# Launcher
./scripts/tests/menu_test.sh && ./scripts/tests/provider_test.sh
```

**Then run it in a browser.** Unit tests here have repeatedly been green while the feature was
broken, because the defects were in wiring, timing and rendering. Drive the real app: click the
control, force the failure path, and screenshot both.

### Use Claude in Chrome for this

Chrome is installed on this machine and the Claude extension is set up, so the proving pass runs
in a real browser rather than a headless script. Use it — it sees things a script does not.

**Before you start:** call `list_connected_browsers`. An empty list means **Chrome is not
running**, not that the extension is broken — the link exists only while Chrome is open. Start
Chrome and call it again. Then confirm with the user which browser to use before acting, because
these tools drive their live session, not a throwaway profile.

What it gives you that a headless run does not:

- **`read_console_messages`** — React warnings, key collisions, effect loops and uncaught
  rejections that never reach the DOM. Filter with `pattern` rather than reading everything.
  A green screenshot with a red console is not a pass.
- **`read_network_requests`** — proof that the request carried what you think. This is how you
  show a window control actually sent `from=`, or that an export sent `format=jsonl` and the
  active filters. Asserting the UI changed is weaker than showing the wire.
- **`javascript_tool`** — force state directly: seed `localStorage`, resolve a CSS variable,
  read a computed style. The chart colours that rendered near-white were proved this way, by
  evaluating `getComputedStyle` rather than by looking at a screenshot.
- **Screenshots of both paths**, which the definition of done requires.

**Two cautions.** Do not trigger native `alert`/`confirm`/`prompt` dialogs — they block the
extension until dismissed by hand, and several pages here use `confirm()` for delete. And the
first `<select>` on any page is the workspace switcher, so select by content, never by index.

**Interactive proving, not permanent guard.** Claude in Chrome is how you convince yourself and
produce the evidence. The regression that stops it breaking again belongs in a script or a spec.
Note that `@playwright/test` is configured here but not installed and its CI job is red — an open
decision, so check before assuming you can add a spec.

### Traps in this repository, learned the hard way

- **`npx tsc --noEmit` checks nothing.** The root `tsconfig.json` is `"files": []` plus project
  references, so it always exits 0. Use `tsc -b --noEmit`.
- **`cmd | head; echo $?` reports `head`'s status**, not `cmd`'s. Redirect to a file and check
  the exit code of the command itself.
- **A skipped test looks like a fast pass.** Any test that can no-op needs a switch that turns
  absence into failure — see `PRISM_REQUIRE_OLLAMA`.
- **Capability flags lie.** Two features were declared unavailable while the transport
  implemented them. Probe the running server before believing a flag.
- **Stale processes corrupt shared state.** Several API instances against one database will
  overwrite each other every 30 seconds. `dev.sh` now stops them; check `pgrep -f Prism.Api`
  before diagnosing anything strange.
- **204 has no body.** `apiClient` handles this now; do not reintroduce a bare `response.json()`.
- **The first `<select>` on any page is the workspace switcher.** Browser tests must select by
  content, not index.

---

## Then assume you got it wrong

You did not finish when the tests passed. Two adversarial passes, both written down.

**Pass one — attack the implementation.** For each new test, break the code it guards and confirm
the test fails; restore. A test never observed failing is not known to test anything. Then ask:
what input have I not tried? Empty, single element, unicode, very large, null, NaN, negative
zero, duplicate keys, concurrent calls. What if the provider returns success with an empty body?
What if two of these run at once?

**Pass two — attack the framing.** Assume the whole approach is wrong, not just the edges:

- Am I measuring what the metric is defined to measure, or something adjacent that agrees on my
  examples? Name the case that would distinguish them, then test it.
- Would a researcher who knows this metric recognise my number? If they would ask "which
  tokeniser?", the UI must already answer.
- Does this hold on a cold install, with no data, with one row, and with a provider that lacks
  the capability?
- Is anything I built unreachable? Grep for every new export and confirm it has a caller.

Record both passes in the pull request or commit message: what you attacked, what broke, what
you changed. **If neither pass found anything, that is evidence you attacked too gently** — say
what you tried.

---

## Definition of done

- [ ] Reachable in the running app, verified in a real browser via Claude in Chrome, with a
      screenshot of the working path and of the failure path.
- [ ] The browser console is clean during that verification — checked, not assumed.
- [ ] Where a request's parameters matter, the network log is cited as evidence rather than the
      rendered result.
- [ ] Reference vectors, invariants and a hand-worked example all present and passing.
- [ ] Every new test observed failing when its subject is broken.
- [ ] Missing data renders as absent, never as zero, and is excluded from aggregates.
- [ ] Failures state what failed.
- [ ] The full verification protocol passes, including `tsc -b`.
- [ ] `docs/features/<tab>.md` requirements table updated: new rows MET, with the check that
      proved them; any presupposition discovered added, with whether it holds on a cold install.
- [ ] Nothing new is unreachable — every export has a caller.
- [ ] Both adversarial passes documented.

---

## Do not stop part-way

If you were given more than one item — or the whole plan — finish it.

- **Do not stop to ask whether to continue.** That decision is already taken. Ask only when
  genuinely blocked: a credential you do not have, a destructive action, or a product decision
  the plan does not settle.
- **Do not stop because the session is long.** If context runs short, commit what is proven,
  write down exactly where you are and what remains, and carry on.
- **Do not stop at "the code compiles".** An item is finished when its proof obligations are met
  and it is reachable in the browser.
- **Report as you go.** After each item, say what was proved and how. Do not go silent for a
  phase at a time and surface one large claim at the end.
- **If an item turns out to be wrong**, say so with evidence and amend the plan. Withdrawing an
  item is a decision to record, not something to skip quietly.

Done means every item is MET with the check that proved it, or WITHDRAWN with a reason, in the
relevant `docs/features/<tab>.md` table.

---

## What to do when you disagree with this prompt

Say so, with the reasoning, and proceed the way you think is right — then flag it clearly in the
summary. Following a procedure that is wrong for the case at hand is not the goal. Silently
skipping a step is.
