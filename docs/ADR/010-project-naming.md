# ADR-010: Project Name — Prism

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The platform needs a name that is:

- Short and memorable (works as a CLI command, namespace, and brand)
- Reflects what the tool does — decomposing model outputs into their full probability spectrum
- Not taken by a major AI product
- Works across all contexts: `Prism.Api`, `prism serve`, "open Prism", the Prism dashboard

## Decision

The project is named **Prism**.

A prism takes a single beam of light and reveals the full spectrum of colors within it. This platform takes a single model output and reveals the full spectrum of token probabilities, alternatives, and confidence signals within it. Logprobs heatmaps, entropy charts, and probability distributions are literally a prism applied to text.

### Naming Conventions

| Context | Name |
|---------|------|
| Product name | Prism |
| Solution/namespace | `Prism` |
| Backend projects | `Prism.Api`, `Prism.Common`, `Prism.Features`, `Prism.Tests` |
| Frontend package | `@prism/web` or just `prism-web` |
| Docker image | `prism-api`, `prism-web` |
| CLI (future) | `prism` |
| Database name | `prism` |
| Config prefix | `Prism:` |
| Window title | Prism — AI Research Workbench |
| README tagline | "See the full spectrum of your model's thinking." |

### Previous Working Name

"AI Research Workbench" — used throughout design docs during the planning phase. This was a description, not a name. Design docs may still reference "AI Research Workbench" in their descriptions, but all code artifacts use "Prism".

## Consequences

### Positive

- One syllable, easy to type, easy to say
- Metaphor maps perfectly to the core feature (logprobs = spectrum decomposition)
- Clean namespace: `Prism.Common.Results`, `Prism.Features.Playground`
- Works as a CLI: `prism status`, `prism replay --tag ner-tests`

### Negative

- "Prism" is a common English word — SEO may require qualification ("Prism AI Research" or "Prism Workbench")
- Some existing open-source projects use the name (Prism.js for syntax highlighting, PrismJS) — no conflict in AI/ML space

### Neutral

- Design documents (DESIGN.md, ARCHITECTURE.md, etc.) will be updated to reference Prism where appropriate
- The CLAUDE.md instructions and agent/skill guides use Prism as the project name

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Parallax | "Different angles on the same thing" — fits model comparison | Two syllables, less intuitive as a namespace | Prism is shorter and the metaphor is stronger |
| Spectra | Plural of spectrum, directly describes logprobs | Slightly awkward as a namespace (`Spectra.Api`) | Prism sounds better as a product name |
| Forge | Strong, tool-like | Generic, doesn't evoke the research/analysis aspect | Doesn't communicate what makes this platform unique |
| Crucible | Testing under pressure | Dark/intimidating tone, three syllables | Too long, connotation doesn't fit a research tool |
| Lumen | Light/insight | Two syllables, already used by several products | Namespace conflicts, less distinctive |
| Aperture | Controls how much you see | Already strongly associated with Portal (Valve) | Brand confusion |

## References

- See `ARCHITECTURE.md` — project structure uses `Prism.*` namespaces
