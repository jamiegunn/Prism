# Playground — Feature Documentation

The Inference Playground is the primary interactive interface for chatting with locally-hosted LLMs. It provides a multi-panel workspace for sending messages, tuning inference parameters, analyzing token-level statistics, and managing conversation history.

---

## Overview

| Aspect | Detail |
|--------|--------|
| Backend slice | `Features/Playground/` |
| Frontend slice | `frontend/src/features/playground/` |
| API prefix | `/api/v1/playground` |
| State management | Zustand (`usePlaygroundStore`) with localStorage persistence |
| Streaming | Server-Sent Events (SSE) via `useStreamChat` hook |

---

## Capabilities

### 1. Multi-Turn Chat with SSE Streaming

Send messages to any configured inference provider and receive responses streamed token-by-token via SSE.

- **Endpoint:** `POST /api/v1/playground/chat` (SSE stream)
- **Handler:** `SendMessageHandler` — builds the chat message list, calls `IInferenceProvider.StreamChatCompletionAsync`, records to history via `RecordingInferenceProvider`
- **Frontend:** `useStreamChat` hook manages the `EventSource`, accumulates tokens, and exposes `streamingContent`, `streamingTokens`, `isStreaming`, `error`, and `stop()`
- Supports multi-turn conversations — each message appends to an existing conversation or creates a new one
- Stop button aborts the stream mid-generation

### 2. Conversation Management (CRUD)

Full lifecycle management of conversation threads.

| Operation | Endpoint | Handler |
|-----------|----------|---------|
| List | `GET /conversations` | `ListConversationsHandler` |
| Get by ID | `GET /conversations/{id}` | `GetConversationHandler` |
| Delete | `DELETE /conversations/{id}` | `DeleteConversationHandler` |
| Export | `GET /conversations/{id}/export` | `ExportConversationHandler` |

- **Conversation History sidebar** — left panel listing all past conversations with search, pin, and delete
- **Search** — filters conversations by title/content
- **Pin** — keeps important conversations at the top of the list
- Selecting a conversation loads its full message history into the chat pane
- "New Conversation" button resets the chat state

### 3. Inference Parameters

All parameters are configurable from the right-side Parameter Sidebar. Each control has an info tooltip explaining what it does.

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| Model Instance | Dropdown | — | Selects which provider/model to use |
| Temperature | Slider 0–2 | 0.7 | Controls randomness of token selection |
| Top P | Slider 0–1 | 1.0 | Nucleus sampling threshold |
| Top K | Slider 0–200 | 0 (disabled) | Limits to top K most likely tokens |
| Max Tokens | Slider 1–4096 | 2048 | Maximum tokens to generate |
| Frequency Penalty | Slider 0–2 | 0 | Penalizes tokens by their frequency in the output |
| Presence Penalty | Slider 0–2 | 0 | Penalizes tokens that have already appeared |
| Stop Sequences | Tag input | [] | Sequences that halt generation |
| Enable Logprobs | Toggle | Off | Returns token-level log probabilities |
| Top Logprobs | Slider 1–20 | 5 | Number of alternative tokens per position (when logprobs enabled) |

Parameters are persisted to localStorage via Zustand's `persist` middleware with `partialize`.

### 4. System Prompt Editor

- Collapsible editor above the chat pane for setting a system-level prompt
- Content is included as the first message in the chat completion request
- Persisted in the Zustand store across sessions

### 5. Logprobs Visualization

When logprobs are enabled, each assistant message includes token-level probability data.

- **Token heatmap** — tokens in the chat pane are color-coded by confidence (green = high probability, red = low probability)
- **Logprobs Analysis panel** — collapsible bottom panel showing detailed per-token probability breakdown
- **Click-to-inspect** — clicking any assistant message opens its logprobs data in the bottom panel
- **Auto-select** — the last assistant message with logprobs data is auto-selected when generation completes

**Components:**
- `LogprobsPanel` — renders the detailed logprobs analysis (bar charts of alternative tokens)
- `ChatPane` — renders token heatmap overlay on assistant messages when logprobs data is present

### 6. Message Statistics

The Stats panel (toggled via the header bar) shows aggregate statistics for the current conversation.

- **Component:** `MessageStatsPanel` — displayed between the chat and parameter panels
- Shows per-message and conversation-level metrics:
  - Token counts (prompt tokens, completion tokens, total)
  - Perplexity scores (when logprobs enabled)
  - Timing information

### 7. Conversation Export

Export conversation data in multiple formats.

- **Endpoint:** `GET /conversations/{id}/export?format={format}`
- **Formats:** `json`, `csv`, `markdown`
- **Handler:** `ExportConversationHandler` — serializes conversation with all messages and metadata
- Downloads as a file via browser blob URL

---

## Frontend Components

| Component | File | Purpose |
|-----------|------|---------|
| `PlaygroundPage` | `PlaygroundPage.tsx` | Top-level layout: header, 3-column layout, bottom panel |
| `ChatPane` | `components/ChatPane.tsx` | Message list with streaming, token heatmap, logprobs click |
| `ChatInput` | `components/ChatInput.tsx` | Input bar with send/stop, disabled when no model selected |
| `ConversationHistory` | `components/ConversationHistory.tsx` | Left sidebar: list, search, pin, delete, new |
| `SystemPromptEditor` | `components/SystemPromptEditor.tsx` | Collapsible system prompt textarea |
| `ParameterSidebar` | `components/ParameterSidebar.tsx` | Right sidebar: all inference parameter controls |
| `MessageStatsPanel` | `components/MessageStatsPanel.tsx` | Conversation-level statistics |
| `LogprobsPanel` | `components/LogprobsPanel.tsx` | Per-token probability analysis |

### Layout

The page uses a flexible multi-panel layout:

```
+-----------------------------------------------------------+
|  [<] Inference Playground           [Stats] [Logprobs] [>] |
+--------+-----------------------------+--------+-----------+
|        |  System Prompt (collapsible) |        |           |
|  Conv  |-----------------------------| Stats  | Parameter |
|  List  |  Chat Messages (scrollable) | Panel  | Sidebar   |
| (left) |                             |        | (right)   |
|        |-----------------------------|        |           |
|        |  Logprobs Panel (bottom)    |        |           |
|        |-----------------------------|        |           |
|        |  [Error bar if any]         |        |           |
|        |-----------------------------|        |           |
|        |  Chat Input                 |        |           |
+--------+-----------------------------+--------+-----------+
```

All panels are independently togglable via header buttons.

---

## Backend Architecture

### Domain

- **Conversation** — aggregate root with title, model instance reference, timestamps
- **Message** — child entity: role (User/Assistant/System), content, token counts, logprobs data, sort order, perplexity

### Application

| Use Case | Type | Description |
|----------|------|-------------|
| `SendMessage` | Command (SSE) | Streams a chat completion, persists conversation + messages |
| `ListConversations` | Query | Paginated list with search/filter |
| `GetConversation` | Query | Single conversation with all messages |
| `DeleteConversation` | Command | Soft or hard delete |
| `ExportConversation` | Query | Serializes to requested format |

### Infrastructure

- `ConversationConfiguration` — EF Core table `playground_conversations`
- `MessageConfiguration` — EF Core table `playground_messages`
- All inference calls go through `IInferenceProvider` abstraction
- Calls auto-recorded by `RecordingInferenceProvider` decorator

---

## State Management

The `usePlaygroundStore` (Zustand) manages:

- Selected model instance ID
- All inference parameters (temperature, topP, topK, maxTokens, etc.)
- System prompt content
- Stop sequences list
- Logprobs toggle and topLogprobs count

Persisted fields are configured via `partialize` to avoid storing transient UI state.

---

## API Hooks

- `useConversation(id)` — TanStack Query hook for fetching a single conversation
- `useStreamChat()` — custom hook managing SSE streaming lifecycle
- Conversation list uses TanStack Query with `queryKey: ['playground', 'conversations']`
- Cache invalidation after successful message send

---

## Parameter Tooltips

Every parameter control uses the `ParamLabel` component which renders a label with an info icon. Hovering shows a portal-based tooltip explaining the parameter's effect on generation. See ADR-012 for the pattern.
