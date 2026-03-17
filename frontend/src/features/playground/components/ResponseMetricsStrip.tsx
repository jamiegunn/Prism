import { Timer, Zap, Hash, Gauge, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { PerplexityBadge } from '@/components/logprobs/PerplexityBadge'
import type { Message } from '../types'

interface ResponseMetricsStripProps {
  message: Message
  className?: string
  onReplay?: () => void
}

export function ResponseMetricsStrip({ message, className, onReplay }: ResponseMetricsStripProps) {
  if (message.role !== 'Assistant') return null

  return (
    <div className={cn(
      'flex items-center gap-3 px-3 py-1.5 text-[11px] text-zinc-500 border-t border-zinc-800/50',
      className
    )}>
      {message.tokenCount != null && (
        <MetricItem icon={Hash} label="tokens" value={String(message.tokenCount)} />
      )}
      {message.latencyMs != null && (
        <MetricItem icon={Timer} label="ms" value={String(message.latencyMs)} />
      )}
      {message.ttftMs != null && (
        <MetricItem icon={Zap} label="TTFT" value={`${message.ttftMs}ms`} />
      )}
      {message.tokensPerSecond != null && (
        <MetricItem icon={Gauge} label="tok/s" value={message.tokensPerSecond.toFixed(1)} />
      )}
      {message.perplexity != null && (
        <PerplexityBadge perplexity={message.perplexity} />
      )}
      {message.finishReason && message.finishReason !== 'stop' && (
        <span className="flex items-center gap-1 text-amber-500">
          <AlertTriangle className="h-3 w-3" />
          {message.finishReason}
        </span>
      )}
      <div className="flex-1" />
      {onReplay && (
        <button
          onClick={onReplay}
          className="text-zinc-600 hover:text-zinc-300 transition-colors text-[11px]"
        >
          Replay
        </button>
      )}
    </div>
  )
}

function MetricItem({ icon: Icon, label, value }: { icon: typeof Timer; label: string; value: string }) {
  return (
    <span className="flex items-center gap-1 tabular-nums">
      <Icon className="h-3 w-3" />
      <span className="text-zinc-400">{value}</span>
      <span>{label}</span>
    </span>
  )
}
