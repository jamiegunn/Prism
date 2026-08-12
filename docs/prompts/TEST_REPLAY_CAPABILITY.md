# Prompt: prove or repair Replay in Prism

You are testing **History → Replay** in the Prism repository, end to end, and fixing everything
you find. Read `CLAUDE.md` first and follow it; it governs architecture and style. This prompt
governs **method** and is deliberately demanding.

Replay is reported broken: *"select a row and then replay."* Your job is not to confirm that one
report. It is to establish what Replay actually does across its whole input space, fix what is
wrong, and leave behind tests that would have caught each defect.

---

## The standard

Replay is done when a person can re-run any recorded call from the running app, see the original
and the replay side by side, and **every failure mode says what failed**. Not a spinner that
stops. Not a blank pane. Not a console error the user never sees.

Three failure patterns this repository has shipped before, all of which apply here:

- **A DTO and its hand-written frontend type disagreeing.** The types are not generated. They
  have drifted at least three times.
- **A feature that is complete on one side and wired to nothing on the other.**
- **A test suite green while the feature is broken**, because the defect was in wiring, shape or
  rendering rather than in logic.

---

## Rule zero: trust nothing you did not run

**Do not trust documentation, code comments, XML docs, this prompt's findings, or a previous
agent's commit message.** Every one of those has been wrong in this repository. `docs/` is
unusually honest and still goes stale.

Specifically:

- Confirm what the endpoint returns by **calling it and printing the JSON**, field by field,
  including nullability and the *type* of each field.
- Confirm what the frontend declares by **reading the type**, then check it against that JSON.
- A doc row that says MET is a claim, not evidence. A doc row that says UNMET may have been
  fixed since. Re-derive both.
- The findings in "Confirmed starting points" below were reproduced on a live instance, but
  **reproduce them yourself before fixing them** — the code may have moved.

---

## Environment

The repo is at the checkout you are working in. Bring the stack up with `./dev.sh`.

- **`./dev.sh` now defaults to running the app in containers**, and asks whether you want
  containers or metal. Either is fine for this work; metal (`native dotnet` + `vite`) is easier
  if you want to attach a debugger. If you pick containers, the API is reachable on the mapped
  host port and the frontend on `http://localhost:5173`.
- The API port is `5000` unless taken, in which case `dev.sh` moves it and prints where. Read the
  banner rather than assuming.
- Postgres is on `5438` (`prism` for the app, `prism_test` for tests).
- You need an **online inference instance** to replay against. `dev.sh` registers one. Discover
  ids rather than hardcoding them:

```bash
API=http://localhost:5000          # confirm from the dev.sh banner
curl -s "$API/api/v1/models/instances" | python3 -m json.tool | head -40
curl -s "$API/api/v1/history?pageSize=5" | python3 -m json.tool | head -40
```

If no local model server is available, stand up any OpenAI-compatible stub that answers
`POST /v1/chat/completions` and register it from the Models page. Several scenarios below need a
server you can make *fail on demand*, so a stub you control is worth the ten minutes.

---

## The surface under test

Read all of these before you start. Paths are from the repo root.

| Layer | File |
|---|---|
| Endpoint | `backend/src/Prism.Features/History/Api/HistoryEndpoints.cs` (`/{id:guid}/replay`) |
| Handler | `backend/src/Prism.Features/History/Application/ReplaySingle/ReplaySingleHandler.cs` |
| Command | `backend/src/Prism.Features/History/Application/ReplaySingle/ReplaySingleCommand.cs` |
| Result DTO | `backend/src/Prism.Features/History/Application/Dtos/ReplayResultDto.cs` |
| Dead entity? | `backend/src/Prism.Features/History/Domain/ReplayRun.cs` + its EF configuration |
| Frontend type | `frontend/src/features/history/types.ts` (`ReplayResult`) |
| Frontend call | `frontend/src/features/history/api.ts` (`useReplayRecord`) |
| Dialog + diff | `frontend/src/features/history/components/ReplayDialog.tsx` (incl. `DiffText`) |
| Entry point | `frontend/src/features/history/components/RecordDetailPanel.tsx` |

---

## Confirmed starting points

These were reproduced against a running instance. **Reproduce each one yourself, then fix it.**
They are where to start, **not** the list of what is wrong. If you finish having fixed only
these, you did not test hard enough — say so explicitly rather than implying coverage.

1. **The response `original` field is an object; the frontend declares it a string.**
   `ReplaySingleHandler` sets `Original: originalDto`, an `InferenceRecordDetailDto`. The live
   response confirms `type(original) == dict`. `frontend/.../types.ts` declares
   `original: string`. `DiffText` then runs `text.split(/(\s+)/)` on it. An object has no
   `.split`, so the results pane throws at render. Decide deliberately which side is correct —
   the whole detail object is arguably more useful than a bare string — and make both sides agree.

2. **`DiffText` compares `original === replay`.** With an object on one side that comparison can
   never be true, so the "identical" fast path is unreachable and every replay takes the diff
   path. Whatever you do in (1), this equality check must be meaningful.

3. **`ReplayRun` is a dead entity.** The class, the EF configuration and the
   `history_replay_runs` table all exist. The table has **0 rows** and nothing in the non-test
   backend writes one. Either wire it up so a replay is persisted and linkable, or remove it and
   record the withdrawal. A table nothing writes reads as a feature and is a liability.

4. **There is no backend test for replay at all.** No `Prism.Tests` file references
   `ReplaySingleHandler`. Whatever you fix, it is currently unguarded.

---

## Scenarios

At minimum, execute every scenario below. **Thirty are listed; ten is the floor, not the target.**
Add your own — the ones you invent are the ones this list did not think of. For each: state what
you expected, what happened, and the evidence (wire log, screenshot, DB query, or test).

### A. The reported path
1. History list → click a row → detail panel → **Replay** → pick an instance → **Replay**. Does
   a result render? Is the console clean? This is the reported break; capture it before fixing.
2. The same from every entry point Replay is reachable from. Grep for `ReplayDialog` and confirm
   you have exercised each caller.

### B. Contract and shape
3. Print the raw replay JSON and check **every field** against the frontend type: name, type,
   nullability. Not just `original`.
4. `diffSummary` — is it correct when responses are identical, when they differ by one token, and
   when one side is empty? Does the UI's own diff agree with the server's summary?
5. Token counts and latency — are they the replay's, or accidentally the original's? Prove it
   with a provider whose usage numbers you control.
6. `replayModel` — does it report the model actually used, after override resolution?

### C. Instance selection
7. No online instances at all. Is the control disabled with a stated reason, or clickable and inert?
8. An instance that is registered but **offline**.
9. An instance id that does not exist (call the API directly).
10. An instance whose model differs from the original record's. The docs claim the model changes
    silently — verify, and make sure the result states which model actually ran.
11. An instance of a **different provider type** than the original (Ollama vs vLLM vs
    OpenAI-compatible).
12. An instance that goes offline **mid-flight** (kill the server after clicking Replay).

### D. Parameter overrides
13. Temperature override — prove on the wire that the request carried it.
14. Top-P override — same.
15. Max tokens override — same, and confirm the response respects it.
16. Boundary values: `0`, negative, non-numeric, absurdly large (`999999999`), and empty string.
17. "Reset" on each override — does it truly revert to the original's value, or send a default?
18. All three overrides at once, and the "active" badge's accuracy.

### E. Which record is being replayed
19. A **failed** record (`responseJson` is null). What renders on the original side?
20. A record whose `requestJson` is malformed, truncated, or an older shape. The handler returns
    `Error.Internal` on deserialize failure — is that surfaced usefully, and is Internal the right
    class for what is really bad stored data?
21. A record that originated from **streaming** (the handler forces `Stream = false`).
22. A record with logprobs recorded, and one without.
23. Records from other source modules: `evaluation`, `batch-inference`, `evaluation-judge`.
24. A very large request (hundreds of KB of prompt).
25. Content with unicode, emoji, RTL marks, and long unbroken strings — check the diff rendering,
    not just that it returns 200.

### F. Persistence and side effects
26. Does a replay write a `ReplayRun`? (Today: no.) Decide, implement, and test the decision.
27. Does a replay create a **new history record** with source `history-replay`? If so: is it
    itself replayable, and does that recurse sensibly? Does it pollute the list the user was
    looking at?
28. Is the replay findable afterwards — by filter, by source, by tag?

### G. Concurrency and lifecycle
29. Two replays at once (same record, and different records). Do results cross over?
30. Close the dialog **mid-flight**, then reopen. Does a stale or cancelled result appear?
    Replay twice in a row without closing — is the second result the second replay's?

### H. Failure surfacing
For each of these the requirement is identical: **the user is told what failed, in the UI.**
Provider 500. Provider timeout. Provider returning 200 with an empty body. Network failure.
Record deleted between opening the dialog and clicking Replay. Malformed JSON from the provider.

### I. Adversarial
Whatever you have not tried. Concurrent replay while the record is being tagged. Replay after the
target instance is deleted. Replay with the workspace switched. Two API processes running (see
traps).

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

### Use Claude in Chrome

Chrome is installed and the Claude extension is set up. Use it — it sees what a headless script
does not.

**Before you start:** call `list_connected_browsers`. An empty list means **Chrome is not
running**, not that the extension is broken. Start Chrome and call it again. Confirm with the
user which browser to use before acting; these tools drive their live session.

For this task specifically:

- **`read_console_messages`** is the primary instrument. The `original`-shape defect manifests as
  an uncaught `TypeError` at render. A green screenshot with a red console is a failure.
- **`read_network_requests`** is how you prove an override reached the wire. Asserting the UI
  changed is weaker than showing the request body.
- **`javascript_tool`** to force state: seed `localStorage`, inspect a computed style, or read
  React state that never reaches the DOM.
- **Screenshot every working path and every failure path.** The definition of done requires both.

**Two cautions.** Do not trigger native `alert`/`confirm`/`prompt` — they block the extension
until dismissed by hand. And **the first `<select>` on any page is the workspace switcher**, so
select by content, never by index.

### Traps in this repository, learned the hard way

- **`npx tsc --noEmit` checks nothing.** Root `tsconfig.json` is `"files": []` plus project
  references, so it always exits 0. Use `tsc -b --noEmit`.
- **`cmd | head; echo $?` reports `head`'s status.** Redirect to a file and check the real one.
- **A skipped test looks like a fast pass.** Anything that can no-op needs a switch that turns
  absence into failure — see `PRISM_REQUIRE_OLLAMA`.
- **Stale processes corrupt shared state.** Several API instances against one database overwrite
  each other every 30 seconds. `dev.sh` now stops any Prism API on the machine; still check
  `pgrep -f 'dotnet.*Prism\.Api'` before diagnosing anything strange.
- **204 has no body.** `apiClient` handles it; do not reintroduce a bare `response.json()`.
- **Capability flags lie.** Probe the running server rather than believing a flag.
- **The seeded instances may be offline.** An "instance not found"-looking failure is often an
  instance that exists and is not answering.

---

## Fixing

For every defect you fix:

- **Write the test first, watch it fail, then fix it.** A test never observed failing is not
  known to test anything.
- **Mutate the fix.** Break the corrected code deliberately, confirm the new test goes red,
  restore, confirm green. Record each mutation and its result.
- **Fix the class, not the instance.** If a hand-written frontend type drifted from a DTO, ask
  what else drifted — check every type in that feature, not just the one that crashed.
- **Missing is never zero.** A token count that was not reported renders absent, not `0`.
- **Prefer making the contract explicit** over defensive coercion. `String(original)` at the
  render site would stop the crash and hide the real defect.

---

## Then assume you got it wrong

Two adversarial passes, both written down.

**Pass one — attack the implementation.** What input have you not tried? Empty, null, unicode,
enormous, concurrent, duplicated, deleted-mid-flight. What if the provider returns 200 with no
body? What if the same record is replayed from two tabs?

**Pass two — attack the framing.** Is "replay" even doing what a researcher means by it? If the
model, the parameters and the server all differ from the original, is the comparison meaningful,
and does the UI say so plainly enough that nobody draws a false conclusion from it? Is a diff of
response *text* the right primitive, or is the useful diff over tokens, logprobs or parameters?
Would a researcher trust this output in a write-up?

**If neither pass found anything, that is evidence you attacked too gently** — say what you tried.

---

## Definition of done

- [ ] All thirty scenarios executed, each with expected/actual/evidence recorded.
- [ ] Every defect found is fixed, or recorded as WITHDRAWN with a reason.
- [ ] Every fix has a test that was observed failing before the fix and passing after.
- [ ] Every fix mutation-checked: mutation applied, test red, restored, test green — each logged.
- [ ] Working path **and** failure path screenshotted from the real browser.
- [ ] Browser console clean during verification — checked, not assumed.
- [ ] Override scenarios evidenced from the **network log**, not the rendered result.
- [ ] The `ReplayRun` question settled: wired up and tested, or removed with the reason recorded.
- [ ] Full verification protocol passes, including `tsc -b`.
- [ ] `docs/features/history.md` requirements table updated — the Replay rows carry the check
      that proved them; any presupposition discovered is added with whether it holds on a cold
      install. `docs/product-truth.yaml` updated to match.
- [ ] Nothing new is unreachable — every new export has a caller.
- [ ] Both adversarial passes documented in the commit message.

---

## Delivery

Commit in logical units with messages that explain **why**, not what — the diff already says what.
State in each message what you attacked, what broke, and what you changed.

This repository is developed against a sandbox and handed back as patches:

```bash
git format-patch <base>..HEAD -o _handoff/patches-research/ --start-number 15
```

Update `_handoff/patches-research/APPLY.md` with the new commits and the gate numbers. If you
produce screenshots, bundle them alongside as evidence.

---

## Do not stop part-way

- **Do not stop to ask whether to continue.** That decision is taken. Ask only when genuinely
  blocked: a credential you do not have, a destructive action, or a product decision this prompt
  does not settle.
- **Do not stop because the session is long.** If context runs short, commit what is proven,
  write down exactly where you are and what remains, and carry on.
- **Do not stop at "the crash is fixed".** Scenario 1 is the beginning of the job.
- **Report as you go.** After each group of scenarios, say what was proved and how. Do not go
  silent and surface one large claim at the end.

---

## What to do when you disagree with this prompt

Say so, with the reasoning, and proceed the way you think is right — then flag it clearly in the
summary. Following a procedure that is wrong for the case at hand is not the goal. Silently
skipping a step is.
