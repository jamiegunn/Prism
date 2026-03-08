import { useState } from 'react'
import { Search } from 'lucide-react'
import { useQueryCollection } from '../api'
import type { ChunkSearchResult } from '../types'

interface SearchPanelProps {
  collectionId: string
}

export function SearchPanel({ collectionId }: SearchPanelProps) {
  const [queryText, setQueryText] = useState('')
  const [searchType, setSearchType] = useState('vector')
  const [topK, setTopK] = useState(5)
  const [results, setResults] = useState<ChunkSearchResult[]>([])

  const queryCollection = useQueryCollection(collectionId)

  const handleSearch = () => {
    if (!queryText.trim()) return
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

      {results.length > 0 && (
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
