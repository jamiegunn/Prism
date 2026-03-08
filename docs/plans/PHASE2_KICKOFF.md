# Phase 2 Kickoff Prompt

Use this prompt to start building **Phase 2 (Jog)** of Prism. Copy everything below the line and paste it into a new Claude Code session in the `C:\dev\AI_Research` directory.

---

## Prompt

You are continuing to build **Prism**, an all-in-one AI research platform. Phase 1 (Walk) is complete — the Playground, Token Explorer, Model Management, and History features are fully functional. Your job is to implement Phase 2 (Jog): **Prompt Engineering Lab**, **Experiment Tracker**, and **Multi-Pane Playground**.

**Read these files first** (in this order — do not skip any):
1. `CLAUDE.md` — your instructions for how to work on this project
2. `ARCHITECTURE.md` — the full architecture (structure, patterns, abstractions, interfaces)
3. `PROJECT_PLAN.md` — Phase 2 task breakdown (tasks 2.1.x through 2.3.x)
4. `DESIGN.md` — full design document (data models, API surface, wireframes)
5. `docs/plans/PHASE2_PLAN.md` — detailed Phase 2 implementation plan with domain models, endpoints, and execution order

After reading all docs, execute Phase 2 in the following order. Do not skip ahead. Complete each section before moving to the next.

---

### Step 1: Projects & Experiments Backend (COMPLETED)

**Status: Done.** The Experiments feature slice has been implemented:

- Domain: `Project`, `Experiment`, `Run`, `RunParameters`, `ExperimentStatus`, `RunStatus`
- Tables: `experiments_projects`, `experiments_experiments`, `experiments_runs`
- EF Configurations with jsonb columns (Parameters, Metrics, Tags), GIN indexes
- Migration: `AddExperimentsFeature`
- Project CRUD: Create, List, Get, Update, Archive — 5 handlers + endpoints
- Experiment CRUD: Create, List, Get, Update, ChangeStatus — 5 handlers + endpoints
- Endpoints: `/api/v1/projects` (5 endpoints), `/api/v1/experiments` (5 endpoints)
- Module: `ExperimentsModule.cs` registered in `ServiceCollectionExtensions` and `WebApplicationExtensions`

---

### Step 2: Prompt Engineering Lab Backend (Steps 2a-2c) (COMPLETED)

**Status: Done.** The PromptLab feature slice has been implemented.

**Backend — `Features/PromptLab/`:**

**Domain:**
- `PromptTemplate` (aggregate root): Id, ProjectId (nullable FK to experiments_projects), Name (200), Category (100 nullable), Description (2000 nullable), Tags (string[] jsonb), LatestVersion (int), CreatedAt, UpdatedAt
  - Table: `prompts_templates`, Index: Name, Category, GIN: Tags
- `PromptVersion`: Id, TemplateId (FK), Version (int auto-increment per template), SystemPrompt (text nullable), UserTemplate (text), Variables (jsonb: PromptVariable[]), FewShotExamples (jsonb: FewShotExample[]), Notes (2000 nullable), CreatedAt
  - Table: `prompts_versions`, Unique index: TemplateId + Version
- `PromptVariable` (value object): Name, Type ("string"|"number"|"boolean"), DefaultValue (nullable), Description (nullable), Required (bool)
- `FewShotExample` (value object): Input, Output, Label (nullable)

**Application — Template CRUD:**

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateTemplate | Command | `POST /api/v1/prompts` |
| ListTemplates | Query | `GET /api/v1/prompts?category=X&tags=Y&search=Z` |
| GetTemplate | Query | `GET /api/v1/prompts/{id}` (with latest version) |
| UpdateTemplate | Command | `PUT /api/v1/prompts/{id}` (metadata only) |
| DeleteTemplate | Command | `DELETE /api/v1/prompts/{id}` |

**Application — Version Management:**

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateVersion | Command | `POST /api/v1/prompts/{id}/versions` |
| ListVersions | Query | `GET /api/v1/prompts/{id}/versions` |
| GetVersion | Query | `GET /api/v1/prompts/{id}/versions/{v}` |
| DiffVersions | Query | `GET /api/v1/prompts/{id}/diff?v1=X&v2=Y` |

**Module:** `PromptLabModule.cs` with `AddPromptLabFeature()` — register in `Program.cs` and `WebApplicationExtensions.cs`.

Generate the EF migration for PromptTemplate and PromptVersion.

---

### Step 3: Prompt Lab Engine (Steps 2d-2f) (COMPLETED)

**Status: Done.** Template rendering engine and test execution implemented.

**TemplateRenderer** (Application layer service):
- Parse `{{variable_name}}` patterns in user template
- Validate all declared variables are provided
- Detect undeclared variables in template
- Render final prompt string with variable substitution
- Count tokens via provider's `TokenizeAsync` (optional)
- Build chat messages: `[System(systemPrompt), ...FewShot pairs, User(rendered)]`

**TestPrompt endpoint:**
- `POST /api/v1/prompts/{id}/test`
- Request: `{ version?, variables: {}, instanceId, temperature, topP, ..., logprobs?, saveAsRun?: experimentId }`
- Renders template with variables, calls inference provider, returns response with metrics
- Optionally saves as an Experiment Run (cross-feature integration)

**A/B Test endpoint:**
- `POST /api/v1/prompts/ab-test`
- Request: `{ variations: [{versionId, variables}], instanceIds[], parameterSets[], runsPerCombo }`
- Creates an Experiment under specified Project
- Enqueues all combinations as background work (in-memory queue, no Redis)
- Returns experiment ID for polling

---

### Step 4: Run Tracking & Auto-Log Integration (COMPLETED)

**Status: Done.** Run lifecycle complete.

**Run CRUD:**

| Use Case | Type | Endpoint |
|----------|------|----------|
| CreateRun | Command | `POST /api/v1/experiments/{id}/runs` |
| ListRuns | Query | `GET /api/v1/experiments/{id}/runs?model=X&sortBy=perplexity&order=asc` |
| GetRun | Query | `GET /api/v1/experiments/{id}/runs/{runId}` |
| DeleteRun | Command | `DELETE /api/v1/experiments/{id}/runs/{runId}` |
| CompareRuns | Query | `POST /api/v1/experiments/{id}/compare` |
| ExportRuns | Query | `GET /api/v1/experiments/{id}/runs/export?format=csv|json` |

**Run Search/Filter:**
- Filter by: model, status, tags, date range, any metric key (e.g., `minPerplexity=2.5`)
- Sort by: any metric key, latency, tokens, cost, createdAt
- Pagination via `IPagedRequest`

**Run Comparison:**
- `CompareRuns` accepts `{ runIds: [] }` and returns parameter diff, metric comparison, output text alignment

**Auto-Log Integration:**
- Add a `RunAutoLogger` service that accepts optional `ExperimentId` + `RunName` metadata
- After inference completes in Playground, optionally create a Run
- Playground gets a "Save as Run" button

**Export:**
- CSV and JSON export formats for experiment runs

---

### Step 5: Frontend — Experiment Tracker (COMPLETED)

**Status: Done.** 14 files created. Full experiments UI with project/experiment/run CRUD, comparison, and charts.

**Routes:**
- `/experiments` → `ExperimentsPage` (project list)
- `/experiments/:projectId` → `ProjectDetailPage`
- `/experiments/:projectId/:experimentId` → `ExperimentDetailPage`
- `/experiments/:projectId/:experimentId/:runId` → `RunDetailPage`

**Components:**
- `ProjectCard.tsx` — project summary card with experiment count
- `CreateProjectDialog.tsx` — create/edit project form
- `ExperimentCard.tsx` — experiment summary with run count + best metric
- `CreateExperimentDialog.tsx` — create/edit experiment form
- `RunTable.tsx` — sortable, filterable, selectable run table
- `RunComparisonView.tsx` — side-by-side parameter diff + metric deltas + output diff
- `MetricChart.tsx` — scatter/bar chart using recharts
- `RunDetailPanel.tsx` — full I/O, params, metrics, logprobs (reuse LogprobsPanel)
- `ExperimentAnalytics.tsx` — summary stats, distributions

**Dependencies:**
- Install `recharts` for chart visualizations
- Reuse `LogprobsPanel`, `TokenHeatmap` from playground

---

### Step 6: Frontend — Prompt Lab (COMPLETED)

**Status: Done.** 10 files created. Prompt Lab with template list, Monaco editor, version management, variable panel, and test panel.

**Routes:**
- `/prompt-lab` → `PromptLabPage` (template library)
- `/prompt-lab/:templateId` → `PromptEditorPage` (editor workspace)

**Components:**
- `TemplateLibrarySidebar.tsx` — searchable template list grouped by category
- `CreateTemplateDialog.tsx` — create new template form
- `MonacoPromptEditor.tsx` — Monaco editor with `{{variable}}` highlighting
- `VariablePanel.tsx` — auto-detected variable input form
- `VersionHistoryPanel.tsx` — version list + diff button
- `VersionDiffView.tsx` — Monaco diff editor for two versions
- `FewShotManager.tsx` — add/remove/reorder few-shot examples
- `PromptPreview.tsx` — rendered prompt preview with token count
- `TestPanel.tsx` — model selector, run button, response display
- `AbTestConfigDialog.tsx` — matrix config for A/B testing

**Dependencies:**
- Install `@monaco-editor/react`
- Custom Monarch language for `{{variable}}` syntax highlighting
- Reuse `ChatMessage`, `LogprobsPanel` from playground

---

### Step 7: Multi-Pane Playground (COMPLETED)

**Status: Done.** Multi-pane comparison view at `/playground/compare` with shared input and independent streaming panes.

**Components:**
- `MultiPanePlayground.tsx` — manages 1-4 pane instances
- `PlaygroundPane.tsx` — single pane (extracted from current PlaygroundPage logic)
- `PaneControls.tsx` — add/remove pane, link toggle, layout selector
- `CompareOutputsPanel.tsx` — side-by-side response comparison
- `LogprobsDiffView.tsx` — logprobs comparison between panes

**State:** `useMultiPaneStore` — panes array, linkedInput, layout ('1'|'2'|'3'|'2x2'), activePaneId

**Route:** `/playground/multi` with toggle in Playground header for Single/Multi mode

**Linked Input Mode:**
- Single shared input bar at the bottom
- On send: dispatches same message to all panes simultaneously
- Each pane streams independently with its own `useStreamChat` hook

---

### Step 8: Cross-Feature Integration & Polish

Wire features together:

**Cross-Feature Navigation:**
- Playground → Experiment: "Save as Run" button on assistant messages
- Experiment → Playground: "Fork" button on runs (opens Playground with pre-filled config)
- Prompt Lab → Experiment: test saves to experiment, A/B test creates experiment
- Prompt Lab → Playground: "Open in Playground" button
- History → Experiment: "Save to Experiment" button on history records

**Polish:**
- Add help panels (ADR-011) to all new pages
- Add parameter tooltips (ADR-012) to all new controls
- Update sidebar: unlock Phase 2 features, remove "Coming Soon" badges
- Create feature documentation: `docs/features/prompt-lab.md`, `docs/features/experiments.md`, `docs/features/multi-pane-playground.md`
- Final type-check + build verification

---

## New Dependencies

| Package | Where | Purpose |
|---------|-------|---------|
| `recharts` | Frontend | Charts for metric visualization |
| `@monaco-editor/react` | Frontend | Code editor for prompt templates |

No new backend packages. No Redis yet (Phase 3). No new infrastructure.

## Database Changes

**5 new tables (2 migrations):**
- `experiments_projects` — research project organization (DONE)
- `experiments_experiments` — experiment grouping under projects (DONE)
- `experiments_runs` — individual inference runs with metrics (DONE)
- `prompts_templates` — prompt template metadata
- `prompts_versions` — versioned prompt content with variables

## Important Reminders

- **ARCHITECTURE.md is the source of truth** for project structure, naming, and patterns.
- **Every public type/method needs XML doc comments.** The compiler will enforce this.
- **All handlers return Result<T>.** No exceptions for expected failures.
- **No raw SQL, no System.IO.File, no IMemoryCache in feature code.** Use the abstractions.
- **Feature-prefixed tables:** `experiments_projects`, `prompts_templates`, etc.
- **Structured logging with named properties.** Never string interpolation in log calls.
- **Cross-feature references use IDs and navigation, not direct imports between feature slices.**
- **Reuse existing components** — LogprobsPanel, TokenHeatmap, ChatMessage, ParameterSidebar controls.
