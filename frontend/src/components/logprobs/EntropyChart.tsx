import { cn } from '@/lib/utils'
import { calculateEntropy, logprobToProb } from '@/lib/logprobs'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import type { TokenLogprob } from '@/services/types/logprobs'

interface EntropyChartProps {
  tokens: TokenLogprob[]
  className?: string
}

export function EntropyChart({ tokens, className }: EntropyChartProps) {
  if (tokens.length === 0) {
    return <p className="text-sm text-zinc-500">No entropy data available.</p>
  }

  const entropies = tokens.map((token) => {
    const probs = token.topLogprobs.map((t) => t.probability ?? logprobToProb(t.logprob))
    return calculateEntropy(probs)
  })

  const maxEntropy = Math.max(...entropies, 0.001)

  return (
    <div className={cn('flex items-end gap-px h-24', className)}>
      {tokens.map((token, index) => {
        const entropy = entropies[index]
        const heightPct = (entropy / maxEntropy) * 100

        return (
          <Tooltip key={index}>
            <TooltipTrigger className="flex-1 min-w-[2px] h-full flex items-end">
              <div
                className={cn(
                  'w-full rounded-t-sm transition-all',
                  entropy < maxEntropy * 0.3
                    ? 'bg-emerald-500/60'
                    : entropy < maxEntropy * 0.6
                      ? 'bg-amber-500/60'
                      : 'bg-red-500/60'
                )}
                style={{ height: `${Math.max(heightPct, 2)}%` }}
              />
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              <div className="space-y-0.5">
                <div>
                  <span className="text-zinc-400">Token:</span>{' '}
                  <span className="font-mono">{JSON.stringify(token.token)}</span>
                </div>
                <div>
                  <span className="text-zinc-400">Entropy:</span>{' '}
                  <span className="font-mono">{entropy.toFixed(3)} bits</span>
                </div>
              </div>
            </TooltipContent>
          </Tooltip>
        )
      })}
    </div>
  )
}
