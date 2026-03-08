import { cn } from '@/lib/utils'
import { getTokenColor, getTokenBgColor, logprobToProb, isSurpriseToken } from '@/lib/logprobs'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import type { TokenLogprob } from '@/services/types/logprobs'

interface SurpriseHighlighterProps {
  tokens: TokenLogprob[]
  threshold?: number
  onTokenClick?: (index: number) => void
  selectedIndex?: number
}

export function SurpriseHighlighter({
  tokens,
  threshold = 0.1,
  onTokenClick,
  selectedIndex,
}: SurpriseHighlighterProps) {
  if (tokens.length === 0) {
    return <p className="text-sm text-zinc-500">No logprob data available.</p>
  }

  return (
    <div className="flex flex-wrap gap-0 leading-relaxed">
      {tokens.map((token, index) => {
        const probability = token.probability ?? logprobToProb(token.logprob)
        const isSurprise = isSurpriseToken(probability, threshold)

        return (
          <Tooltip key={index}>
            <TooltipTrigger>
              <span
                className={cn(
                  'cursor-pointer rounded-sm px-0.5 py-0.5 text-sm font-mono transition-all',
                  getTokenBgColor(token.logprob),
                  getTokenColor(token.logprob),
                  isSurprise && 'underline decoration-wavy decoration-red-500 underline-offset-4',
                  selectedIndex === index && 'ring-2 ring-violet-500 ring-offset-1 ring-offset-zinc-950',
                  onTokenClick && 'hover:opacity-80'
                )}
                onClick={() => onTokenClick?.(index)}
              >
                {token.token.replace(/ /g, '\u00B7').replace(/\n/g, '\u21B5\n')}
              </span>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              <div className="space-y-0.5">
                <div className="font-mono">{JSON.stringify(token.token)}</div>
                <div>
                  Prob: {(probability * 100).toFixed(2)}%
                  {isSurprise && (
                    <span className="ml-2 text-red-400">Surprise!</span>
                  )}
                </div>
              </div>
            </TooltipContent>
          </Tooltip>
        )
      })}
    </div>
  )
}
