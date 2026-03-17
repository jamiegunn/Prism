import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Download, ExternalLink, Save } from 'lucide-react'
import { useState, useEffect } from 'react'
import { useNotebook, useUpdateNotebook } from './api'

export function NotebookDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [, setJupyterReady] = useState(false)
  const [showJsonEditor, setShowJsonEditor] = useState(false)
  const [editContent, setEditContent] = useState('')
  const [contentInitialized, setContentInitialized] = useState(false)

  const { data: notebook } = useNotebook(id!)
  const updateNotebook = useUpdateNotebook(id!)

  if (notebook && !contentInitialized) {
    setEditContent(notebook.content)
    setContentInitialized(true)
  }

  // Listen for JupyterLite ready messages
  useEffect(() => {
    const handler = (event: MessageEvent) => {
      if (event.data?.type === 'jupyterlite-ready') {
        setJupyterReady(true)
      }
    }
    window.addEventListener('message', handler)
    return () => window.removeEventListener('message', handler)
  }, [])

  const handleSave = () => {
    if (!editContent) return
    updateNotebook.mutate(
      { content: editContent },
      { onSuccess: () => setShowJsonEditor(false) }
    )
  }

  const handleDownload = () => {
    if (!notebook) return
    const link = document.createElement('a')
    link.href = `/api/v1/notebooks/${notebook.id}/download`
    link.download = `${notebook.name.replace(/\s+/g, '_')}.ipynb`
    link.click()
  }

  const handleOpenJupyterLite = () => {
    // Open JupyterLite in a new tab — notebooks content is loaded from the API
    window.open('/jupyterlite/lab/index.html', '_blank')
  }

  if (!notebook) {
    return <p className="text-sm text-zinc-500">Loading notebook...</p>
  }

  // Parse the notebook content to display cells
  let cells: { cell_type: string; source: string[]; outputs?: unknown[]; execution_count?: number | null }[] = []
  try {
    const parsed = JSON.parse(notebook.content)
    cells = parsed.cells ?? []
  } catch {
    // Invalid JSON
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button
            className="text-zinc-400 hover:text-zinc-50"
            onClick={() => navigate('/notebooks')}
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <h1 className="text-2xl font-bold text-zinc-50">{notebook.name}</h1>
            {notebook.description && (
              <p className="text-sm text-zinc-400">{notebook.description}</p>
            )}
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            className="flex items-center gap-1.5 rounded border border-zinc-700 px-3 py-1.5 text-sm text-zinc-400 hover:text-zinc-50 hover:border-zinc-600"
            onClick={() => setShowJsonEditor(!showJsonEditor)}
          >
            {showJsonEditor ? 'View Cells' : 'Edit JSON'}
          </button>
          <button
            className="flex items-center gap-1.5 rounded border border-zinc-700 px-3 py-1.5 text-sm text-zinc-400 hover:text-zinc-50 hover:border-zinc-600"
            onClick={handleDownload}
          >
            <Download className="h-3.5 w-3.5" />
            Download
          </button>
          <button
            className="flex items-center gap-1.5 rounded bg-violet-600 px-3 py-1.5 text-sm text-white hover:bg-violet-700"
            onClick={handleOpenJupyterLite}
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Open in JupyterLite
          </button>
        </div>
      </div>

      <div className="flex items-center gap-4 text-sm text-zinc-500">
        <span>v{notebook.version}</span>
        <span>{(notebook.sizeBytes / 1024).toFixed(1)} KB</span>
        <span>{notebook.kernelName} kernel</span>
        {notebook.lastEditedAt && (
          <span>Last edited {new Date(notebook.lastEditedAt).toLocaleString()}</span>
        )}
      </div>

      {showJsonEditor ? (
        <div className="space-y-3">
          <textarea
            className="w-full rounded border border-zinc-700 bg-zinc-900 px-3 py-2 text-xs text-zinc-300 font-mono min-h-[500px]"
            value={editContent}
            onChange={(e) => setEditContent(e.target.value)}
          />
          <div className="flex gap-2">
            <button
              className="flex items-center gap-1.5 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
              onClick={handleSave}
              disabled={updateNotebook.isPending}
            >
              <Save className="h-3.5 w-3.5" />
              {updateNotebook.isPending ? 'Saving...' : 'Save'}
            </button>
            <button
              className="rounded px-4 py-2 text-sm text-zinc-400 hover:text-zinc-50"
              onClick={() => {
                setEditContent(notebook.content)
                setShowJsonEditor(false)
              }}
            >
              Cancel
            </button>
          </div>
        </div>
      ) : (
        <div className="space-y-3">
          {cells.length === 0 && (
            <p className="text-sm text-zinc-500">No cells in this notebook.</p>
          )}
          {cells.map((cell, i) => (
            <div
              key={i}
              className="rounded border border-zinc-700 bg-zinc-800/50 overflow-hidden"
            >
              <div className="flex items-center gap-2 px-3 py-1.5 bg-zinc-800 border-b border-zinc-700">
                <span
                  className={`text-[10px] uppercase font-medium ${
                    cell.cell_type === 'code'
                      ? 'text-blue-400'
                      : cell.cell_type === 'markdown'
                        ? 'text-green-400'
                        : 'text-zinc-500'
                  }`}
                >
                  {cell.cell_type}
                </span>
                {cell.cell_type === 'code' && cell.execution_count != null && (
                  <span className="text-[10px] text-zinc-500">
                    [{cell.execution_count}]
                  </span>
                )}
              </div>
              <pre className="px-3 py-2 text-xs text-zinc-300 font-mono whitespace-pre-wrap overflow-auto max-h-64">
                {Array.isArray(cell.source) ? cell.source.join('') : String(cell.source)}
              </pre>
              {cell.outputs && cell.outputs.length > 0 && (
                <div className="border-t border-zinc-700 px-3 py-2 bg-zinc-900/50">
                  <span className="text-[10px] uppercase text-zinc-500 font-medium">Output</span>
                  <pre className="text-xs text-zinc-400 mt-1 whitespace-pre-wrap">
                    {cell.outputs
                      .map((o: unknown) => {
                        const output = o as Record<string, unknown>
                        if (output.text) return (output.text as string[]).join('')
                        if (output.data) {
                          const data = output.data as Record<string, unknown>
                          return (data['text/plain'] as string[])?.join('') ?? JSON.stringify(output)
                        }
                        return JSON.stringify(output)
                      })
                      .join('\n')}
                  </pre>
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {/* JupyterLite iframe embed — hidden by default, shown when JupyterLite assets are available */}
      <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4">
        <h3 className="text-sm font-medium text-zinc-50 mb-2">JupyterLite Integration</h3>
        <p className="text-xs text-zinc-400 mb-3">
          Click "Open in JupyterLite" to launch a full Python notebook environment in your browser.
          The <code className="px-1 py-0.5 rounded bg-zinc-700 text-violet-400">workbench</code> module
          is available for interacting with the Prism API from Python.
        </p>
        <div className="rounded bg-zinc-900 border border-zinc-700 p-3">
          <pre className="text-xs text-zinc-300 font-mono">
{`import workbench

# Chat with a model
response = await workbench.chat("instance-id", "model-name", "Hello!")

# Search RAG collections
results = await workbench.rag_query("collection-id", "search query")

# Get dataset records
records = await workbench.get_dataset_records("dataset-id")

# Type workbench.help() for all available functions`}
          </pre>
        </div>
      </div>
    </div>
  )
}
