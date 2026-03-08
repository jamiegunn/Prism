import { useState } from 'react'
import { useCreateCollection } from '../api'

interface CreateCollectionDialogProps {
  open: boolean
  onClose: () => void
}

export function CreateCollectionDialog({ open, onClose }: CreateCollectionDialogProps) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [embeddingModel, setEmbeddingModel] = useState('text-embedding-3-small')
  const [dimensions, setDimensions] = useState(1536)
  const [chunkingStrategy, setChunkingStrategy] = useState('recursive')
  const [chunkSize, setChunkSize] = useState(512)
  const [chunkOverlap, setChunkOverlap] = useState(50)
  const [distanceMetric, setDistanceMetric] = useState('Cosine')

  const createCollection = useCreateCollection()

  if (!open) return null

  const handleSubmit = () => {
    createCollection.mutate(
      { name, description, embeddingModel, dimensions, chunkingStrategy, chunkSize, chunkOverlap, distanceMetric },
      {
        onSuccess: () => {
          onClose()
          setName('')
          setDescription('')
        },
      }
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-lg rounded-lg border border-zinc-700 bg-zinc-900 p-6">
        <h2 className="text-lg font-semibold text-zinc-50 mb-4">Create RAG Collection</h2>

        <div className="space-y-3">
          <div>
            <label className="text-sm text-zinc-400">Name *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My Documents"
            />
          </div>

          <div>
            <label className="text-sm text-zinc-400">Description</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-sm text-zinc-400">Embedding Model</label>
              <input
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={embeddingModel}
                onChange={(e) => setEmbeddingModel(e.target.value)}
              />
            </div>
            <div>
              <label className="text-sm text-zinc-400">Dimensions</label>
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={dimensions}
                onChange={(e) => setDimensions(Number(e.target.value))}
              />
            </div>
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="text-sm text-zinc-400">Chunking</label>
              <select
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={chunkingStrategy}
                onChange={(e) => setChunkingStrategy(e.target.value)}
              >
                <option value="recursive">Recursive</option>
                <option value="sentence">Sentence</option>
                <option value="fixed">Fixed</option>
              </select>
            </div>
            <div>
              <label className="text-sm text-zinc-400">Chunk Size</label>
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={chunkSize}
                onChange={(e) => setChunkSize(Number(e.target.value))}
              />
            </div>
            <div>
              <label className="text-sm text-zinc-400">Overlap</label>
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={chunkOverlap}
                onChange={(e) => setChunkOverlap(Number(e.target.value))}
              />
            </div>
          </div>

          <div>
            <label className="text-sm text-zinc-400">Distance Metric</label>
            <select
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              value={distanceMetric}
              onChange={(e) => setDistanceMetric(e.target.value)}
            >
              <option value="Cosine">Cosine</option>
              <option value="Euclidean">Euclidean</option>
              <option value="InnerProduct">Inner Product</option>
            </select>
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button
            className="rounded px-4 py-2 text-sm text-zinc-400 hover:text-zinc-50"
            onClick={onClose}
          >
            Cancel
          </button>
          <button
            className="rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
            onClick={handleSubmit}
            disabled={!name || !embeddingModel || createCollection.isPending}
          >
            {createCollection.isPending ? 'Creating...' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  )
}
