import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { BookOpen, Plus, Trash2, FileText, Database } from 'lucide-react'
import { useCollections, useDeleteCollection } from './api'
import { CreateCollectionDialog } from './components/CreateCollectionDialog'
import type { RagCollection } from './types'

export function RagWorkbenchPage() {
  const [showCreate, setShowCreate] = useState(false)
  const [search, setSearch] = useState('')
  const navigate = useNavigate()

  const { data: collections, isLoading } = useCollections(undefined, search || undefined)
  const deleteCollection = useDeleteCollection()

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">RAG Workbench</h1>
          <p className="text-sm text-zinc-400 mt-1">
            Build and test retrieval-augmented generation pipelines
          </p>
        </div>
        <button
          className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700"
          onClick={() => setShowCreate(true)}
        >
          <Plus className="h-4 w-4" />
          New Collection
        </button>
      </div>

      <input
        className="w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
        placeholder="Search collections..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      {isLoading && <p className="text-sm text-zinc-500">Loading collections...</p>}

      {collections && collections.length === 0 && (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-12 text-center">
          <BookOpen className="mx-auto h-12 w-12 text-zinc-600 mb-3" />
          <p className="text-zinc-400">No collections yet</p>
          <p className="text-sm text-zinc-500 mt-1">
            Create a collection to start building RAG pipelines
          </p>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {collections?.map((col: RagCollection) => (
          <div
            key={col.id}
            className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4 hover:border-violet-500/50 transition-colors cursor-pointer"
            onClick={() => navigate(`/rag/${col.id}`)}
          >
            <div className="flex items-start justify-between">
              <div>
                <h3 className="font-medium text-zinc-50">{col.name}</h3>
                {col.description && (
                  <p className="text-xs text-zinc-500 mt-1 line-clamp-2">{col.description}</p>
                )}
              </div>
              <button
                className="text-zinc-500 hover:text-red-400 p-1"
                onClick={(e) => {
                  e.stopPropagation()
                  if (confirm('Delete this collection?')) deleteCollection.mutate(col.id)
                }}
              >
                <Trash2 className="h-4 w-4" />
              </button>
            </div>

            <div className="mt-3 flex items-center gap-4 text-xs text-zinc-500">
              <span className="flex items-center gap-1">
                <FileText className="h-3 w-3" />
                {col.documentCount} docs
              </span>
              <span className="flex items-center gap-1">
                <Database className="h-3 w-3" />
                {col.chunkCount} chunks
              </span>
            </div>

            <div className="mt-2 flex flex-wrap gap-1">
              <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-700 text-zinc-400">
                {col.embeddingModel}
              </span>
              <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-700 text-zinc-400">
                {col.chunkingStrategy}
              </span>
              <span className="text-[10px] px-1.5 py-0.5 rounded bg-zinc-700 text-zinc-400">
                {col.dimensions}d
              </span>
            </div>
          </div>
        ))}
      </div>

      <CreateCollectionDialog open={showCreate} onClose={() => setShowCreate(false)} />
    </div>
  )
}
