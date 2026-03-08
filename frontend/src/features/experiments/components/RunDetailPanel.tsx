import { X, Clock, Cpu, DollarSign, Zap, Hash } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import type { Run } from '../types'

interface RunDetailPanelProps {
  run: Run
  onClose: () => void
}

export function RunDetailPanel({ run, onClose }: RunDetailPanelProps) {
  const metricEntries = Object.entries(run.metrics)

  return (
    <div className="rounded-lg border border-border bg-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h3 className="font-medium text-zinc-100">{run.name || `Run ${run.id.slice(0, 8)}`}</h3>
        <Button size="sm" variant="ghost" onClick={onClose} className="h-7 w-7 p-0">
          <X className="h-4 w-4" />
        </Button>
      </div>

      <ScrollArea className="h-[calc(100vh-16rem)]">
        <div className="p-4 space-y-5">
          {/* Status & Model */}
          <div className="flex flex-wrap gap-2">
            <Badge variant="secondary" className="font-mono">{run.model}</Badge>
            <Badge variant="outline">{run.status}</Badge>
            {run.finishReason && <Badge variant="outline">{run.finishReason}</Badge>}
          </div>

          {/* Key Metrics */}
          <div className="grid grid-cols-2 gap-3">
            <MetricCard icon={Clock} label="Latency" value={`${run.latencyMs.toLocaleString()}ms`} />
            <MetricCard icon={Cpu} label="Tokens" value={run.totalTokens.toLocaleString()} />
            <MetricCard icon={DollarSign} label="Cost" value={run.cost != null ? `$${run.cost.toFixed(4)}` : '-'} />
            <MetricCard icon={Zap} label="Tokens/s" value={run.tokensPerSecond?.toFixed(1) ?? '-'} />
            {run.ttftMs != null && (
              <MetricCard icon={Clock} label="TTFT" value={`${run.ttftMs}ms`} />
            )}
            {run.perplexity != null && (
              <MetricCard icon={Hash} label="Perplexity" value={run.perplexity.toFixed(2)} />
            )}
          </div>

          {/* Token Breakdown */}
          <div>
            <h4 className="text-xs font-medium text-zinc-400 mb-2">Token Breakdown</h4>
            <div className="flex gap-4 text-sm">
              <span className="text-zinc-400">Prompt: <span className="text-zinc-200">{run.promptTokens}</span></span>
              <span className="text-zinc-400">Completion: <span className="text-zinc-200">{run.completionTokens}</span></span>
            </div>
          </div>

          {/* Parameters */}
          <div>
            <h4 className="text-xs font-medium text-zinc-400 mb-2">Parameters</h4>
            <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
              {run.parameters.temperature != null && (
                <ParamRow label="Temperature" value={run.parameters.temperature} />
              )}
              {run.parameters.topP != null && (
                <ParamRow label="Top P" value={run.parameters.topP} />
              )}
              {run.parameters.topK != null && (
                <ParamRow label="Top K" value={run.parameters.topK} />
              )}
              {run.parameters.maxTokens != null && (
                <ParamRow label="Max Tokens" value={run.parameters.maxTokens} />
              )}
              {run.parameters.frequencyPenalty != null && (
                <ParamRow label="Freq. Penalty" value={run.parameters.frequencyPenalty} />
              )}
              {run.parameters.presencePenalty != null && (
                <ParamRow label="Pres. Penalty" value={run.parameters.presencePenalty} />
              )}
            </div>
          </div>

          {/* Custom Metrics */}
          {metricEntries.length > 0 && (
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-2">Custom Metrics</h4>
              <div className="grid grid-cols-2 gap-x-4 gap-y-1 text-sm">
                {metricEntries.map(([key, value]) => (
                  <ParamRow key={key} label={key} value={value} />
                ))}
              </div>
            </div>
          )}

          <Separator />

          {/* Tags */}
          {run.tags.length > 0 && (
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-2">Tags</h4>
              <div className="flex flex-wrap gap-1">
                {run.tags.map((tag) => (
                  <Badge key={tag} variant="secondary" className="text-xs">{tag}</Badge>
                ))}
              </div>
            </div>
          )}

          {/* System Prompt */}
          {run.systemPrompt && (
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-2">System Prompt</h4>
              <pre className="rounded bg-zinc-900 p-3 text-xs text-zinc-300 whitespace-pre-wrap max-h-32 overflow-auto">
                {run.systemPrompt}
              </pre>
            </div>
          )}

          {/* Input */}
          <div>
            <h4 className="text-xs font-medium text-zinc-400 mb-2">Input</h4>
            <pre className="rounded bg-zinc-900 p-3 text-xs text-zinc-300 whitespace-pre-wrap max-h-48 overflow-auto">
              {run.input}
            </pre>
          </div>

          {/* Output */}
          {run.output && (
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-2">Output</h4>
              <pre className="rounded bg-zinc-900 p-3 text-xs text-zinc-300 whitespace-pre-wrap max-h-48 overflow-auto">
                {run.output}
              </pre>
            </div>
          )}

          {/* Error */}
          {run.error && (
            <div>
              <h4 className="text-xs font-medium text-red-400 mb-2">Error</h4>
              <pre className="rounded bg-red-950/30 border border-red-500/20 p-3 text-xs text-red-300 whitespace-pre-wrap">
                {run.error}
              </pre>
            </div>
          )}
        </div>
      </ScrollArea>
    </div>
  )
}

function MetricCard({
  icon: Icon,
  label,
  value,
}: {
  icon: typeof Clock
  label: string
  value: string
}) {
  return (
    <div className="rounded-md bg-zinc-900 px-3 py-2">
      <div className="flex items-center gap-1.5 text-xs text-zinc-500 mb-0.5">
        <Icon className="h-3 w-3" />
        {label}
      </div>
      <div className="text-sm font-medium text-zinc-200 tabular-nums">{value}</div>
    </div>
  )
}

function ParamRow({ label, value }: { label: string; value: string | number }) {
  return (
    <>
      <span className="text-zinc-500">{label}</span>
      <span className="text-zinc-300 tabular-nums">{value}</span>
    </>
  )
}
