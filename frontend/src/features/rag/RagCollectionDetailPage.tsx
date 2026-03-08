import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, FileText } from 'lucide-react'
import { useState } from 'react'
import { useCollection, useDocuments, useCollectionStats } from './api'
import { DocumentUpload } from './components/DocumentUpload'
import { SearchPanel } from './components/SearchPanel'
import type { RagDocument } from './types'

type Tab = 'documents' | 'search' | 'stats'

export function RagCollectionDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [activeTab, setActiveTab] = useState<Tab>('documents')

  const { data: collection } = useCollection(id!)
  const { data: documents } = useDocuments(id!)
  const { data: stats } = useCollectionStats(id!)

  if (!collection) {
    return <p className="text-sm text-zinc-500">Loading collection...</p>
  }

  const tabs: { key: Tab; label: string }[] = [
    { key: 'documents', label: 'Documents' },
    { key: 'search', label: 'Search & RAG' },
    { key: 'stats', label: 'Statistics' },
  ]

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button
          className="text-zinc-400 hover:text-zinc-50"
          onClick={() => navigate('/rag')}
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">{collection.name}</h1>
          {collection.description && (
            <p className="text-sm text-zinc-400">{collection.description}</p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-4 text-sm text-zinc-400">
        <span>{collection.embeddingModel}</span>
        <span>{collection.dimensions}d</span>
        <span>{collection.chunkingStrategy} chunking</span>
        <span>{collection.documentCount} docs</span>
        <span>{collection.chunkCount} chunks</span>
      </div>

      <div className="flex gap-1 border-b border-zinc-700">
        {tabs.map((tab) => (
          <button
            key={tab.key}
            className={`px-4 py-2 text-sm ${
              activeTab === tab.key
                ? 'text-violet-400 border-b-2 border-violet-400'
                : 'text-zinc-500 hover:text-zinc-300'
            }`}
            onClick={() => setActiveTab(tab.key)}
          >
            {tab.label}
          </button>
        ))}
      </div>

      {activeTab === 'documents' && (
        <div className="space-y-4">
          <DocumentUpload collectionId={id!} />

          {documents && documents.length > 0 && (
            <div className="space-y-2">
              {documents.map((doc: RagDocument) => (
                <div
                  key={doc.id}
                  className="flex items-center justify-between rounded border border-zinc-700 bg-zinc-800/50 p-3"
                >
                  <div className="flex items-center gap-3">
                    <FileText className="h-4 w-4 text-zinc-500" />
                    <div>
                      <p className="text-sm font-medium text-zinc-50">{doc.filename}</p>
                      <p className="text-xs text-zinc-500">
                        {doc.chunkCount} chunks &middot; {(doc.sizeBytes / 1024).toFixed(1)} KB
                        &middot; {doc.characterCount.toLocaleString()} chars
                      </p>
                    </div>
                  </div>
                  <span
                    className={`text-xs px-2 py-0.5 rounded ${
                      doc.status === 'Completed'
                        ? 'bg-green-900/50 text-green-400'
                        : doc.status === 'Failed'
                          ? 'bg-red-900/50 text-red-400'
                          : 'bg-yellow-900/50 text-yellow-400'
                    }`}
                  >
                    {doc.status}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {activeTab === 'search' && <SearchPanel collectionId={id!} />}

      {activeTab === 'stats' && stats && (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
          <StatCard label="Documents" value={stats.documentCount} />
          <StatCard label="Chunks" value={stats.chunkCount} />
          <StatCard label="Total Characters" value={stats.totalCharacters.toLocaleString()} />
          <StatCard label="Avg Chunk Size" value={`${stats.averageChunkSize.toFixed(0)} chars`} />
          <StatCard label="Est. Tokens" value={stats.totalTokens.toLocaleString()} />
          {Object.entries(stats.documentsByStatus).map(([status, count]) => (
            <StatCard key={status} label={`Docs: ${status}`} value={count} />
          ))}
        </div>
      )}
    </div>
  )
}

function StatCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
      <p className="text-sm text-zinc-500">{label}</p>
      <p className="text-2xl font-bold text-zinc-50 mt-1">{value}</p>
    </div>
  )
}
