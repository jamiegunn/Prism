import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { NotebookPen, Plus, Trash2, Download, Clock } from 'lucide-react'
import { useNotebooks, useCreateNotebook, useDeleteNotebook } from './api'
import type { NotebookSummary } from './types'

export function NotebooksPage() {
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const navigate = useNavigate()

  const { data: notebooks, isLoading } = useNotebooks(search || undefined)
  const deleteNotebook = useDeleteNotebook()

  const handleDownload = (id: string, name: string) => {
    const link = document.createElement('a')
    link.href = `/api/v1/notebooks/${id}/download`
    link.download = `${name.replace(/\s+/g, '_')}.ipynb`
    link.click()
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">Notebooks</h1>
          <p className="text-sm text-zinc-400 mt-1">
            Research notebooks with JupyterLite — Python in the browser
          </p>
        </div>
        <button
          className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700"
          onClick={() => setShowCreate(true)}
        >
          <Plus className="h-4 w-4" />
          New Notebook
        </button>
      </div>

      <input
        className="w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
        placeholder="Search notebooks..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      {isLoading && <p className="text-sm text-zinc-500">Loading...</p>}

      {notebooks && notebooks.length === 0 && (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-12 text-center">
          <NotebookPen className="mx-auto h-10 w-10 text-zinc-600 mb-2" />
          <p className="text-zinc-400">No notebooks yet</p>
          <p className="text-xs text-zinc-500 mt-1">
            Create a notebook to start researching with Python
          </p>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {notebooks?.map((nb: NotebookSummary) => (
          <div
            key={nb.id}
            className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4 hover:border-zinc-600 cursor-pointer transition-colors"
            onClick={() => navigate(`/notebooks/${nb.id}`)}
          >
            <div className="flex items-start justify-between">
              <div className="flex items-center gap-2">
                <NotebookPen className="h-4 w-4 text-violet-400" />
                <h3 className="text-sm font-medium text-zinc-50">{nb.name}</h3>
              </div>
              <div className="flex items-center gap-1">
                <button
                  className="text-zinc-500 hover:text-zinc-300 p-1"
                  onClick={(e) => {
                    e.stopPropagation()
                    handleDownload(nb.id, nb.name)
                  }}
                  title="Download .ipynb"
                >
                  <Download className="h-3.5 w-3.5" />
                </button>
                <button
                  className="text-zinc-500 hover:text-red-400 p-1"
                  onClick={(e) => {
                    e.stopPropagation()
                    if (confirm('Delete this notebook?')) deleteNotebook.mutate(nb.id)
                  }}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
            {nb.description && (
              <p className="text-xs text-zinc-500 mt-1 line-clamp-2">{nb.description}</p>
            )}
            <div className="mt-3 flex items-center gap-3 text-xs text-zinc-500">
              <span>v{nb.version}</span>
              <span>{(nb.sizeBytes / 1024).toFixed(1)} KB</span>
              <span className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                {nb.lastEditedAt
                  ? new Date(nb.lastEditedAt).toLocaleDateString()
                  : new Date(nb.createdAt).toLocaleDateString()}
              </span>
            </div>
          </div>
        ))}
      </div>

      {showCreate && <CreateNotebookDialog onClose={() => setShowCreate(false)} />}
    </div>
  )
}

function CreateNotebookDialog({ onClose }: { onClose: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const navigate = useNavigate()

  const createNotebook = useCreateNotebook()

  const handleSubmit = () => {
    createNotebook.mutate(
      { name, description: description || undefined },
      {
        onSuccess: (nb) => {
          onClose()
          navigate(`/notebooks/${nb.id}`)
        },
      }
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-md rounded-lg border border-zinc-700 bg-zinc-900 p-6">
        <h2 className="text-lg font-semibold text-zinc-50 mb-4">New Notebook</h2>

        <div className="space-y-3">
          <div>
            <label className="text-sm text-zinc-400">Name *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              placeholder="My Research Notebook"
              value={name}
              onChange={(e) => setName(e.target.value)}
              autoFocus
            />
          </div>
          <div>
            <label className="text-sm text-zinc-400">Description</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              placeholder="Optional description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
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
            disabled={!name || createNotebook.isPending}
          >
            Create
          </button>
        </div>
      </div>
    </div>
  )
}
