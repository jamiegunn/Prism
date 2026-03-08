# ADR-012: Parameter Tooltips on All Configuration Controls

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

Prism exposes many inference parameters across its features — temperature, top-p, top-k, frequency penalty, logprobs, max tokens, stop sequences, and more. These parameters are well-known to ML practitioners but their exact behavior, valid ranges, and interactions are not obvious to all users. Even experienced users benefit from quick reminders, especially for parameters like presence penalty vs frequency penalty where the distinction is subtle.

During Phase 1, the Playground and Token Explorer both present parameter sidebars with bare labels and sliders. Without context, users must leave the application to look up what a parameter does or how it affects output.

This is distinct from the contextual help panels described in ADR-011. Help panels explain a view's purpose and how to interpret results. Parameter tooltips explain individual controls — what the parameter does, what values mean, and when to adjust it.

## Decision

Every configuration control that affects model behavior **must** have a tooltip accessible via hover. The tooltips:

- Are triggered by hovering the label, which displays a small `(i)` info icon as an affordance
- Use a **shared `ParamLabel` component** (`components/ui/param-label.tsx`) to enforce consistency
- Appear **below the label**, left-aligned, at a fixed width of `w-72` (18rem / 288px) for comfortable reading
- Contain a **single concise paragraph** (1-3 sentences) explaining what the parameter does and what its values mean
- Are plain text, not structured with headers — they should be glanceable in under 5 seconds

### Where to apply

Parameter tooltips are required on:

- Every slider, toggle, dropdown, and numeric input that controls inference behavior
- Model/instance selectors (explain what an "instance" is)
- Any parameter whose name alone is insufficient (e.g., "Top P" needs explanation, "Prompt" is borderline but included for completeness)

Parameter tooltips are **not needed** on:

- Action buttons (Predict, Send, Reset) — their label is their explanation
- Text input areas where the placeholder already explains usage
- Display-only values (stats, badges, readouts)

### Component pattern

```tsx
import { ParamLabel } from '@/components/ui/param-label'

<ParamLabel
  label="Temperature"
  tooltip="Controls randomness. 0 = deterministic (always picks the most likely token). Higher values make output more creative by flattening the probability distribution."
/>
```

Replaces bare `<label>` elements. The component renders the label text, a subtle info icon, and the hover tooltip in one unit.

### Writing guidelines for tooltip text

- Lead with what the parameter **does**, not its technical definition
- Include the effect of extreme values (e.g., "0 = greedy", "1.0 = consider all tokens")
- Mention interactions with other parameters only when critical (e.g., "Works alongside Top P")
- Keep under 40 words when possible — this is a tooltip, not documentation
- Use plain language; avoid jargon that itself needs a tooltip

## Consequences

### Positive

- Users can learn parameter behavior without leaving the interface
- Consistent component ensures every control gets the same treatment
- The info icon signals that help is available without cluttering the UI
- Tooltip text is co-located with the control in the component tree, making it easy to update when behavior changes

### Negative

- Every new parameter requires writing a tooltip — small authoring cost per control
- Tooltips can obscure adjacent controls momentarily while shown

### Neutral

- The `ParamLabel` component is in `components/ui/` (shared) rather than feature-specific, so all features use the same pattern
- Tooltip positioning (bottom, left-aligned) is optimized for narrow sidebar layouts where right or centered positioning would get clipped

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Bare labels with no help | Cleanest UI, no implementation cost | Users must look up parameters externally; poor for onboarding | Prism targets researchers who may not be inference experts |
| Help text below each control | Always visible, no hover needed | Clutters the sidebar; doubles the vertical space per control | Too noisy for a dense parameter panel |
| Single "parameter reference" page | Comprehensive, searchable | Requires leaving the current view; breaks flow | Tooltip is faster for a quick reminder |
| Popover with extended help (title + description + examples) | Richer content per parameter | Heavier UI, requires click instead of hover, more authoring effort | Overkill for parameters — a sentence is enough |
| Browser-native `title` attribute | Zero implementation cost | Slow to appear (OS delay), unstyled, no control over width or position | Not readable enough for multi-sentence help |

## References

- `ParamLabel` component: `frontend/src/components/ui/param-label.tsx`
- Tooltip component: `frontend/src/components/ui/tooltip.tsx`
- First usage: Playground `ParameterSidebar.tsx`, Token Explorer `TokenExplorerPage.tsx`
- Related: ADR-011 (Contextual Help Panels) — complementary pattern for view-level help
