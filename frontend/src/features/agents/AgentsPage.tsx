import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Bot, Plus, Trash2, Play } from 'lucide-react'
import { useWorkflows, useCreateWorkflow, useDeleteWorkflow, useTools } from './api'
import type { AgentWorkflow, AgentTool } from './types'

export function AgentsPage() {
  const [search, setSearch] = useState('')
  const [showCreate, setShowCreate] = useState(false)
  const navigate = useNavigate()

  const { data: workflows, isLoading } = useWorkflows(search || undefined)
  const deleteWorkflow = useDeleteWorkflow()

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">Agent Builder</h1>
          <p className="text-sm text-zinc-400 mt-1">
            Create and run AI agents with tool use and reasoning
          </p>
        </div>
        <button
          className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700"
          onClick={() => setShowCreate(true)}
        >
          <Plus className="h-4 w-4" />
          New Workflow
        </button>
      </div>

      <input
        className="w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
        placeholder="Search workflows..."
        value={search}
        onChange={(e) => setSearch(e.target.value)}
      />

      {isLoading && <p className="text-sm text-zinc-500">Loading...</p>}

      {workflows && workflows.length === 0 && (
        <div className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-12 text-center">
          <Bot className="mx-auto h-10 w-10 text-zinc-600 mb-2" />
          <p className="text-zinc-400">No agent workflows yet</p>
          <p className="text-xs text-zinc-500 mt-1">Create one to get started</p>
        </div>
      )}

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
        {workflows?.map((workflow: AgentWorkflow) => (
          <div
            key={workflow.id}
            className="rounded-lg border border-zinc-700 bg-zinc-800/50 p-4 hover:border-zinc-600 cursor-pointer transition-colors"
            onClick={() => navigate(`/agents/${workflow.id}`)}
          >
            <div className="flex items-start justify-between">
              <div>
                <h3 className="text-sm font-medium text-zinc-50">{workflow.name}</h3>
                {workflow.description && (
                  <p className="text-xs text-zinc-500 mt-0.5 line-clamp-2">{workflow.description}</p>
                )}
              </div>
              <button
                className="text-zinc-500 hover:text-red-400 p-1"
                onClick={(e) => {
                  e.stopPropagation()
                  if (confirm('Delete this workflow?')) deleteWorkflow.mutate(workflow.id)
                }}
              >
                <Trash2 className="h-3.5 w-3.5" />
              </button>
            </div>
            <div className="mt-3 flex items-center gap-3 text-xs text-zinc-500">
              <span className="rounded bg-zinc-700 px-1.5 py-0.5">{workflow.pattern}</span>
              <span>{workflow.model}</span>
              <span>{workflow.runCount} runs</span>
            </div>
            <div className="mt-2 flex items-center gap-1 flex-wrap">
              {workflow.enabledTools.map((tool) => (
                <span key={tool} className="text-[10px] rounded bg-violet-900/30 text-violet-400 px-1.5 py-0.5">
                  {tool}
                </span>
              ))}
            </div>
          </div>
        ))}
      </div>

      {showCreate && <CreateWorkflowDialog onClose={() => setShowCreate(false)} />}
    </div>
  )
}

function CreateWorkflowDialog({ onClose }: { onClose: () => void }) {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [systemPrompt, setSystemPrompt] = useState('You are a helpful AI research assistant.')
  const [model, setModel] = useState('')
  const [instanceId, setInstanceId] = useState('')
  const [pattern, setPattern] = useState('ReAct')
  const [maxSteps, setMaxSteps] = useState(10)
  const [tokenBudget, setTokenBudget] = useState(8000)
  const [temperature, setTemperature] = useState(0.7)
  const [selectedTools, setSelectedTools] = useState<string[]>([])

  const { data: tools } = useTools()
  const createWorkflow = useCreateWorkflow()

  const handleSubmit = () => {
    createWorkflow.mutate(
      {
        name,
        description: description || undefined,
        systemPrompt,
        model,
        instanceId,
        pattern,
        maxSteps,
        tokenBudget,
        temperature,
        enabledTools: selectedTools,
      },
      { onSuccess: onClose }
    )
  }

  const toggleTool = (toolName: string) => {
    setSelectedTools((prev) =>
      prev.includes(toolName) ? prev.filter((t) => t !== toolName) : [...prev, toolName]
    )
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-lg rounded-lg border border-zinc-700 bg-zinc-900 p-6 max-h-[90vh] overflow-y-auto">
        <h2 className="text-lg font-semibold text-zinc-50 mb-4">Create Agent Workflow</h2>

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
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="text-sm text-zinc-400">Instance ID *</label>
              <input
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                placeholder="Instance GUID"
                value={instanceId}
                onChange={(e) => setInstanceId(e.target.value)}
              />
            </div>
            <div>
              <label className="text-sm text-zinc-400">Model *</label>
              <input
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                placeholder="Model name"
                value={model}
                onChange={(e) => setModel(e.target.value)}
              />
            </div>
          </div>
          <div>
            <label className="text-sm text-zinc-400">System Prompt</label>
            <textarea
              className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50 min-h-[80px]"
              value={systemPrompt}
              onChange={(e) => setSystemPrompt(e.target.value)}
            />
          </div>
          <div className="grid grid-cols-3 gap-2">
            <div>
              <label className="text-sm text-zinc-400">Pattern</label>
              <select
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={pattern}
                onChange={(e) => setPattern(e.target.value)}
              >
                <option value="ReAct">ReAct</option>
                <option value="Sequential">Sequential</option>
              </select>
            </div>
            <div>
              <label className="text-sm text-zinc-400">Max Steps</label>
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={maxSteps}
                onChange={(e) => setMaxSteps(Number(e.target.value))}
              />
            </div>
            <div>
              <label className="text-sm text-zinc-400">Token Budget</label>
              <input
                type="number"
                className="mt-1 w-full rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50"
                value={tokenBudget}
                onChange={(e) => setTokenBudget(Number(e.target.value))}
              />
            </div>
          </div>

          {tools && tools.length > 0 && (
            <div>
              <label className="text-sm text-zinc-400">Tools</label>
              <div className="mt-1 space-y-1">
                {tools.map((tool: AgentTool) => (
                  <label
                    key={tool.name}
                    className="flex items-center gap-2 rounded border border-zinc-700 bg-zinc-800 px-3 py-2 cursor-pointer hover:border-zinc-600"
                  >
                    <input
                      type="checkbox"
                      checked={selectedTools.includes(tool.name)}
                      onChange={() => toggleTool(tool.name)}
                      className="rounded"
                    />
                    <div>
                      <span className="text-sm text-zinc-50">{tool.name}</span>
                      <p className="text-xs text-zinc-500">{tool.description}</p>
                    </div>
                  </label>
                ))}
              </div>
            </div>
          )}
        </div>

        <div className="mt-6 flex justify-end gap-2">
          <button className="rounded px-4 py-2 text-sm text-zinc-400 hover:text-zinc-50" onClick={onClose}>
            Cancel
          </button>
          <button
            className="rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
            onClick={handleSubmit}
            disabled={!name || !model || !instanceId || createWorkflow.isPending}
          >
            Create
          </button>
        </div>
      </div>
    </div>
  )
}
