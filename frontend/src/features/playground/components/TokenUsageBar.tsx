import { cn } from '@/lib/utils'
import type { Message } from '../types'

interface TokenUsageBarProps {
  messages: Message[]
  className?: string
}

export function TokenUsageBar({ messages, className }: TokenUsageBarProps) {
  const assistantMessages = messages.filter((m) => m.role === 'Assistant')
  const userMessages = messages.filter((m) => m.role === 'User')

  const promptTokens = userMessages.reduce((sum, m) => sum + (m.tokenCount ?? 0), 0)
  const completionTokens = assistantMessages.reduce((sum, m) => sum + (m.tokenCount ?? 0), 0)
  const totalTokens = promptTokens + completionTokens

  const lastAssistant = assistantMessages[assistantMessages.length - 1]
  const latency = lastAssistant?.latencyMs
  const ttft = lastAssistant?.ttftMs
  const tokPerSec = lastAssistant?.tokensPerSecond

  if (totalTokens === 0 && !latency) return null

  return (
    <div
      className={cn(
        'flex items-center gap-3 border-t border-zinc-800 bg-zinc-950 px-4 py-1.5 text-[10px] text-zinc-500',
        className
      )}
    >
      {totalTokens > 0 && (
        <>
          <span>
            <span className="text-zinc-400">{promptTokens}</span> prompt
          </span>
          <span className="text-zinc-700">/</span>
          <span>
            <span className="text-zinc-400">{completionTokens}</span> completion
          </span>
          <span className="text-zinc-700">/</span>
          <span>
            <span className="text-zinc-400">{totalTokens}</span> total
          </span>
        </>
      )}
      {latency != null && (
        <>
          <span className="text-zinc-700">|</span>
          <span>
            <span className="text-zinc-400">{latency}</span>ms latency
          </span>
        </>
      )}
      {ttft != null && (
        <>
          <span className="text-zinc-700">|</span>
          <span>
            <span className="text-zinc-400">{ttft}</span>ms TTFT
          </span>
        </>
      )}
      {tokPerSec != null && (
        <>
          <span className="text-zinc-700">|</span>
          <span>
            <span className="text-zinc-400">{tokPerSec.toFixed(1)}</span> tok/s
          </span>
        </>
      )}
    </div>
  )
}
