# RAG Workbench

**Index your documents, then find out whether retrieval is finding the right ones.**

Most bad RAG answers are not generation failures. The model was given three irrelevant chunks
and did the best it could with them. The RAG Workbench exists so you can look at the chunks
before you blame the model: what got indexed, what a query actually retrieves, and what the
scores were.

Sidebar: **RAG Workbench**.

The pipeline is four steps, in order: **create a collection → open it → ingest documents →
search**. Each one has something in it that will bite you, so it is worth reading in order too.

---

## Before you start

**Prism ships with a demo collection called "AI Research Papers", and a Vector search on it
returns nothing.** The seeded chunks have no embeddings — the text is there, the vectors are
not — and vector search skips any chunk without one. You get zero results, no error, no
explanation. This is the first thing most people hit on this page and it is not a sign that
anything is broken.

To see the demo collection do something, switch the search mode to **BM25**, which reads the
text. For anything real, make your own collection.

**You need an embedding server.** This is the other thing to settle before you start, and it is
easy to get wrong:

- The embedding endpoint is **completely separate from the chat instance** you selected
  elsewhere in Prism. It is resolved on its own, each time, from: an explicit `Embedding:BaseUrl`
  or `Inference:DefaultEndpoint` in configuration, then the **oldest registered inference
  instance**, then `http://localhost:8000` as a last resort. Neither configuration key exists in
  the shipped `appsettings.json`, so in practice it is your oldest registered instance.
- It calls `POST /v1/embeddings` on whatever that resolves to.
- **No API key is ever sent.** Hosted OpenAI cannot be used, despite `text-embedding-3-small`
  being the default model name in the create dialog. Point it at a local server — vLLM or
  Ollama serving an embedding model — and set the model name to something that server actually
  has, such as `nomic-embed-text`.

If embedding fails, ingestion fails, and search fails. Get this right first.

---

## Create a collection

**New Collection** opens the **Create RAG Collection** dialog.

| Field | Default | Notes |
|---|---|---|
| **Name** | empty | Required. The Create button stays disabled until it is filled in. |
| **Description** | empty | Free text, shown on the collection card. |
| **Embedding Model** | `text-embedding-3-small` | Required. Change it. This name is sent verbatim to your embedding server, and the default names a model no local server has. |
| **Dimensions** | 1536 | Stored and displayed. Never used. |
| **Chunking** | Recursive | Recursive / Sentence / Fixed. See the table below. |
| **Chunk Size** | 512 | **Characters, not tokens** — roughly 128 tokens of English, not 512. |
| **Overlap** | 50 | Characters. Read the warning below before raising it. |
| **Distance Metric** | Cosine | Cosine / Euclidean / Inner Product. Stored. Never used. |

> **Dimensions and Distance Metric do nothing.** Both are saved on the collection and shown on
> the card and header, and neither is consulted by anything. Search is always cosine similarity,
> and the vector column has no fixed dimension, so choosing Euclidean and 768 gives you exactly
> the same search as Cosine and 1536. The dimensionality your embeddings actually have is
> whatever your embedding server returns.

Chunk Size being in characters is the setting people most often misread. If you are used to
thinking in tokens, divide by about four: the default 512 characters is a short paragraph, not a
page.

---

## Ingest documents

Open a collection and you land on the **Documents** tab, with a dashed drop zone: drag files
onto it, or click to open a file picker.

**Supported formats: `.txt`, `.md`, `.html`.** HTML has script and style blocks removed, tags
stripped and the common entities decoded. Plain text and Markdown are read as-is.

**Markdown syntax is not stripped.** Every `#`, `|`, `*` and backtick is embedded along with the
prose. For a document that is mostly paragraphs this matters little; for a document that is
mostly tables, a substantial fraction of what you have indexed is pipe characters. If retrieval
quality on a Markdown-heavy corpus is poor, converting to plain text first is worth trying.

### The drag-and-drop path does not check the file type

The click-to-upload path filters the picker to the supported extensions. **The drop path applies
no filter at all** and sends whatever you drop straight to the server, which then decides based
on the MIME type the browser attached to the file.

That produces two different bad outcomes:

- **A file the browser can type as something unsupported is rejected — silently.** Drop a PDF, a
  DOCX, a CSV or a JSON file and the server refuses it with an error the page never displays.
  Nothing appears in the document list, no message, no red pill. The upload area returns to
  idle as though you had never dropped anything.
- **A file the browser cannot type is accepted and read as text.** Anything the browser reports
  as `application/octet-stream` or does not type at all — most binary formats with unusual
  extensions, and any file with no extension — is decoded as UTF-8, chunked, embedded and
  indexed. It finishes with a green **Completed** pill and a plausible chunk count. What is in
  those chunks is mojibake, and it will keep turning up in your search results at respectable
  similarity scores forever.

There is no way to tell the second case from a real document by looking at the Documents tab.
Use the click-to-upload path, and if you must drop files, check afterwards that the character
count on the row looks like text rather than binary.

### Ingestion is synchronous

The HTTP request stays open through parsing, chunking, and every embedding batch (32 chunks per
call to your embedding server). There is no progress indicator beyond "Uploading &
processing...", no cancel, and no background job. A large file will sit there for minutes and
can time out.

Feed it small files. If you have a big corpus, split it before uploading.

If ingestion fails part-way — most often because the embedding server rejected the request — the
document appears in the list with a red **Failed** pill. The reason is recorded in the database
and never shown. The backend log has it.

---

## Chunking strategies

| Strategy | How it splits | What to know |
|---|---|---|
| `fixed` | A sliding window of exactly Chunk Size characters, stepping forward by Chunk Size minus Overlap. | The only strategy that honours Overlap properly and records exact character offsets into the source. It also cuts mid-word and mid-sentence, every time. |
| `sentence` | Accumulates whole sentences until adding another would exceed Chunk Size, then keeps trailing sentences as overlap. | Sentence boundaries are detected as punctuation followed by whitespace, so `Dr. Smith`, `e.g.` and `Fig. 3` all split. English-centric. Rejoins sentences with single spaces, so the chunk text no longer matches the original whitespace and the recorded offsets are approximate. |
| `recursive` (default) | Splits on blank lines, then single newlines, then `. `, then spaces, taking the first level that produces pieces small enough; then merges adjacent small pieces back together up to Chunk Size. | Produces the most natural-looking chunks on ordinary prose. **It ignores your Overlap setting entirely** for such text — overlap is only applied in the last-resort fixed-size fallback, which paragraphed prose never reaches. Set Overlap to whatever you like; on `recursive` it will be zero. |

If you specifically need overlap — because your answers straddle chunk boundaries — use `fixed`
and accept the ragged edges, or use `sentence`. Choosing `recursive` and raising Overlap is a
no-op that reads like a decision.

> **Setting Overlap greater than or equal to Chunk Size is accepted, and with `fixed` it is
> catastrophic.** The step between chunks is Chunk Size minus Overlap, floored at 1. At equal
> values the step becomes 1 character, so a 50 KB document produces roughly 50,000 chunks, each
> one 512 characters long and nearly identical to its neighbour. Every one of them is sent to
> your embedding server. Nothing warns you, and there is no way to delete the document
> afterwards.
>
> Keep Overlap well below Chunk Size. Ten to twenty percent is a reasonable starting point.

---

## Search

The **Search & RAG** tab has a query box, a mode dropdown, a top-K box (default 5), and a
magnifier button. Enter runs the search.

Each result shows the source filename, the score to four decimal places, the chunk content, its
token count and its chunk number within the document.

### The three modes

| Mode | Finds | Misses |
|---|---|---|
| **Vector** | Semantic matches. A query about "cancelling a subscription" retrieves a chunk about "ending your plan". | Exact identifiers. Error codes, part numbers, function names and rare proper nouns are frequently not retrieved at all, because they carry little semantic signal. |
| **BM25** | Exact and stemmed term matches, ranked by term frequency against document frequency. Excellent for identifiers and jargon. | Paraphrase. A chunk that answers the question without using any of its words scores zero. |
| **Hybrid** | Both, merged. Each side's scores are normalised to its own maximum and combined. | Nothing in particular — it is the sensible default for a corpus you do not know well. |

Two fixed choices in Hybrid mode:

- **The weighting is 70% vector, 30% BM25, and the UI cannot change it.** The API accepts a
  `vectorWeight` on the query body; the search panel never sends one. To try a different
  balance, call `POST /api/v1/rag/collections/{id}/query` with `"vectorWeight": 0.4` and
  compare.
- Because the two score scales are normalised separately, the hybrid score is a blended rank
  number between 0 and 1 and is not comparable to a raw cosine similarity or a raw BM25 rank.
  Compare hybrid scores to other hybrid scores only.

**BM25 is hardcoded to English stemming.** The full-text index is built with the `english`
configuration and queries are parsed the same way. On a German, French or Chinese corpus, BM25
still returns results — it degrades to something close to exact token matching rather than
failing — but the stemming is working against you. Use Vector mode on non-English content.

### Every vector search is a full scan

There is no approximate-nearest-neighbour index on the embeddings, and there cannot be one as
things stand: pgvector can only build HNSW or IVFFlat on a column declared with a fixed
dimension, and the column is deliberately dimensionless so that different collections can use
different embedding sizes. Every vector query therefore compares the query vector against every
chunk in the collection, one at a time.

This is completely fine for thousands of chunks and it will not hold for millions. If searches
start taking seconds, that is why, and no setting on this page will fix it.

---

## The generation step

The tab is called **Search & RAG**, and both halves now work. Under the search box, **Answer
with** picks a server and **Retrieve & answer** runs the whole pipeline: retrieve, assemble the
context, generate. The answer appears above its sources with the model, token count and latency,
so you can see what produced it and read the chunks it was grounded in.

For a long time only the retrieval half was wired up. The endpoint, the `useRagPipeline` hook and
the `RagPipelineResult` type all existed, and nothing called them — the page retrieved chunks and
stopped, on the tab named after doing more than that. The endpoint was also unusable without
naming a model explicitly: it passed the request's model straight through, so leaving it out sent
an empty one and came back as a 503 carrying Ollama's own `model is required`. It now falls back
to the model the chosen instance runs, which is what the instance is for.

The same pipeline is available directly:

```bash
curl -X POST http://localhost:5000/api/v1/rag/collections/<collection-id>/rag \
  -H "Content-Type: application/json" \
  -d '{
    "query": "What problem does multi-head attention solve?",
    "model": "llama3.1:8b",
    "instanceId": "9b2f77c4-1d3a-4e58-8c07-2a6d5f0e1122",
    "systemPrompt": null,
    "promptTemplate": null,
    "topK": 5,
    "searchType": "hybrid",
    "temperature": null,
    "maxTokens": null
  }'
```

Points worth knowing:

- **`instanceId` is required** and must be a real registered instance — get one from
  `GET /api/v1/models/instances`. Unlike Evaluation and Batch Inference, this endpoint lets you
  choose, and refuses if the ID does not exist.
- **`model` is optional.** Omit it and the instance's own model is used. Naming one overrides
  that. If neither names a model, the failure says so rather than passing a blank to the server.
- Defaults are **temperature 0.1** and **2048 max tokens** when you pass `null`. Logprobs are
  requested with 5 alternatives per token, so the call also produces the data the
  [Playground](playground.md) analysis views use, viewable afterwards in
  [History](history.md).
- `promptTemplate` supports `{{context}}` and `{{query}}` placeholders. The built-in template
  numbers each retrieved chunk with its source filename and instructs the model to say so when
  the answer is not in the context.
- **The response includes `renderedPrompt`** — the exact text the model was given, context and
  all. Read it. It is the fastest way to discover that your retrieval put three irrelevant
  chunks in front of the model, which is a different problem from the model answering badly.
- The call is recorded as a RAG trace, so the query, the retrieved chunks, the assembled context
  and the response are kept.

---

## Statistics

The **Statistics** tab shows **Documents**, **Chunks**, **Total Characters**, **Avg Chunk
Size**, **Est. Tokens**, and one card per document status (`Docs: Completed`, `Docs: Failed`,
and so on).

**Est. Tokens** is characters divided by four, summed across chunks. It is an approximation, not
a tokenizer count. For a real count use the [Tokenizer](tokenizer.md).

**Avg Chunk Size** is total source characters divided by chunk count, which is not the average
size of a chunk. With overlap in play the chunks collectively contain more characters than the
source document does, so this figure **understates** real chunk size — the more overlap you
configured, the more it understates. On `recursive`, where overlap is ignored, it is roughly
right.

---

## Why bother

The value of this page is diagnostic, and it is worth being blunt about what the diagnosis
usually is: **when a RAG system gives bad answers, retrieval is the problem far more often than
generation.** The model is handed a context and asked to answer from it. If the right passage
is not in that context, no amount of prompt engineering or model upgrading will produce the
right answer, and every hour spent on the generation side is wasted.

So before you change the model, run the query here and read what comes back:

- **Is the right chunk in the results at all?** If not, the problem is upstream — the document
  is not indexed, or it is chunked so that the answer is split across a boundary, or the query
  and the passage share no vocabulary and you are in BM25 when you need Vector.
- **What are the scores?** A top result at 0.82 and a fifth at 0.79 means the retriever cannot
  really tell them apart, and top-K is doing the choosing. A top result at 0.91 with a sharp
  drop-off means retrieval is confident. Both are useful; the flat one tells you to look at your
  chunking.
- **Does the same query work in a different mode?** A query that fails on Vector and succeeds on
  BM25 is telling you it hinges on an exact term. The reverse is telling you it hinges on
  meaning. That distinction should drive your choice of mode for the whole corpus.
- **Read the chunk text.** Half of all retrieval problems are visible the moment you look at
  what was actually indexed: half a sentence, a page of Markdown table pipes, a heading with no
  body, or the tail end of a binary file that got dropped in by mistake.

Only once the right chunks are coming back at sensible scores is it worth calling the pipeline
endpoint and looking at the generated answer.

---

## What this page will not do

- **No generation in the UI.** Retrieval only; the pipeline is API-only.
- **The seeded "AI Research Papers" collection has no embeddings**, so Vector search on it
  returns nothing.
- **Dimensions and Distance Metric are stored and ignored.** Search is always cosine.
- **The embedding endpoint sends no API key**, so hosted embedding providers cannot be used.
- **Drag-and-drop bypasses the file-type filter**, silently rejecting some unsupported files and
  silently indexing others as garbage.
- **Ingestion is synchronous**, with no progress and no cancel.
- **`recursive` ignores the Overlap setting** on ordinary prose.
- **No document delete and no re-index.** Uploading a corrected version of a file adds a second
  copy; both stay in the index and both compete in search results. The only way to remove a
  document is to delete the whole collection and rebuild it.
- **Ingest failure reasons are hidden.** A red **Failed** pill, nothing more.
- **No export.** Not chunks, not embeddings, not search results, not statistics.
- **The counters on the collection card can drift from the Statistics tab.** The card's
  document and chunk counts are incremented on successful ingest only, while Statistics counts
  rows directly, so failed uploads make the two disagree. Statistics is the accurate one.
- **Search results are lost when you switch tabs.** Going to Statistics and back clears them;
  the search re-runs from scratch.
- **A failed search shows nothing.** If your embedding server is down, the magnifier stops
  spinning and no results and no error appear.

---

## See also

- [Model Management](models.md) — registering the instance that also serves embeddings
- [Playground](playground.md) — for testing the generation half in isolation
- [History](history.md) — where pipeline calls and their logprobs end up
- [Tokenizer](tokenizer.md) — real token counts, rather than characters divided by four

---

## Functional requirements

### Presuppositions

| # | Presupposition | Holds on a cold install? | Evidence |
|---|---|---|---|
| P1 | The seeded collection is searchable — it says Ready, 1 doc, 3 chunks | **Yes now.** The seeder embeds the sample, and retries shortly after startup when no server was reachable in time; when it still cannot, the document reads Pending with the reason rather than Ready. It used to write null embeddings and mark itself Completed, so vector search returned nothing on the only collection a new user has | `RagSampleEmbedder`, `RagSampleEmbeddingService`, `RagSeedEmbeddingTests` |
| P2 | Embeddings go to the server you are running | **Yes.** Reachable first, then the default, then the oldest. "Default, then oldest" tied on the two seeded instances and picked the offline vLLM, which is how a fresh install came up unembedded with `Connection refused (…:8000)` | `An_Offline_Instance_Is_Not_Preferred_Over_A_Reachable_One` |
| P3 | …and the URL is right | **Yes now.** An endpoint already ending in `/v1` is not given a second one. It was `/v1/v1/embeddings` before, so embeddings could never work against vLLM or LM Studio, which both publish their `/v1` | `The_Embedding_Url_Carries_Exactly_One_V1` |
| P4 | BM25 needs no embedding server | **True for both now.** Hybrid returns its keyword half and says it is not hybrid; it used to abort and throw that half away | `RagHybridFallbackTests` |
| P5 | The distance metric chosen for a collection is used | **Yes now.** Each ranks by its own operator, proved on vectors where all three disagree about the winner | `RagDistanceMetricTests` |
| P6 | The default new-collection settings will work here | **No.** They default to OpenAI's `text-embedding-3-small` at 1536 dims, and there is no OpenAI key path | `CreateCollectionDialog.tsx:12-13` |

### Requirements

| # | Requirement | Verified by | Status |
|---|---|---|---|
| R1 | A cold install lists the seeded collection with its document and chunk counts | open `/rag` | MET |
| R2 | BM25 returns a chunk with no embedding server running at all | detail → Search → BM25 → "transformer" | MET |
| R3 | A search that could not run shows an error naming the failure | browser check with a forced failure | MET |
| R4 | A search that ran and matched nothing shows an empty state, not an error | BM25 for a nonsense term | MET |
| R5 | With nothing configured, embedding goes to a registered instance rather than a hardcoded address | `Embeddings_Go_To_The_Registered_Instance_When_Unconfigured` | MET |
| R6 | Vector search returns a chunk on the seeded collection when an embedding server is available | `RagSeedEmbeddingTests`; browser-verified after a full delete and fresh install — vector 3 hits, BM25 2, hybrid 3 | MET |
| R7 | Hybrid returns its BM25 half when embedding is unavailable | `Hybrid_Falls_Back_To_Keyword_Results`, and it is labelled rather than passed off as hybrid (`The_Fallback_Says_It_Is_Not_Hybrid`); browser-verified with embedding forced to fail | MET |
| R8 | The embedding URL contains exactly one `/v1` when the endpoint already ends in `/v1` | `The_Embedding_Url_Carries_Exactly_One_V1` (4 cases, mutation-checked) | MET |
| R9 | A collection created with Euclidean ranks by Euclidean distance | `Euclidean_Ranks_By_Distance` — the three vectors are chosen so cosine, Euclidean and inner product each have a *different* winner, so the test cannot pass against always-cosine (mutation-checked) | MET |
| R10 | An unknown collection id says the collection is missing | the page reports it and offers the way back; 4xx responses are no longer retried, so it says so promptly | MET |
| R11 | A document whose ingest failed appears marked Failed without a manual refresh | the ingest mutation refreshes on settle rather than on success, so the row the server already wrote — Failed, with its reason — appears straight away | MET |
| R12 | Retrieval quality is measurable against ground truth | the Evaluate tab scores vector/BM25/hybrid over a labelled query set with precision@k, recall@k, MRR and nDCG@k; metrics proved by hand-worked examples (incl. both nDCG truncation directions and duplicate-id ties), invariants (recall non-decreasing in k, ideal ranking nDCG exactly 1, bounds), and a live end-to-end test against real pgvector (`RetrievalMetricsTests`, `RetrievalEvaluationTests`, mutation-checked); browser-verified | MET |
| R13 | Labelled query sets are validated against the collection | creating a set rejects empty items, unlabelled queries, and chunk ids from another collection (`Labels_Must_Belong_To_The_Collection`) | MET |
| R14 | A mode that cannot run reports why instead of scoring zero | vector/hybrid on an unembedded collection return "could not be evaluated" with the reason and no metrics; BM25 still evaluates (`Unembedded_Collection_Reports_Vector_Unavailable_Not_Zero`); browser-verified on the seeded collection | MET |
| R15 | Ground truth can be built without leaving the page | the New query set flow searches (BM25 ∪ vector, deduplicated, so labelling is not biased to one mode), ticks relevant chunks, and saves — with the vector half's absence stated when embeddings are missing | MET |

| R16 | The page answers from the retrieved context, not just retrieves | Answer with → Retrieve & answer; browser-verified end to end (mistral:7b-instruct, 3 chunks, answer shown above its sources) | MET |
| R17 | The answer step runs without naming a model explicitly | `An_Unnamed_Model_Falls_Back_To_The_Instance`; it used to send a blank model and surface Ollama's `model is required` as a 503 | MET |
| R18 | A query set sent with no items is a 400, not a 500 | `A_Query_Set_With_No_Items_Is_A_Validation_Error`, `An_Item_With_Null_Labels_Is_A_Validation_Error` | MET |
| R19 | The embedding model is present on a cold install | `models_test.sh` cases A/B/D — `ensure_ollama_model` used to return early if *any* model existed, so the embedding model was never pulled on a machine that already had a chat model | MET |
| R20 | A degraded search is never scored as though it ran | the retrieval evaluation reports a fallback as unavailable with the reason rather than putting a number against a method that never ran (`The_Evaluation_Will_Not_Score_A_Fallback_As_Hybrid`) | MET |
| R21 | Inner product ranks by magnitude as well as direction | `Inner_Product_Rewards_Magnitude` | MET |

### Withdrawn

| # | Requirement | Why withdrawn | Decided by |
|---|---|---|---|
| W1 | ~~This page generates an answer from the retrieved context~~ | **Reinstated and now MET.** It was withdrawn on the reasoning that the page is retrieval-only by design, with the note that the dangling hook should be "removed or documented as API-only, not left as an accident". Resolved the other way: the tab is called Search & RAG, the endpoint was fixed (it could not run without an explicit model), and Retrieve & answer now calls it. Recorded here rather than quietly deleted, because reversing an earlier decision is worth being able to see | reinstated when Prompt Lab and RAG were gone through end to end |
