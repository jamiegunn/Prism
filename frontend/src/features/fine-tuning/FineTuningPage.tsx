import { useState } from 'react'
import { Plus, Trash2, Download, Wrench } from 'lucide-react'
import { useAdapters, useCreateAdapter, useDeleteAdapter, useExportFineTune } from './api'
import type { LoraAdapter, ExportFineTuneResult } from './types'

type Tab = 'adapters' | 'export'

export function FineTuningPage() {
  const [activeTab, setActiveTab] = useState<Tab>('adapters')
  const [showCreateAdapter, setShowCreateAdapter] = useState(false)

  const tabs: { key: Tab; label: string }[] = [
    { key: 'adapters', label: 'LoRA Adapters' },
    { key: 'export', label: 'Export Dataset' },
  ]

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold text-zinc-50">Fine-Tuning</h1>
        <p className="text-sm text-zinc-400 mt-1">
          LoRA adapter management and dataset export for fine-tuning
        </p>
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

      {activeTab === 'adapters' && (
        <AdaptersPanel onShowCreate={() => setShowCreateAdapter(true)} />
      )}

      {activeTab === 'export' && <ExportPanel />}

      {showCreateAdapter && (
        <CreateAdapterDialog onClose={() => setShowCreateAdapter(false)} />
      )}
    </div>
  )
}

function AdaptersPanel({ onShowCreate }: { onShowCreate: () => void }) {
  const { data: adapters, isLoading } = useAdapters()
  const deleteAdapter = useDeleteAdapter()

  return (
    <div className="space-y-4">
      <div className="flex justify-end">
        <button
          className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700"
          onClick={onShowCreate}
        >
          <Plus className="h-4 w-4" />
          Register Adapter
        </button>
      </div>

      {isLoading && <p className="text-sm text-zinc-500">Loading...</p>}

      {adapters && adapters.length === 0 && (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-8 text-center">
          <Wrench className="mx-auto h-10 w-10 text-zinc-600 mb-2" />
          <p className="text-zinc-400">No LoRA adapters registered</p>
        </div>
      )}

      {adapters?.map((adapter: LoraAdapter) => (
        <div
          key={adapter.id}
          className="rounded border border-zinc-700 bg-zinc-800/50 p-3"
        >
          <div className="flex items-start justify-between">
            <div>
              <h3 className="text-sm font-medium text-zinc-50">{adapter.name}</h3>
              {adapter.description && (
                <p className="text-xs text-zinc-500 mt-0.5">{adapter.description}</p>
              )}
            </div>
            <button
              className="text-zinc-500 hover:text-red-400 p-1"
              onClick={() => {
                if (confirm('Delete this adapter?')) deleteAdapter.mutate(adapter.id)
              }}
            >
              <Trash2 className="h-3.5 w-3.5" />
            </button>
          </div>
          <div className="mt-2 flex items-center gap-3 text-xs text-zinc-500">
            <span>Base: {adapter.baseModel}</span>
            <span className="font-mono text-[10px]">{adapter.adapterPath}</span>
            <span className={adapter.isActive ? 'text-green-400' : 'text-zinc-500'}>
              {adapter.isActive ? 'Active' : 'Inactive'}
            </span>
          </div>
        </div>
      ))}
    </div>
  )
}

function ExportPanel() {
  const [datasetId, setDatasetId] = useState('')
  const [format, setFormat] = useState('Alpaca')
  const [instructionCol, setInstructionCol] = useState('instruction')
  const [inputCol, setInputCol] = useState('input')
  const [outputCol, setOutputCol] = useState('output')
  const [result, setResult] = useState<ExportFineTuneResult | null>(null)

  const exportFn = useExportFineTune()

  const handleExport = () => {
    if (!datasetId) return
    exportFn.mutate(
      {
        datasetId,
        format,
        instructionColumn: instructionCol,
        inputColumn: inputCol,
        outputColumn: outputCol,
      },
      {
        onSuccess: (data) => setResult(data),
      }
    )
  }

  const handleDownload = () => {
    if (!result) return
    const blob = new Blob([result.content], { type: result.contentType })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = result.filename
    a.click()
    URL.revokeObjectURL(url)
  }

  return (
    <div className="space-y-4 max-w-xl">
      <div>
        <label className="text-sm text-zinc-400">Dataset ID *</label>
        <input
          className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          placeholder="Dataset GUID"
          value={datasetId}
          onChange={(e) => setDatasetId(e.target.value)}
        />
      </div>

      <div>
        <label className="text-sm text-zinc-400">Export Format</label>
        <select
          className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          value={format}
          onChange={(e) => setFormat(e.target.value)}
        >
          <option value="Alpaca">Alpaca (instruction/input/output)</option>
          <option value="ShareGpt">ShareGPT (conversations)</option>
          <option value="ChatMl">ChatML (role tokens)</option>
          <option value="OpenAiJsonl">OpenAI JSONL (messages)</option>
        </select>
      </div>

      <div className="grid grid-cols-3 gap-2">
        <div>
          <label className="text-sm text-zinc-400">Instruction Column</label>
          <input
            className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
            value={instructionCol}
            onChange={(e) => setInstructionCol(e.target.value)}
          />
        </div>
        <div>
          <label className="text-sm text-zinc-400">Input Column</label>
          <input
            className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
            value={inputCol}
            onChange={(e) => setInputCol(e.target.value)}
          />
        </div>
        <div>
          <label className="text-sm text-zinc-400">Output Column</label>
          <input
            className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
            value={outputCol}
            onChange={(e) => setOutputCol(e.target.value)}
          />
        </div>
      </div>

      <button
        className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
        onClick={handleExport}
        disabled={!datasetId || exportFn.isPending}
      >
        <Download className="h-4 w-4" />
        {exportFn.isPending ? 'Exporting...' : 'Export'}
      </button>

      {result && (
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <div className="text-sm text-zinc-50">
              Exported {result.recordCount} records as{' '}
              <span className="font-mono text-violet-400">{result.filename}</span>
            </div>
            <button
              className="text-sm text-violet-400 hover:text-violet-300"
              onClick={handleDownload}
            >
              Download
            </button>
          </div>

          {result.warnings.length > 0 && (
            <div className="rounded border border-yellow-800 bg-yellow-900/20 p-2">
              {result.warnings.map((w, i) => (
                <p key={i} className="text-xs text-yellow-400">{w}</p>
              ))}
            </div>
          )}

          <pre className="rounded border border-zinc-700 bg-zinc-900 p-3 text-xs text-zinc-300 overflow-auto max-h-64">
            {result.content.slice(0, 3000)}
            {result.content.length > 3000 && '\n... [truncated]'}
          </pre>
        </div>
      )}
    </div>
  )
}

function CreateAdapterDialog({ onClose }: { onClose: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [instanceId, setInstanceId] = useState('')
  const [adapterPath, setAdapterPath] = useState('')
  const [baseModel, setBaseModel] = useState('')

  const createAdapter = useCreateAdapter()

  const handleSubmit = () => {
    createAdapter.mutate(
      {
        name,
        description: description || undefined,
        instanceId,
        adapterPath,
        baseModel,
      },
      { onSuccess: onClose }
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-lg rounded-lg border border-zinc-700 bg-zinc-900 p-6">
        <h2 className="text-lg font-semibold text-zinc-50 mb-4">Register LoRA Adapter</h2>

        <div className="space-y-3">
          <div>
            <label className="text-sm text-zinc-400">Name *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              value={name}
              onChange={(e) => setName(e.target.value)}
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
          <div>
            <label className="text-sm text-zinc-400">Instance ID *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              placeholder="vLLM instance GUID"
              value={instanceId}
              onChange={(e) => setInstanceId(e.target.value)}
            />
          </div>
          <div>
            <label className="text-sm text-zinc-400">Adapter Path *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50 font-mono"
              placeholder="/path/to/lora/adapter"
              value={adapterPath}
              onChange={(e) => setAdapterPath(e.target.value)}
            />
          </div>
          <div>
            <label className="text-sm text-zinc-400">Base Model *</label>
            <input
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
              placeholder="e.g. meta-llama/Llama-3-8B"
              value={baseModel}
              onChange={(e) => setBaseModel(e.target.value)}
            />
          </div>
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button className="rounded px-4 py-2 text-sm text-zinc-400 hover:text-zinc-50" onClick={onClose}>
            Cancel
          </button>
          <button
            className="rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
            onClick={handleSubmit}
            disabled={!name || !instanceId || !adapterPath || !baseModel || createAdapter.isPending}
          >
            Register
          </button>
        </div>
      </div>
    </div>
  )
}
