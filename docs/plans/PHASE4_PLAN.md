# Phase 4: Sprint (RAG & Structure) — Execution Plan

## Overview
Advanced retrieval-augmented generation and structured output research tools.
pgvector is already available (docker uses pgvector/pgvector:pg16). IVectorStore abstraction exists.

## Steps

### Step 1: Enable pgvector + RAG Domain Models + Migration
- Migration to `CREATE EXTENSION vector`
- `RagCollection` entity (name, description, embedding model, dimensions, chunk strategy config, distance metric)
- `RagDocument` entity (collection_id, filename, content_type, size, metadata)
- `RagChunk` entity (document_id, content, embedding vector, metadata, order_index, token_count)
- EF configurations with feature-prefixed tables (`rag_collections`, `rag_documents`, `rag_chunks`)
- pgvector column for embeddings, tsvector computed column for BM25
- **Status: COMPLETED**

### Step 2: Implement Chunking Strategies + Document Parsing
- `IChunkingStrategy` interface
- Fixed-size chunking (by characters/tokens with overlap)
- Sentence-boundary chunking (regex-based)
- Recursive chunking (paragraphs -> sentences -> fixed)
- Text extraction: TXT, MD, HTML parsers (regex-based, no heavy NuGet)
- **Status: COMPLETED**

### Step 3: Implement Embedding Generation + Vector Storage
- `IEmbeddingProvider` interface (separate from IInferenceProvider)
- OpenAI-compatible embedding endpoint support (vLLM /v1/embeddings)
- Batch embedding with configurable batch size (32 per batch)
- Store embeddings in rag_chunks via Pgvector.EntityFrameworkCore
- **Status: COMPLETED**

### Step 4: Implement Vector Search + BM25 + Hybrid
- Vector search via pgvector cosine distance ordering
- BM25 via PostgreSQL tsvector/tsquery full-text search with ts_rank
- Hybrid search: weighted combination of vector + BM25 with score normalization
- **Status: COMPLETED**

### Step 5: Implement RAG Pipeline + API Endpoints
- Full RAG pipeline: retrieve -> format context -> render prompt -> call model -> return with attribution
- 9 endpoints: collection CRUD (4), document ingest + list (2), query + RAG pipeline + stats (3)
- Uses InferenceProviderFactory for model calls (consistent with existing features)
- **Status: COMPLETED**

### Step 6: Structured Output Backend
- `JsonSchemaEntity` domain model (name, schema JSON, version)
- Guided decoding endpoint using vLLM `guided_json` via ResponseFormat on ChatRequest
- JSON schema validation on response (required fields, type checking)
- 5 endpoints: schema CRUD (4) + structured inference (1)
- **Status: COMPLETED**

### Step 7: Frontend — RAG Workbench
- Collection management page (create, configure, list, delete)
- Document upload with drag-and-drop
- Retrieval test panel (query + results with scores, search type toggle)
- Collection detail page with Documents/Search/Stats tabs
- **Status: COMPLETED**

### Step 8: Frontend — Structured Output
- Schema list with create dialog
- Structured output test panel (prompt + guided decoding + validation results)
- Validation display with check/cross icons
- **Status: COMPLETED**

### Step 9: Route/Sidebar Integration + Build Verification
- Added routes: /rag, /rag/:id, /structured-output
- Updated Sidebar: RAG Workbench and Structured Output now active
- Backend: 0 errors, 0 warnings
- Frontend: 0 new errors (pre-existing errors in generated token-explorer client)
- Migration: AddRagAndStructuredOutput generated
- **Status: COMPLETED**
