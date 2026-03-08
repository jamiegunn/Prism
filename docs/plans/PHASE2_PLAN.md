# Phase 2: Jog — Implementation Plan

**Goal:** Systematic prompt development, experiment tracking, and multi-model comparison.

**Three modules:** Prompt Engineering Lab (2.1), Experiment Tracker (2.2), Multi-Pane Playground (2.3)

---

## Dependencies & Build Order

```
Step 1:  Projects & Experiments (backend domain + CRUD)    ← foundation for everything
Step 2:  Prompt Lab (backend domain + CRUD + rendering)    ← depends on Projects
Step 3:  Run tracking + auto-log integration               ← depends on Experiments + Prompt Lab
Step 4:  Frontend: Experiment Tracker UI                    ← depends on Steps 1-3
Step 5:  Frontend: Prompt Lab UI                           ← depends on Steps 2-3
Step 6:  Multi-Pane Playground                             ← independent, but uses shared components
Step 7:  Cross-feature integration                         ← ties everything together
Step 8:  Polish & documentation                            ← final pass
```

---

## Step 1: Projects & Experiments Backend

**What:** Core domain models and CRUD for organizing research work.

### 1a. Domain Models

Create `Features/Experiments/Domain/`:

**`Project`** (aggregate root)
```
Id (Guid), Name (string 200), Description (string 2000 nullable),
IsArchived (bool), CreatedAt, UpdatedAt
```
- Table: `experiments_projects`

**`Experiment`**
```
Id (Guid), ProjectId (Guid FK), Name (string 200), Description (string 2000 nullable),
Status (enum: Active, Archived, Completed), Hypothesis (string nullable),
CreatedAt, UpdatedAt
```
- Table: `experiments_experiments`
- Index: ProjectId + CreatedAt

**`Run`**
```
Id (Guid), ExperimentId (Guid FK), Name (string nullable),
Model (string 500), InstanceId (Guid nullable FK),
Parameters (jsonb: RunParameters), PromptVersionId (Guid nullable FK),
Input (text), Output (text nullable), SystemPrompt (text nullable),
Metrics (jsonb: Dictionary<string, double>),
PromptTokens (int), CompletionTokens (int), TotalTokens (int),
Cost (decimal nullable), LatencyMs (long), TtftMs (int nullable),
TokensPerSecond (double nullable), Perplexity (double nullable),
LogprobsData (jsonb nullable), Status (enum: Pending, Running, Completed, Failed),
Error (string nullable), Tags (string[] jsonb),
FinishReason (string nullable), CreatedAt, UpdatedAt
```
- Table: `experiments_runs`
- Indexes: ExperimentId + CreatedAt, Status, Model
- GIN indexes: Metrics (jsonb), Tags (array)

**`RunParameters`** (value object, stored as jsonb)
```
Temperature, TopP, TopK, MaxTokens, StopSequences[],
FrequencyPenalty, PresencePenalty
```

### 1b. EF Configurations

- `ProjectConfiguration.cs` — table, indexes, constraints
- `ExperimentConfiguration.cs` — FK to Project, status conversion
- `RunConfiguration.cs` — FK to Experiment, jsonb columns, GIN indexes

### 1c. Migration

```bash
dotnet ef migrations add AddExperimentsFeature
```

### 1d. Application Layer — Project CRUD

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateProject | Command | `POST /api/v1/projects` |
| ListProjects | Query | `GET /api/v1/projects` |
| GetProject | Query | `GET /api/v1/projects/{id}` (includes experiment summary) |
| UpdateProject | Command | `PUT /api/v1/projects/{id}` |
| ArchiveProject | Command | `POST /api/v1/projects/{id}/archive` |

### 1e. Application Layer — Experiment CRUD

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateExperiment | Command | `POST /api/v1/experiments` |
| ListExperiments | Query | `GET /api/v1/experiments?projectId=X` |
| GetExperiment | Query | `GET /api/v1/experiments/{id}` (includes run summary: count, best metrics) |
| UpdateExperiment | Command | `PUT /api/v1/experiments/{id}` |
| ArchiveExperiment | Command | `POST /api/v1/experiments/{id}/archive` |

### 1f. Module Registration

- `ExperimentsModule.cs` → `AddExperimentsFeature()`
- Register in `Program.cs` and `WebApplicationExtensions.cs`

**Files to create:** ~20 files
**Estimated complexity:** Medium — standard CRUD, follows established patterns exactly

---

## Step 2: Prompt Engineering Lab Backend

**What:** Prompt templates with versioning, variable rendering, and test execution.

### 2a. Domain Models

Create `Features/PromptLab/Domain/`:

**`PromptTemplate`** (aggregate root)
```
Id (Guid), ProjectId (Guid nullable FK), Name (string 200),
Category (string 100 nullable), Description (string 2000 nullable),
Tags (string[] jsonb), LatestVersion (int),
CreatedAt, UpdatedAt
```
- Table: `prompts_templates`
- Index: Name, Category
- GIN index: Tags

**`PromptVersion`**
```
Id (Guid), TemplateId (Guid FK), Version (int, auto-increment per template),
SystemPrompt (text nullable), UserTemplate (text),
Variables (jsonb: PromptVariable[]),
FewShotExamples (jsonb: FewShotExample[]),
Notes (string 2000 nullable), CreatedAt
```
- Table: `prompts_versions`
- Unique index: TemplateId + Version

**`PromptVariable`** (value object)
```
Name (string), Type (string: "string"|"number"|"boolean"),
DefaultValue (string nullable), Description (string nullable),
Required (bool)
```

**`FewShotExample`** (value object)
```
Input (string), Output (string), Label (string nullable)
```

### 2b. Application Layer — Template CRUD

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateTemplate | Command | `POST /api/v1/prompts` |
| ListTemplates | Query | `GET /api/v1/prompts?category=X&tags=Y&search=Z` |
| GetTemplate | Query | `GET /api/v1/prompts/{id}` (with latest version) |
| UpdateTemplate | Command | `PUT /api/v1/prompts/{id}` (metadata only) |
| DeleteTemplate | Command | `DELETE /api/v1/prompts/{id}` |

### 2c. Application Layer — Version Management

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateVersion | Command | `POST /api/v1/prompts/{id}/versions` |
| ListVersions | Query | `GET /api/v1/prompts/{id}/versions` |
| GetVersion | Query | `GET /api/v1/prompts/{id}/versions/{v}` |
| DiffVersions | Query | `GET /api/v1/prompts/{id}/diff?v1=X&v2=Y` |

### 2d. Template Rendering Engine

`TemplateRenderer` in Application layer:
- Parse `{{variable_name}}` patterns in user template
- Validate all declared variables are provided
- Detect undeclared variables in template
- Render final prompt string
- Count tokens via provider's TokenizeAsync (optional)
- Build chat messages: [System(systemPrompt), ...FewShot pairs, User(rendered)]

### 2e. Prompt Test Endpoint

| Use Case | Type | Endpoint |
|----------|------|----------|
| TestPrompt | Command | `POST /api/v1/prompts/{id}/test` |

Request: `{ version?, variables: {}, instanceId, temperature, topP, ... , logprobs?, saveAsRun?: experimentId }`
- Renders template with variables
- Calls inference provider
- Returns response with metrics
- Optionally saves as an Experiment Run (cross-feature)

### 2f. A/B Test Endpoint

| Use Case | Type | Endpoint |
|----------|------|----------|
| StartAbTest | Command | `POST /api/v1/prompts/ab-test` |

Request: `{ variations: [{versionId, variables}], instanceIds[], parameterSets[], runsPerCombo }`
- Creates an Experiment under specified Project
- Enqueues all combinations as background work
- Returns experiment ID for polling
- Uses `BackgroundService` to execute (no Redis yet — in-memory queue)

**Files to create:** ~25 files
**Estimated complexity:** High — template rendering engine + cross-feature integration

---

## Step 3: Run Tracking & Auto-Log Integration

**What:** Complete Run CRUD, comparison, search/filter, and auto-logging from Playground/Prompt Lab.

### 3a. Run CRUD Endpoints

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateRun | Command | `POST /api/v1/experiments/{id}/runs` |
| ListRuns | Query | `GET /api/v1/experiments/{id}/runs?model=X&sortBy=perplexity&order=asc` |
| GetRun | Query | `GET /api/v1/experiments/{id}/runs/{runId}` |
| DeleteRun | Command | `DELETE /api/v1/experiments/{id}/runs/{runId}` |
| CompareRuns | Query | `POST /api/v1/experiments/{id}/compare` |

### 3b. Run Search & Filtering

`ListRuns` supports:
- Filter by: model, status, tags, date range, any metric key (e.g., `minPerplexity=2.5`)
- Sort by: any metric key, latency, tokens, cost, createdAt
- Pagination via `IPagedRequest`

### 3c. Run Comparison

`CompareRuns` accepts `{ runIds: [] }` and returns:
- Aligned parameter diff (which params changed between runs)
- Metric comparison table with deltas
- Output text side-by-side

### 3d. Auto-Log Integration

Modify `RecordingInferenceProvider` or add a new service `RunAutoLogger`:
- Accepts an optional `ExperimentId` + `RunName` in the chat request metadata
- After inference completes, creates a Run in the specified experiment
- Playground gets a "Save as Run" button that prompts for experiment selection
- Prompt Lab's test endpoint already creates runs when `saveAsRun` is specified

### 3e. Export Runs

| Use Case | Type | Endpoint |
|----------|------|----------|
| ExportRuns | Query | `GET /api/v1/experiments/{id}/runs/export?format=csv|json` |

**Files to create:** ~15 files
**Estimated complexity:** Medium-High — flexible filtering on jsonb is the tricky part

---

## Step 4: Frontend — Experiment Tracker

**What:** Full UI for Projects, Experiments, and Runs.

### 4a. Route & Sidebar Setup

- Add route: `/experiments` → `ExperimentsPage`
- Add route: `/experiments/:projectId` → `ProjectDetailPage`
- Add route: `/experiments/:projectId/:experimentId` → `ExperimentDetailPage`
- Add route: `/experiments/:projectId/:experimentId/:runId` → `RunDetailPage`
- Update Sidebar: unlock "Experiments" link

### 4b. Feature Structure

```
frontend/src/features/experiments/
  ExperimentsPage.tsx          — project list + create
  ProjectDetailPage.tsx        — experiment list for a project
  ExperimentDetailPage.tsx     — run table + analytics
  RunDetailPage.tsx            — full run detail
  api.ts                       — TanStack Query hooks
  store.ts                     — Zustand (selected project/experiment, view prefs)
  types.ts                     — TypeScript interfaces
  components/
    ProjectCard.tsx            — project summary card
    CreateProjectDialog.tsx    — create/edit project form
    ExperimentCard.tsx         — experiment summary with run count + best metric
    CreateExperimentDialog.tsx — create/edit experiment form
    RunTable.tsx               — sortable, filterable, selectable run table
    RunComparisonView.tsx      — side-by-side run comparison
    MetricChart.tsx            — scatter/bar chart for metrics (recharts)
    RunDetailPanel.tsx         — full I/O, params, metrics, logprobs
    ExperimentAnalytics.tsx    — summary stats, distributions, correlations
```

### 4c. Key Interactions

- **Run Table:** sortable columns, column visibility toggle, checkbox selection, bulk compare
- **Comparison View:** parameter diff highlighting, metric deltas (green/red), output text diff
- **Charts:** recharts library for scatter plots (metric vs metric), time series (metric over runs)
- **"Fork" button:** opens Playground with same model/params pre-filled
- **"Re-run" button:** re-executes with same config, saves as new run

### 4d. Dependencies

- Install `recharts` for chart visualizations
- Reuse `LogprobsPanel`, `TokenHeatmap` from playground components

**Files to create:** ~15 files
**Estimated complexity:** High — data-heavy UI with charts and comparison views

---

## Step 5: Frontend — Prompt Lab

**What:** Full prompt engineering interface with Monaco editor, versioning, and test execution.

### 5a. Route & Sidebar Setup

- Add route: `/prompt-lab` → `PromptLabPage`
- Add route: `/prompt-lab/:templateId` → `PromptEditorPage`
- Update Sidebar: unlock "Prompt Lab" link

### 5b. Feature Structure

```
frontend/src/features/prompt-lab/
  PromptLabPage.tsx             — template library browser
  PromptEditorPage.tsx          — main editor workspace
  api.ts                        — TanStack Query hooks
  store.ts                      — Zustand (current template, version, variables)
  types.ts                      — TypeScript interfaces
  components/
    TemplateLibrarySidebar.tsx   — searchable template list grouped by category
    CreateTemplateDialog.tsx     — create new template form
    MonacoPromptEditor.tsx       — Monaco editor with {{variable}} highlighting
    VariablePanel.tsx            — auto-detected variable input form
    VersionHistoryPanel.tsx      — version list + diff button
    VersionDiffView.tsx          — Monaco diff editor for two versions
    FewShotManager.tsx           — add/remove/reorder few-shot examples
    PromptPreview.tsx            — rendered prompt preview with token count
    TestPanel.tsx                — model selector, run button, response display
    AbTestConfigDialog.tsx       — matrix config for A/B testing
    PipelineBuilder.tsx          — prompt chain/pipeline editor (stretch goal)
```

### 5c. Monaco Editor Setup

- Install `@monaco-editor/react`
- Custom Monarch language for `{{variable}}` syntax highlighting
- Two editor panes: system prompt (top) + user template (bottom)
- Read-only preview pane showing rendered output

### 5d. Key Interactions

- **Live variable detection:** regex scan of `{{...}}` patterns → auto-populate variable panel
- **Live preview:** as user fills variables, preview updates in real-time with token count
- **Version management:** create new version, view diff between any two versions
- **Test execution:** select model + params, click Run, see response with logprobs (reuse ChatMessage component)
- **A/B Test:** configure matrix → launches background experiment → navigates to Experiment Tracker

### 5e. Dependencies

- Install `@monaco-editor/react` (~2MB, tree-shakes well)
- Reuse `ChatMessage`, `LogprobsPanel` from playground
- Reuse `ParamLabel`, parameter controls from playground's `ParameterSidebar`

**Files to create:** ~15 files
**Estimated complexity:** High — Monaco integration + live rendering + cross-feature navigation

---

## Step 6: Multi-Pane Playground

**What:** 1-4 side-by-side chat panes with linked input and comparison.

### 6a. Architecture

The current `PlaygroundPage` manages a single chat. Multi-pane creates a wrapper that manages N independent chat contexts.

```
frontend/src/features/playground/
  MultiPanePlayground.tsx       — NEW: manages 1-4 pane instances
  components/
    PlaygroundPane.tsx           — NEW: single pane (extracted from current PlaygroundPage logic)
    PaneControls.tsx             — NEW: add/remove pane, link toggle
    CompareOutputsPanel.tsx      — NEW: side-by-side response comparison
    LogprobsDiffView.tsx         — NEW: logprobs comparison between panes
```

### 6b. State Management

New Zustand store `useMultiPaneStore`:
```
panes: PaneConfig[]  — array of {id, instanceId, conversationId, parameters}
linkedInput: boolean — when true, send to all panes simultaneously
layout: '1' | '2' | '3' | '2x2'
activePaneId: string — which pane has focus
```

Each pane gets its own `useStreamChat` hook instance.

### 6c. Layout

```
1 pane:  [============================]
2 panes: [============|============]
3 panes: [========|========|========]
4 panes: [============|============]
         [============|============]
```

### 6d. Linked Input Mode

- Single input bar at the bottom (shared)
- On send: dispatches the same message to all panes' `useStreamChat.send()`
- Each pane streams independently
- When all complete: "Compare" button appears

### 6e. Comparison Features

- **Output comparison:** side-by-side text with differences highlighted
- **Metrics comparison:** table of token counts, latency, perplexity per pane
- **Logprobs diff:** overlay heatmaps aligned by token position

### 6f. Route

- Add route: `/playground/multi` → `MultiPanePlayground`
- Add toggle in Playground header: "Single" / "Multi" mode switch
- Or: make it a tab on the existing Playground page

**Files to create:** ~8 files
**Estimated complexity:** Medium — mostly frontend layout + state management

---

## Step 7: Cross-Feature Integration

**What:** Wire features together for seamless workflow.

### 7a. Playground → Experiment

- "Save as Run" button on any assistant message in Playground
- Dialog: select Project → Experiment (or create new)
- Captures: input, output, model, parameters, logprobs, metrics

### 7b. Prompt Lab → Experiment

- Test panel already saves to experiment when `saveAsRun` is specified
- A/B test creates experiment and navigates to tracker

### 7c. Experiment → Playground

- "Fork" button on any Run → opens Playground with pre-filled model, params, system prompt
- "Re-run" button → re-executes and saves as new run

### 7d. Prompt Lab → Playground

- "Open in Playground" button → loads rendered prompt as first message

### 7e. History → Experiment

- "Save to Experiment" button on any history record
- Creates a Run from the historical inference data

---

## Step 8: Polish & Documentation

### 8a. Help Panels

Add `HelpPanel` (ADR-011 pattern) to:
- Experiment Tracker pages (Projects, Experiments, Runs)
- Prompt Lab pages (Editor, Test Panel, A/B Test)
- Multi-Pane Playground

### 8b. Parameter Tooltips

Add `ParamLabel` (ADR-012 pattern) to all new configuration controls.

### 8c. Feature Documentation

Create per ADR-approved skill:
- `docs/features/prompt-lab.md`
- `docs/features/experiments.md`
- `docs/features/multi-pane-playground.md`

### 8d. Update Sidebar

Remove "Coming Soon" badges from Phase 2 features. Add proper icons and active states.

---

## New Dependencies

| Package | Where | Purpose |
|---------|-------|---------|
| `recharts` | Frontend | Charts for metric visualization |
| `@monaco-editor/react` | Frontend | Code editor for prompt templates |

No new backend packages needed. No Redis yet (Phase 3). No new infrastructure.

---

## Database Changes Summary

**3 new tables:**
- `experiments_projects` — research project organization
- `experiments_experiments` — experiment grouping under projects
- `experiments_runs` — individual inference runs with metrics

**2 new tables:**
- `prompts_templates` — prompt template metadata
- `prompts_versions` — versioned prompt content with variables

**Total: 5 new tables, 1 migration**

---

## Execution Order (Task-by-Task)

For a session-by-session approach, execute in this order:

### Session A: Experiments Backend (Steps 1a-1f)
1. Create `Features/Experiments/` domain models (Project, Experiment, Run)
2. Create EF configurations + migration
3. Create Project CRUD handlers + endpoints
4. Create Experiment CRUD handlers + endpoints
5. Register module

### Session B: Prompt Lab Backend (Steps 2a-2c)
6. Create `Features/PromptLab/` domain models (PromptTemplate, PromptVersion)
7. Create EF configurations + migration
8. Create Template CRUD handlers + endpoints
9. Create Version management handlers + endpoints
10. Register module

### Session C: Prompt Lab Engine (Steps 2d-2f)
11. Implement TemplateRenderer (variable parsing, rendering, validation)
12. Implement TestPrompt handler + endpoint
13. Implement A/B test handler + endpoint (with background execution)

### Session D: Run Tracking (Steps 3a-3e)
14. Create Run CRUD handlers + endpoints
15. Implement Run search/filter with jsonb querying
16. Implement Run comparison endpoint
17. Implement auto-log integration (SaveAsRun service)
18. Implement Run export

### Session E: Frontend — Experiments (Step 4)
19. Create experiments feature structure + routes
20. Build ProjectCard + CreateProjectDialog
21. Build ExperimentCard + CreateExperimentDialog
22. Build RunTable with sorting/filtering/selection
23. Build RunComparisonView
24. Build MetricChart (install recharts)
25. Build RunDetailPanel with logprobs reuse
26. Build ExperimentAnalytics

### Session F: Frontend — Prompt Lab (Step 5)
27. Create prompt-lab feature structure + routes
28. Build TemplateLibrarySidebar + CreateTemplateDialog
29. Build MonacoPromptEditor with {{variable}} highlighting (install @monaco-editor/react)
30. Build VariablePanel with auto-detection
31. Build VersionHistoryPanel + VersionDiffView
32. Build FewShotManager
33. Build PromptPreview with live rendering
34. Build TestPanel with response display
35. Build AbTestConfigDialog

### Session G: Multi-Pane Playground (Step 6)
36. Create MultiPanePlayground + useMultiPaneStore
37. Extract PlaygroundPane from existing PlaygroundPage
38. Build PaneControls (add/remove/link toggle)
39. Build linked input mode
40. Build CompareOutputsPanel
41. Build LogprobsDiffView

### Session H: Integration & Polish (Steps 7-8)
42. Wire Playground → Experiment (Save as Run)
43. Wire Experiment → Playground (Fork/Re-run)
44. Wire Prompt Lab → Experiment (test saves, A/B test navigation)
45. Wire History → Experiment (Save to Experiment)
46. Add help panels to all new pages
47. Add parameter tooltips to all new controls
48. Update sidebar (unlock Phase 2)
49. Create feature documentation
50. Final type-check + build verification

---

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Monaco editor bundle size | Tree-shake, lazy-load the editor route |
| JSONB querying performance | GIN indexes on metrics + tags, pagination |
| A/B test background execution | Simple in-memory queue for now, Redis in Phase 3 |
| Cross-feature coupling | Use IDs and navigation, not direct imports between feature slices |
| Multi-pane SSE management | Each pane manages its own EventSource independently |
| Template rendering injection | Strict `{{variable}}` regex, no code execution |
