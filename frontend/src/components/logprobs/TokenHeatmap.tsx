import { cn } from '@/lib/utils'
import { getTokenColor, getTokenBgColor, logprobToProb } from '@/lib/logprobs'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import type { TokenLogprob } from '@/services/types/logprobs'

interface TokenHeatmapProps {
  tokens: TokenLogprob[]
  onTokenClick?: (index: number) => void
  selectedIndex?: number
}

export function TokenHeatmap({ tokens, onTokenClick, selectedIndex }: TokenHeatmapProps) {
  if (tokens.length === 0) {
    return <p className="text-sm text-zinc-500">No logprob data available.</p>
  }

  return (
    <div className="flex flex-wrap gap-0 leading-relaxed">
      {tokens.map((token, index) => {
        const probability = token.probability ?? logprobToProb(token.logprob)
        const rank =
          token.topLogprobs.length > 0
            ? token.topLogprobs.findIndex((t) => t.token === token.token) + 1
            : 0

        return (
          <Tooltip key={index}>
            <TooltipTrigger>
              <span
                className={cn(
                  'cursor-pointer rounded-sm px-0.5 py-0.5 text-sm font-mono transition-all',
                  getTokenBgColor(token.logprob),
                  getTokenColor(token.logprob),
                  selectedIndex === index && 'ring-2 ring-violet-500 ring-offset-1 ring-offset-zinc-950',
                  onTokenClick && 'hover:opacity-80'
                )}
                onClick={() => onTokenClick?.(index)}
              >
                {token.token.replace(/ /g, '\u00B7').replace(/\n/g, '\u21B5\n')}
              </span>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="max-w-xs">
              <div className="space-y-1 text-xs">
                <div>
                  <span className="text-zinc-400">Token:</span>{' '}
                  <span className="font-mono font-medium">{JSON.stringify(token.token)}</span>
                </div>
                <div>
                  <span className="text-zinc-400">Logprob:</span>{' '}
                  <span className="font-mono">{token.logprob.toFixed(4)}</span>
                </div>
                <div>
                  <span className="text-zinc-400">Probability:</span>{' '}
                  <span className="font-mono">{(probability * 100).toFixed(2)}%</span>
                </div>
                {rank > 0 && (
                  <div>
                    <span className="text-zinc-400">Rank:</span>{' '}
                    <span className="font-mono">#{rank}</span>
                  </div>
                )}
              </div>
            </TooltipContent>
          </Tooltip>
        )
      })}
    </div>
  )
}
