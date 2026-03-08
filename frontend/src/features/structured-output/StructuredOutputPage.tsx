import { useState } from 'react'
import { Braces, Plus, Trash2, CheckCircle, XCircle } from 'lucide-react'
import { useSchemas, useCreateSchema, useDeleteSchema, useStructuredInference } from './api'
import type { JsonSchema, StructuredInferenceResult } from './types'

export function StructuredOutputPage() {
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const [selectedSchema, setSelectedSchema] = useState<JsonSchema | null>(null)

  const { data: schemas, isLoading } = useSchemas(undefined, search || undefined)
  const deleteSchema = useDeleteSchema()

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">Structured Output</h1>
          <p className="text-sm text-zinc-400 mt-1">
            JSON schema guided decoding and validation
          </p>
        </div>
        <button
          className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700"
          onClick={() => setShowCreate(true)}
        >
          <Plus className="h-4 w-4" />
          New Schema
        </button>
      </div>

      <div className="grid gap-6 lg:grid-cols-2">
        {/* Schema List */}
        <div className="space-y-4">
          <input
            className="w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
            placeholder="Search schemas..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />

          {isLoading && <p className="text-sm text-zinc-500">Loading...</p>}

          {schemas && schemas.length === 0 && (
            <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-8 text-center">
              <Braces className="mx-auto h-10 w-10 text-zinc-600 mb-2" />
              <p className="text-zinc-400">No schemas yet</p>
            </div>
          )}

          {schemas?.map((schema: JsonSchema) => (
            <div
              key={schema.id}
              className={`rounded border p-3 cursor-pointer transition-colors ${
                selectedSchema?.id === schema.id
                  ? 'border-violet-500 bg-zinc-800'
                  : 'border-zinc-700 bg-zinc-800/50 hover:border-zinc-600'
              }`}
              onClick={() => setSelectedSchema(schema)}
            >
              <div className="flex items-start justify-between">
                <div>
                  <h3 className="text-sm font-medium text-zinc-50">{schema.name}</h3>
                  {schema.description && (
                    <p className="text-xs text-zinc-500 mt-0.5">{schema.description}</p>
                  )}
                </div>
                <button
                  className="text-zinc-500 hover:text-red-400 p-1"
                  onClick={(e) => {
                    e.stopPropagation()
                    if (confirm('Delete this schema?')) deleteSchema.mutate(schema.id)
                  }}
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
              <span className="text-[10px] text-zinc-500 mt-1">v{schema.version}</span>
            </div>
          ))}
        </div>

        {/* Test Panel */}
        <div>
          {selectedSchema ? (
            <TestPanel schema={selectedSchema} />
          ) : (
            <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-12 text-center">
              <p className="text-zinc-500">Select a schema to test</p>
            </div>
          )}
        </div>
      </div>

      {showCreate && <CreateSchemaDialog onClose={() => setShowCreate(false)} />}
    </div>
  )
}

function TestPanel({ schema }: { schema: JsonSchema }) {
  const [prompt, setPrompt] = useState('')
  const [instanceId, setInstanceId] = useState('')
  const [model, setModel] = useState('')
  const [result, setResult] = useState<StructuredInferenceResult | null>(null)

  const infer = useStructuredInference(schema.id)

  const handleRun = () => {
    if (!prompt || !instanceId || !model) return
    infer.mutate(
      {
        instanceId,
        model,
        messages: [{ role: 'user', content: prompt }],
      },
      { onSuccess: (data) => setResult(data) }
    )
  }

  return (
    <div className="space-y-4">
      <div>
        <h3 className="text-sm font-medium text-zinc-50 mb-2">Schema: {schema.name}</h3>
        <pre className="rounded border border-zinc-700 bg-zinc-900 p-3 text-xs text-zinc-300 overflow-auto max-h-48">
          {JSON.stringify(JSON.parse(schema.schemaJson), null, 2)}
        </pre>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <input
          className="rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          placeholder="Instance ID"
          value={instanceId}
          onChange={(e) => setInstanceId(e.target.value)}
        />
        <input
          className="rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
          placeholder="Model name"
          value={model}
          onChange={(e) => setModel(e.target.value)}
        />
      </div>

      <textarea
        className="w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50 min-h-[100px]"
        placeholder="Enter prompt..."
        value={prompt}
        onChange={(e) => setPrompt(e.target.value)}
      />

      <button
        className="rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
        onClick={handleRun}
        disabled={infer.isPending || !prompt}
      >
        {infer.isPending ? 'Running...' : 'Run Structured Inference'}
      </button>

      {result && (
        <div className="space-y-3">
          <div className="flex items-center gap-2">
            {result.isValid ? (
              <CheckCircle className="h-5 w-5 text-green-400" />
            ) : (
              <XCircle className="h-5 w-5 text-red-400" />
            )}
            <span className={`text-sm font-medium ${result.isValid ? 'text-green-400' : 'text-red-400'}`}>
              {result.isValid ? 'Valid JSON' : 'Validation Failed'}
            </span>
            <span className="text-xs text-zinc-500 ml-auto">
              {result.latencyMs.toFixed(0)}ms &middot; {result.promptTokens + result.completionTokens} tokens
            </span>
          </div>

          {result.validationErrors.length > 0 && (
            <div className="rounded border border-red-800 bg-red-900/20 p-2">
              {result.validationErrors.map((err, i) => (
                <p key={i} className="text-xs text-red-400">{err}</p>
              ))}
            </div>
          )}

          <pre className="rounded border border-zinc-700 bg-zinc-900 p-3 text-xs text-zinc-300 overflow-auto max-h-64">
            {result.parsedJson
              ? JSON.stringify(result.parsedJson, null, 2)
              : result.rawOutput}
          </pre>
        </div>
      )}
    </div>
  )
}

function CreateSchemaDialog({ onClose }: { onClose: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [schemaJson, setSchemaJson] = useState(
    JSON.stringify(
      {
        type: 'object',
        properties: {
          name: { type: 'string' },
          age: { type: 'integer' },
        },
        required: ['name'],
      },
      null,
      2
    )
  )

  const createSchema = useCreateSchema()

  const handleSubmit = () => {
    try {
      JSON.parse(schemaJson) // validate JSON
      createSchema.mutate(
        { name, description, schemaJson },
        { onSuccess: onClose }
      )
    } catch {
      alert('Invalid JSON schema')
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-lg rounded-lg border border-zinc-700 bg-zinc-900 p-6">
        <h2 className="text-lg font-semibold text-zinc-50 mb-4">Create JSON Schema</h2>

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
            <label className="text-sm text-zinc-400">JSON Schema *</label>
            <textarea
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50 font-mono min-h-[200px]"
              value={schemaJson}
              onChange={(e) => setSchemaJson(e.target.value)}
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
            disabled={!name || createSchema.isPending}
          >
            Create
          </button>
        </div>
      </div>
    </div>
  )
}
