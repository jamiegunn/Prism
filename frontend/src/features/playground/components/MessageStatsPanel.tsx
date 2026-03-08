import { cn } from '@/lib/utils'
import { ScrollArea } from '@/components/ui/scroll-area'
import { PerplexityBadge } from '@/components/logprobs/PerplexityBadge'
import type { Message } from '../types'

interface MessageStatsPanelProps {
  messages: Message[]
  className?: string
}

export function MessageStatsPanel({ messages, className }: MessageStatsPanelProps) {
  const assistantMessages = messages.filter((m) => m.role === 'Assistant')

  if (assistantMessages.length === 0) return null

  // Aggregate stats
  const totalPromptTokens = messages
    .filter((m) => m.role === 'User')
    .reduce((sum, m) => sum + (m.tokenCount ?? 0), 0)
  const totalCompletionTokens = assistantMessages.reduce(
    (sum, m) => sum + (m.tokenCount ?? 0),
    0
  )
  const totalTokens = totalPromptTokens + totalCompletionTokens

  const avgLatency =
    assistantMessages.filter((m) => m.latencyMs != null).length > 0
      ? Math.round(
          assistantMessages
            .filter((m) => m.latencyMs != null)
            .reduce((sum, m) => sum + m.latencyMs!, 0) /
            assistantMessages.filter((m) => m.latencyMs != null).length
        )
      : null

  const avgTokPerSec =
    assistantMessages.filter((m) => m.tokensPerSecond != null).length > 0
      ? assistantMessages
          .filter((m) => m.tokensPerSecond != null)
          .reduce((sum, m) => sum + m.tokensPerSecond!, 0) /
        assistantMessages.filter((m) => m.tokensPerSecond != null).length
      : null

  return (
    <ScrollArea className={cn('border-l border-zinc-800 bg-zinc-900/50', className)}>
      <div className="p-4 space-y-5">
        <h3 className="text-xs font-semibold uppercase tracking-wider text-zinc-500">
          Statistics
        </h3>

        {/* Conversation Summary Table */}
        <div>
          <h4 className="text-[11px] font-medium text-zinc-400 mb-2">Conversation</h4>
          <table className="w-full text-xs">
            <tbody className="divide-y divide-zinc-800">
              <StatRow label="Messages" value={String(messages.length)} />
              <StatRow label="Prompt tokens" value={totalPromptTokens.toLocaleString()} />
              <StatRow
                label="Completion tokens"
                value={totalCompletionTokens.toLocaleString()}
              />
              <StatRow
                label="Total tokens"
                value={totalTokens.toLocaleString()}
                highlight
              />
              {avgLatency != null && (
                <StatRow label="Avg latency" value={`${avgLatency.toLocaleString()}ms`} />
              )}
              {avgTokPerSec != null && (
                <StatRow label="Avg throughput" value={`${avgTokPerSec.toFixed(1)} tok/s`} />
              )}
            </tbody>
          </table>
        </div>

        {/* Per-Message Stats */}
        {assistantMessages.map((msg, index) => (
          <div key={msg.id}>
            <h4 className="text-[11px] font-medium text-zinc-400 mb-2">
              Response #{index + 1}
            </h4>
            <table className="w-full text-xs">
              <tbody className="divide-y divide-zinc-800">
                {msg.tokenCount != null && (
                  <StatRow label="Tokens" value={msg.tokenCount.toLocaleString()} />
                )}
                {msg.latencyMs != null && (
                  <StatRow label="Latency" value={`${msg.latencyMs.toLocaleString()}ms`} />
                )}
                {msg.ttftMs != null && (
                  <StatRow label="TTFT" value={`${msg.ttftMs}ms`} />
                )}
                {msg.tokensPerSecond != null && (
                  <StatRow label="Throughput" value={`${msg.tokensPerSecond.toFixed(1)} tok/s`} />
                )}
                {msg.perplexity != null && (
                  <tr>
                    <td className="py-1.5 pr-3 text-zinc-500 whitespace-nowrap">Perplexity</td>
                    <td className="py-1.5 text-right">
                      <PerplexityBadge
                        perplexity={msg.perplexity}
                        className="text-[10px] px-1.5 py-0"
                      />
                    </td>
                  </tr>
                )}
                {msg.finishReason && (
                  <StatRow label="Finish reason" value={msg.finishReason} />
                )}
              </tbody>
            </table>
          </div>
        ))}
      </div>
    </ScrollArea>
  )
}

interface StatRowProps {
  label: string
  value: string
  highlight?: boolean
}

function StatRow({ label, value, highlight }: StatRowProps) {
  return (
    <tr>
      <td className="py-1.5 pr-3 text-zinc-500 whitespace-nowrap">{label}</td>
      <td
        className={cn(
          'py-1.5 text-right font-mono tabular-nums',
          highlight ? 'text-zinc-200 font-medium' : 'text-zinc-300'
        )}
      >
        {value}
      </td>
    </tr>
  )
}
