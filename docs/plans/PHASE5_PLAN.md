# Phase 5: Fly (Agents & Integration) — Execution Plan

## Overview
Agent workflows, research notebooks, and fine-tuning support.
Builds on existing InferenceProviderFactory, RAG pipeline, and dataset features.

## Steps

### Step 1: Agent Domain Models + Migration
- `AgentWorkflow` entity (Name, Description, SystemPrompt, Model, Pattern enum, MaxSteps, TokenBudget, ToolConfig JSON, GuardrailConfig JSON, Version)
- `AgentRun` entity (WorkflowId, Status enum, Input, Output, Steps JSON, TotalTokens, TotalLatencyMs, ErrorMessage)
- `AgentRunStatus` enum: Pending, Running, Completed, Failed, Cancelled
- `AgentPatternType` enum: ReAct, Sequential
- EF configurations with feature-prefixed tables (`agent_workflows`, `agent_runs`)
- JSONB columns for Steps, ToolConfig, GuardrailConfig
- **Status: COMPLETED**

### Step 2: Implement Tool Registry + Built-in Tools
- `IAgentTool` interface (Name, Description, ParameterSchema, ExecuteAsync)
- `AgentToolRegistry` service to register and resolve tools by name
- Built-in tools: Calculator (expression eval), RAGQuery (calls RAG pipeline), ApiCall (HTTP), Echo (testing)
- Tool result model: `ToolResult` record (Success, Output, Error)
- **Status: COMPLETED**

### Step 3: Implement ReAct Agent Pattern
- `ReActExecutor` — Thought-Action-Observation loop
- System prompt template for ReAct format
- LLM output parser: extract Thought, Action (tool name + input), Final Answer
- Step logging: each step captures thought, action, observation, token usage
- Stop conditions: Final Answer, max steps, token budget exceeded
- Uses InferenceProviderFactory for model calls
- **Status: COMPLETED**

### Step 4: Agent Execution Endpoints + SSE Streaming
- 8 endpoints: workflow CRUD (4), run execution (POST), run detail (GET), list runs (GET), SSE stream (GET)
- SSE endpoint streams steps in real-time during execution
- Background execution via Task.Run with step-by-step SSE events
- **Status: COMPLETED**

### Step 5: Frontend — Agent Builder
- Workflow list page with create/edit/delete
- Workflow config editor (system prompt, model, tools, parameters)
- Tool selection panel with parameter schemas
- Test input area with "Run" button
- **Status: COMPLETED**

### Step 6: Frontend — Execution Trace Viewer
- Step-by-step display: Thought → Action → Observation
- Expandable/collapsible steps
- Token counter per step and cumulative
- Live streaming updates via SSE
- Run history list
- **Status: COMPLETED**

### Step 7: Fine-Tuning Format Export
- Extend dataset export with fine-tuning formats
- Alpaca: instruction/input/output JSON
- ShareGPT: conversations[] format
- ChatML: with role tokens
- OpenAI JSONL: messages[] format
- Validation: required fields, non-empty values, token length checks
- **Status: COMPLETED**

### Step 8: LoRA Adapter Registration
- `LoraAdapter` entity (Name, InstanceId, AdapterPath, BaseModel, Description)
- Register/unregister adapter paths with vLLM instances
- EF config with `finetuning_lora_adapters` table
- 4 endpoints: CRUD for adapters
- **Status: COMPLETED**

### Step 9: Frontend — Export Wizard + Model Comparison
- Step-by-step export wizard: dataset → split → format → column mapping → validate → export
- LoRA adapter management page
- "Compare base vs fine-tuned" action opening 2-pane playground
- **Status: COMPLETED**

### Step 10: Research Notebooks + JupyterLite Integration
- `Notebook` entity (Name, Description, Content as JSONB, Version, SizeBytes, KernelName)
- EF config with `notebooks` table
- 6 endpoints: CRUD (4) + download .ipynb (1) + list (1)
- Default .ipynb template with Pyodide kernel metadata
- `workbench.py` Python helper module for JupyterLite: chat, logprobs, datasets, RAG, experiments
- Frontend: Notebook list page, detail page with cell viewer, JSON editor, JupyterLite launcher
- JupyterLite setup script for building static assets
- **Status: COMPLETED**

### Step 11: Route/Sidebar Integration + Build Verification
- Added routes: /agents, /agents/:id, /fine-tuning, /notebooks, /notebooks/:id
- Updated Sidebar: Agents, Fine-Tuning, and Notebooks now active (all items active)
- Backend: 0 errors, 0 warnings
- Frontend: 0 new TypeScript errors
- Migrations: AddAgentsAndFineTuning + AddNotebooks generated
- **Status: COMPLETED**
