import { useState } from 'react'
import { Search } from 'lucide-react'
import { describeMutationError } from '@/services/mutationErrors'
import { useQueryCollection } from '../api'
import type { ChunkSearchResult } from '../types'

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

  const queryCollection = useQueryCollection(collectionId)

  const handleSearch = () => {
    if (!queryText.trim()) return
    setResults(null)
    queryCollection.mutate(
      { queryText, topK, searchType },
      { onSuccess: (data) => setResults(data) }
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
        >
          <Search className="h-4 w-4" />
        </button>
      </div>

      {queryCollection.isPending && (
        <p className="text-sm text-zinc-500">Searching...</p>
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
