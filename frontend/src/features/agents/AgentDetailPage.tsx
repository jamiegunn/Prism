import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Play, Clock } from 'lucide-react'
import { useState } from 'react'
import { useWorkflow, useRuns } from './api'
import type { AgentRun, AgentStep } from './types'

import { AgentTraceView } from './components/AgentTraceView'

type Tab = 'run' | 'history' | 'trace'

export function AgentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const [activeTab, setActiveTab] = useState<Tab>('run')
  const [input, setInput] = useState('')
  const [liveSteps, setLiveSteps] = useState<AgentStep[]>([])
  const [isRunning, setIsRunning] = useState(false)
  const [runResult, setRunResult] = useState<AgentRun | null>(null)

  const { data: workflow } = useWorkflow(id!)
  const { data: runs } = useRuns(id!)

  const handleRun = async () => {
    if (!input || isRunning) return
    setIsRunning(true)
    setLiveSteps([])
    setRunResult(null)

    try {
      const response = await fetch(`/api/v1/agents/${id}/run`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ input }),
      })

      if (!response.ok) throw new Error('Run failed')

      const reader = response.body?.getReader()
      if (!reader) throw new Error('No response body')

      const decoder = new TextDecoder()
      let buffer = ''
      let currentEventType = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (line.startsWith('event: ')) {
            currentEventType = line.slice(7).trim()
          } else if (line.startsWith('data: ')) {
            const data = line.slice(6)
            try {
              const parsed = JSON.parse(data)
              if (currentEventType === 'step') {
                setLiveSteps((prev) => [...prev, parsed.step])
              } else if (currentEventType === 'finished') {
                setRunResult(parsed.run)
              }
            } catch {
              // skip
            }
          }
        }
      }
    } catch (err) {
      console.error('Agent run error:', err)
    } finally {
      setIsRunning(false)
    }
  }

  if (!workflow) {
    return <p className="text-sm text-zinc-500">Loading workflow...</p>
  }

  const tabs: { key: Tab; label: string }[] = [
    { key: 'run', label: 'Run Agent' },
    { key: 'history', label: 'Run History' },
    { key: 'trace', label: 'Trace View' },
  ]

  const stepsToShow = runResult?.steps ?? liveSteps

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <button
          className="text-zinc-400 hover:text-zinc-50"
          onClick={() => navigate('/agents')}
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <div>
          <h1 className="text-2xl font-bold text-zinc-50">{workflow.name}</h1>
          {workflow.description && (
            <p className="text-sm text-zinc-400">{workflow.description}</p>
          )}
        </div>
      </div>

      <div className="flex items-center gap-4 text-sm text-zinc-400">
        <span className="rounded bg-zinc-700 px-2 py-0.5">{workflow.pattern}</span>
        <span>{workflow.model}</span>
        <span>Max {workflow.maxSteps} steps</span>
        <span>{workflow.tokenBudget} token budget</span>
        <span>v{workflow.version}</span>
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

      {activeTab === 'run' && (
        <div className="space-y-4">
          <div className="flex gap-2">
            <textarea
              className="flex-1 rounded border border-zinc-700 bg-zinc-800 px-3 py-2 text-sm text-zinc-50 min-h-[80px]"
              placeholder="Enter your question or task..."
              value={input}
              onChange={(e) => setInput(e.target.value)}
            />
          </div>
          <button
            className="flex items-center gap-2 rounded bg-violet-600 px-4 py-2 text-sm text-white hover:bg-violet-700 disabled:opacity-50"
            onClick={handleRun}
            disabled={isRunning || !input}
          >
            <Play className="h-4 w-4" />
            {isRunning ? 'Running...' : 'Run Agent'}
          </button>

          {stepsToShow.length > 0 && (
            <div className="space-y-3">
              <h3 className="text-sm font-medium text-zinc-50">Execution Trace</h3>
              {stepsToShow.map((step, i) => (
                <StepCard key={i} step={step} />
              ))}
              {runResult && (
                <div className="rounded border border-zinc-700 bg-zinc-800/50 p-3 mt-4">
                  <div className="flex items-center gap-3 text-xs text-zinc-500">
                    <span className={`font-medium ${runResult.status === 'Completed' ? 'text-green-400' : 'text-red-400'}`}>
                      {runResult.status}
                    </span>
                    <span>{runResult.stepCount} steps</span>
                    <span>{runResult.totalTokens} tokens</span>
                    <span>{runResult.totalLatencyMs}ms</span>
                  </div>
                  {runResult.output && (
                    <div className="mt-2 rounded bg-zinc-900 p-3 text-sm text-zinc-50">
                      {runResult.output}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}
        </div>
      )}

      {activeTab === 'history' && (
        <div className="space-y-2">
          {runs && runs.length === 0 && (
            <p className="text-sm text-zinc-500">No runs yet</p>
          )}
          {runs?.map((run: AgentRun) => (
            <div
              key={run.id}
              className="rounded border border-zinc-700 bg-zinc-800/50 p-3 cursor-pointer hover:border-zinc-600"
              onClick={() => {
                setRunResult(run)
                setLiveSteps([])
                setActiveTab('run')
              }}
            >
              <div className="flex items-center justify-between">
                <p className="text-sm text-zinc-50 truncate max-w-[400px]">{run.input}</p>
                <span
                  className={`text-xs px-2 py-0.5 rounded ${
                    run.status === 'Completed'
                      ? 'bg-green-900/50 text-green-400'
                      : run.status === 'Failed'
                        ? 'bg-red-900/50 text-red-400'
                        : 'bg-yellow-900/50 text-yellow-400'
                  }`}
                >
                  {run.status}
                </span>
              </div>
              <div className="mt-1 flex items-center gap-3 text-xs text-zinc-500">
                <span>{run.stepCount} steps</span>
                <span>{run.totalTokens} tokens</span>
                <span>{run.totalLatencyMs}ms</span>
                <span className="flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  {new Date(run.createdAt).toLocaleString()}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}

      {activeTab === 'trace' && (
        <div className="rounded border border-zinc-700 bg-zinc-800/30 h-[500px]">
          {runResult?.steps && runResult.steps.length > 0 ? (
            <AgentTraceView stepsJson={JSON.stringify(runResult.steps.map((s, i) => ({
              step: i + 1,
              type: s.isFinalAnswer ? 'response' : s.action ? 'tool_call' : s.error ? 'error' : 'thought',
              content: s.thought || s.finalAnswer,
              tool: s.action,
              input: s.actionInput,
              output: s.observation,
            })))} />
          ) : (
            <div className="flex items-center justify-center h-full text-zinc-500 text-sm">
              Run an agent first, then select a run to view its trace.
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function StepCard({ step }: { step: AgentStep }) {
  const [expanded, setExpanded] = useState(true)

  return (
    <div className="rounded border border-zinc-700 bg-zinc-800/50 overflow-hidden">
      <button
        className="w-full flex items-center justify-between px-3 py-2 text-left hover:bg-zinc-800"
        onClick={() => setExpanded(!expanded)}
      >
        <div className="flex items-center gap-2">
          <span className="text-xs font-mono text-zinc-500">#{step.index + 1}</span>
          {step.isFinalAnswer ? (
            <span className="text-xs font-medium text-green-400">Final Answer</span>
          ) : step.action ? (
            <span className="text-xs font-medium text-violet-400">{step.action}</span>
          ) : (
            <span className="text-xs font-medium text-zinc-400">Thinking</span>
          )}
        </div>
        <div className="flex items-center gap-2 text-[10px] text-zinc-500">
          <span>{step.tokensUsed} tokens</span>
          <span>{step.latencyMs}ms</span>
          <span>{expanded ? '▼' : '▶'}</span>
        </div>
      </button>

      {expanded && (
        <div className="border-t border-zinc-700 px-3 py-2 space-y-2">
          {step.thought && (
            <div>
              <span className="text-[10px] uppercase text-zinc-500 font-medium">Thought</span>
              <p className="text-xs text-zinc-300 mt-0.5 whitespace-pre-wrap">{step.thought}</p>
            </div>
          )}
          {step.action && (
            <div>
              <span className="text-[10px] uppercase text-zinc-500 font-medium">Action</span>
              <p className="text-xs text-violet-400 mt-0.5">
                {step.action}({step.actionInput})
              </p>
            </div>
          )}
          {step.observation && (
            <div>
              <span className="text-[10px] uppercase text-zinc-500 font-medium">Observation</span>
              <pre className="text-xs text-zinc-300 mt-0.5 whitespace-pre-wrap bg-zinc-900 rounded p-2 max-h-32 overflow-auto">
                {step.observation}
              </pre>
            </div>
          )}
          {step.finalAnswer && (
            <div>
              <span className="text-[10px] uppercase text-green-500 font-medium">Final Answer</span>
              <p className="text-sm text-zinc-50 mt-0.5 whitespace-pre-wrap">{step.finalAnswer}</p>
            </div>
          )}
          {step.error && (
            <div>
              <span className="text-[10px] uppercase text-red-500 font-medium">Error</span>
              <p className="text-xs text-red-400 mt-0.5">{step.error}</p>
            </div>
          )}
        </div>
      )}
    </div>
  )
}
