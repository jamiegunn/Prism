# ADR-011: Contextual Help Panels on Feature Views

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

Prism is a research tool that surfaces low-level model internals — token probabilities, log-probs, perplexity, tokenizer boundaries, sampling thresholds, and branch exploration. These concepts are not self-explanatory to many users, even those with ML experience. Without guidance, users can see the data but may not understand what it means, why it matters, or how to act on it.

During Phase 1 development of the Token Explorer, we found that each tab (Predictions, Step Through, Branches, Tokenizer, Compare) benefits from a brief explanation of:

1. **What** the view is doing (the mechanism)
2. **Why** it matters (the research value)
3. **How to interpret the results** (reading the output)
4. **Tips** for getting the most out of the feature

This need will recur across every Prism feature — Playground logprobs, Experiment Tracker metrics, Evaluation Suite scoring, RAG retrieval results, and so on.

## Decision

Every feature view and tab that presents non-obvious data **must** include a collapsible contextual help panel. The panel:

- Is **collapsed by default** so it stays out of the way for experienced users
- Sits at the **top of the tab content area**, above the main UI
- Uses a consistent `HelpPanel` component with a chevron toggle, help icon, and title
- Follows a **structured content format**: What, Why, How to read, and Tip sections
- Is styled as a subtle, muted container that does not compete with the primary UI
- Contains **static content** — no API calls, no dynamic state
- Is implemented as a **reusable component** (`HelpPanel`) that accepts a title and children

### Where to add help panels

Help panels are required on:

- Every tab in a multi-tab feature view (e.g., Token Explorer's 5 tabs)
- Feature pages that surface model internals or statistical data
- Views where the output format is not immediately obvious (e.g., logprobs heatmaps, perplexity scores, tokenizer blocks)

Help panels are **not needed** on:

- Simple CRUD views (model instance list, history search)
- Settings and configuration pages
- Views where the UI is self-explanatory (text input/output playground)

### Component pattern

```tsx
<HelpPanel title="How Predictions Work">
  <p><strong>What:</strong> ...</p>
  <p><strong>Why:</strong> ...</p>
  <p><strong>How to read the results:</strong> ...</p>
  <p><strong>Tip:</strong> ...</p>
</HelpPanel>
```

## Consequences

### Positive

- Users can learn the tool without leaving the interface or consulting external docs
- Collapsed by default means zero visual overhead for experienced users
- Consistent structure across all features makes the help predictable and scannable
- Reusable component keeps implementation trivial — adding help to a new tab is just content authoring
- Reduces support burden and onboarding friction for new users

### Negative

- Every new tab/view requires writing help content, adding a small authoring cost
- Help text can become stale if the feature changes and the content is not updated

### Neutral

- Help content is static JSX, not stored in a database or CMS — suitable for a developer tool where content changes infrequently
- The `HelpPanel` component uses native HTML/CSS (no extra UI library dependency)

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Tooltip-only help | Minimal UI footprint, already used for token hover details | Cannot convey multi-paragraph explanations; discoverable only on hover | Insufficient for "why" and "how to interpret" guidance |
| Separate docs/wiki page | Can be more detailed, supports search | Breaks flow — users must leave the app to learn | Research tools need inline context, not external reference |
| Always-visible help sidebar | Always accessible without clicking | Consumes permanent screen space on an already 3-panel layout | Too intrusive for a power-user tool |
| Guided tour / onboarding wizard | Great for first-time users | Annoying on repeat visits, complex to implement, doesn't help when revisiting a specific tab | Overkill for Phase 1; could be added later as a complement |
| Info icon with popover | Small footprint, click to reveal | Popovers have limited space, hard to format structured content, awkward positioning | Collapsible panel is better for multi-paragraph help |

## References

- Token Explorer implementation: `frontend/src/features/token-explorer/components/HelpPanel.tsx`
- First usage: Token Explorer tabs (Predictions, Step Through, Branches, Tokenizer, Compare)
