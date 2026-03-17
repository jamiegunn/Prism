import { useState } from 'react'
import { ChevronDown, ChevronRight, Brain, Wrench, MessageSquare, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'

interface AgentStep {
  step: number
  type: 'thought' | 'tool_call' | 'response' | 'error'
  content?: string
  tool?: string
  input?: string
  output?: string
}

interface AgentTraceViewProps {
  stepsJson: string | null
  className?: string
}

const STEP_ICONS = {
  thought: Brain,
  tool_call: Wrench,
  response: MessageSquare,
  error: AlertTriangle,
} as const

const STEP_COLORS = {
  thought: 'border-violet-800/50 bg-violet-950/20',
  tool_call: 'border-cyan-800/50 bg-cyan-950/20',
  response: 'border-emerald-800/50 bg-emerald-950/20',
  error: 'border-red-800/50 bg-red-950/20',
} as const

const STEP_LABELS = {
  thought: 'Thought',
  tool_call: 'Tool Call',
  response: 'Response',
  error: 'Error',
} as const

export function AgentTraceView({ stepsJson, className }: AgentTraceViewProps) {
  const [expandedSteps, setExpandedSteps] = useState<Set<number>>(new Set())

  if (!stepsJson) {
    return (
      <div className="text-sm text-zinc-500 p-4 text-center">
        No trace data available.
      </div>
    )
  }

  let steps: AgentStep[]
  try {
    steps = JSON.parse(stepsJson)
  } catch {
    return (
      <div className="text-sm text-red-400 p-4">
        Failed to parse trace data.
      </div>
    )
  }

  if (steps.length === 0) {
    return (
      <div className="text-sm text-zinc-500 p-4 text-center">
        No steps recorded.
      </div>
    )
  }

  function toggleStep(step: number) {
    setExpandedSteps((prev) => {
      const next = new Set(prev)
      if (next.has(step)) next.delete(step)
      else next.add(step)
      return next
    })
  }

  return (
    <ScrollArea className={cn('h-full', className)}>
      <div className="space-y-2 p-4">
        <h4 className="text-xs font-medium text-zinc-400 mb-3">
          Execution Trace ({steps.length} steps)
        </h4>

        {/* Timeline */}
        <div className="relative pl-6">
          {/* Vertical line */}
          <div className="absolute left-[11px] top-2 bottom-2 w-px bg-zinc-800" />

          {steps.map((step) => {
            const Icon = STEP_ICONS[step.type] ?? Brain
            const colorClass = STEP_COLORS[step.type] ?? STEP_COLORS.thought
            const label = STEP_LABELS[step.type] ?? step.type
            const isExpanded = expandedSteps.has(step.step)
            const hasDetail = step.content || step.input || step.output

            return (
              <div key={step.step} className="relative mb-3">
                {/* Step dot */}
                <div className="absolute -left-6 top-2 h-[22px] w-[22px] rounded-full bg-zinc-900 border border-zinc-700 flex items-center justify-center">
                  <Icon className="h-3 w-3 text-zinc-400" />
                </div>

                {/* Step card */}
                <div className={cn('rounded border p-3', colorClass)}>
                  <button
                    onClick={() => hasDetail && toggleStep(step.step)}
                    className="flex items-center gap-2 w-full text-left"
                  >
                    {hasDetail && (
                      isExpanded
                        ? <ChevronDown className="h-3 w-3 text-zinc-500 shrink-0" />
                        : <ChevronRight className="h-3 w-3 text-zinc-500 shrink-0" />
                    )}
                    <Badge variant="outline" className="text-[10px]">
                      Step {step.step}
                    </Badge>
                    <span className="text-xs font-medium text-zinc-300">{label}</span>
                    {step.tool && (
                      <Badge variant="secondary" className="text-[10px]">{step.tool}</Badge>
                    )}
                  </button>

                  {isExpanded && (
                    <div className="mt-2 space-y-2 text-xs">
                      {step.content && (
                        <pre className="whitespace-pre-wrap text-zinc-400 bg-zinc-950/50 rounded p-2">
                          {step.content}
                        </pre>
                      )}
                      {step.input && (
                        <div>
                          <span className="text-zinc-500 font-medium">Input:</span>
                          <pre className="whitespace-pre-wrap text-zinc-400 bg-zinc-950/50 rounded p-2 mt-1">
                            {step.input}
                          </pre>
                        </div>
                      )}
                      {step.output && (
                        <div>
                          <span className="text-zinc-500 font-medium">Output:</span>
                          <pre className="whitespace-pre-wrap text-zinc-400 bg-zinc-950/50 rounded p-2 mt-1">
                            {step.output}
                          </pre>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </ScrollArea>
  )
}
