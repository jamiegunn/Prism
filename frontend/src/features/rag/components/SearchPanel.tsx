import { useState } from 'react'
import { Search, Sparkles } from 'lucide-react'
import { describeMutationError } from '@/services/mutationErrors'
import { useInstances } from '@/features/models/api'
import { useDefaultInstance } from '@/features/models/useDefaultInstance'
import { useQueryCollection, useRagPipeline } from '../api'
import type { ChunkSearchResult, RagPipelineResult } from '../types'

interface SearchPanelProps {
  collectionId: string
}

export function SearchPanel({ collectionId }: SearchPanelProps) {
  const [queryText, setQueryText] = useState('')
  const [searchType, setSearchType] = useState('vector')
  const [topK, setTopK] = useState(5)
  // Null means "nothing has been asked yet", which is a different screen from a search that
  // ran and matched nothing. Collapsing the two is what made a failed embedding call
  // indistinguishable from an empty corpus.
  const [results, setResults] = useState<ChunkSearchResult[] | null>(null)

  // Set when a search ran a different method from the one asked for — hybrid falling back to its
  // keyword half. The results are real; the label on them would otherwise be wrong.
  const [degraded, setDegraded] = useState<string | null>(null)

  // The other half of "Search & RAG". The endpoint, the client hook and the result type all
  // existed; nothing called them, so the tab retrieved chunks and stopped — the R in RAG with
  // no G, on the page named after it.
  const [answer, setAnswer] = useState<RagPipelineResult | null>(null)
  const [instanceId, setInstanceId] = useState<string | null>(null)

  const { data: instances } = useInstances()
  useDefaultInstance(instanceId, setInstanceId)

  const queryCollection = useQueryCollection(collectionId)
  const ragPipeline = useRagPipeline(collectionId)

  const handleSearch = () => {
    if (!queryText.trim()) return
    setResults(null)
    setAnswer(null)
    setDegraded(null)
    queryCollection.mutate(
      { queryText, topK, searchType },
      {
        onSuccess: (data) => {
          setResults(data.results)
          setDegraded(data.degradedReason)
        },
      }
    )
  }

  const handleAnswer = () => {
    if (!queryText.trim() || !instanceId) return
    setAnswer(null)
    setDegraded(null)
    ragPipeline.mutate(
      // No model named: the instance's own is used. Sending a blank one used to reach the
      // inference server as `model is required` and surface as a 503.
      { query: queryText, model: '', instanceId, topK, searchType },
      {
        onSuccess: (data) => {
          setAnswer(data)
          setResults(data.retrievedChunks)
        },
      }
    )
  }

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <input
          className="flex-1 rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          placeholder="Enter search query..."
          value={queryText}
          onChange={(e) => setQueryText(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
        />
        <select
          className="rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          value={searchType}
          onChange={(e) => setSearchType(e.target.value)}
        >
          <option value="vector">Vector</option>
          <option value="bm25">BM25</option>
          <option value="hybrid">Hybrid</option>
        </select>
        <input
          type="number"
          className="w-16 rounded border border-zinc-700 bg-zinc-800 px-2 py-2 text-sm text-zinc-50"
          value={topK}
          onChange={(e) => setTopK(Number(e.target.value))}
          min={1}
          max={50}
        />
        <button
          className="rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
          onClick={handleSearch}
          disabled={queryCollection.isPending || !queryText.trim()}
          title="Retrieve matching chunks"
        >
          <Search className="h-4 w-4" />
        </button>
      </div>

      <div className="flex items-center gap-2">
        <span className="text-xs text-zinc-500">Answer with</span>
        <select
          className="rounded border border-zinc-700 bg-zinc-800 px-2 py-1 text-xs text-zinc-50"
          value={instanceId ?? ''}
          onChange={(e) => setInstanceId(e.target.value || null)}
        >
          <option value="">Select a server...</option>
          {instances?.map((instance) => (
            <option key={instance.id} value={instance.id}>
              {instance.name}
              {instance.modelId ? ` (${instance.modelId})` : ''}
            </option>
          ))}
        </select>
        <button
          className="flex items-center gap-1.5 rounded border border-violet-700 px-3 py-1 text-xs text-violet-300 hover:bg-violet-950/40 disabled:opacity-50"
          onClick={handleAnswer}
          disabled={ragPipeline.isPending || !queryText.trim() || !instanceId}
        >
          <Sparkles className="h-3.5 w-3.5" />
          {ragPipeline.isPending ? 'Generating...' : 'Retrieve & answer'}
        </button>
      </div>

      {queryCollection.isPending && (
        <p className="text-sm text-zinc-500">Searching...</p>
      )}

      {degraded && !queryCollection.isPending && (
        <div className="rounded border border-amber-900/60 bg-amber-950/30 p-3 text-sm">
          <p className="font-medium text-amber-300">These are not the results you asked for.</p>
          <p className="mt-1 text-amber-200/80">{degraded}</p>
        </div>
      )}

      {ragPipeline.isError && !ragPipeline.isPending && (
        <div className="rounded border border-red-900/60 bg-red-950/40 p-3 text-sm">
          <p className="font-medium text-red-300">The answer could not be generated.</p>
          <p className="mt-1 text-red-200/80">{describeMutationError(ragPipeline.error)}</p>
        </div>
      )}

      {answer && (
        <div className="rounded border border-violet-900/60 bg-violet-950/20 p-3">
          <div className="mb-2 flex items-center justify-between">
            <span className="text-xs font-medium text-violet-300">Answer</span>
            <span className="text-xs text-zinc-500">
              {answer.model} &middot; {answer.promptTokens + answer.completionTokens} tokens
              &middot; {Math.round(answer.latencyMs)}ms
            </span>
          </div>
          <p className="whitespace-pre-wrap text-sm text-zinc-200">{answer.generatedResponse}</p>
          <p className="mt-2 text-xs text-zinc-500">
            Grounded in the {answer.retrievedChunks.length} chunk
            {answer.retrievedChunks.length === 1 ? '' : 's'} below.
          </p>
        </div>
      )}

      {queryCollection.isError && !queryCollection.isPending && (
        <div className="rounded border border-red-900/60 bg-red-950/40 p-3 text-sm">
          <p className="font-medium text-red-300">The search did not run.</p>
          <p className="mt-1 text-red-200/80">{describeMutationError(queryCollection.error)}</p>
          <p className="mt-2 text-xs text-red-200/60">
            Vector and hybrid search need an embedding server to embed the query. BM25 does not
            &mdash; it is computed by the database at ingest, so it still works when embedding is
            unavailable.
          </p>
        </div>
      )}

      {results !== null && results.length === 0 && !queryCollection.isPending && (
        <div className="rounded border border-zinc-800 bg-zinc-900/40 p-3 text-sm text-zinc-400">
          <p>No chunks matched.</p>
          <p className="mt-1 text-xs text-zinc-500">
            The search ran and returned nothing. On a vector or hybrid search this also happens
            when the collection was ingested without embeddings &mdash; try BM25 to tell the two
            apart.
          </p>
        </div>
      )}

      {results !== null && results.length > 0 && (
        <div className="space-y-2">
          <p className="text-xs text-zinc-500">{results.length} results</p>
          {results.map((chunk) => (
            <div
              key={chunk.chunkId}
              className="rounded border border-zinc-700 bg-zinc-800/50 p-3"
            >
              <div className="flex items-center justify-between mb-1">
                <span className="text-xs font-medium text-violet-400">
                  {chunk.documentFilename}
                </span>
                <span className="text-xs text-zinc-500">
                  Score: {chunk.score.toFixed(4)}
                </span>
              </div>
              <p className="text-sm text-zinc-300 whitespace-pre-wrap line-clamp-4">
                {chunk.content}
              </p>
              <div className="mt-1 flex gap-2 text-xs text-zinc-500">
                <span>{chunk.tokenCount} tokens</span>
                <span>Chunk #{chunk.orderIndex}</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
