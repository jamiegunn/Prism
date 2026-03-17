import { cn } from '@/lib/utils'
import { getTokenColor } from '@/lib/logprobs'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import type { TokenPredictionEntry } from '../types'
import { useTokenExplorerStore } from '../store'

interface ProbabilityDistributionProps {
  predictions: TokenPredictionEntry[]
  totalProbability: number
  onTokenClick?: (token: string) => void
  className?: string
}

export function ProbabilityDistribution({
  predictions,
  totalProbability,
  onTokenClick,
  className,
}: ProbabilityDistributionProps) {
  const { topP, topK } = useTokenExplorerStore()

  if (predictions.length === 0) {
    return (
      <div className={cn('flex h-64 items-center justify-center text-zinc-500', className)}>
        No predictions yet. Enter a prompt and click Predict.
      </div>
    )
  }

  const maxProb = predictions[0]?.probability ?? 0

  // Calculate top-p cutoff index: first index where cumulative >= topP
  const topPCutoffIndex = predictions.findIndex(
    (p) => p.cumulativeProbability >= topP
  )

  return (
    <div className={cn('space-y-1', className)}>
      {predictions.map((entry, index) => {
        const barWidth = maxProb > 0 ? (entry.probability / maxProb) * 100 : 0
        const isAboveTopP = topPCutoffIndex < 0 || index <= topPCutoffIndex
        const isAboveTopK = index < topK
        const isPastCutoff = !isAboveTopP || !isAboveTopK

        return (
          <Tooltip key={`${entry.token}-${index}`}>
            <TooltipTrigger className="w-full">
              <button
                onClick={() => onTokenClick?.(entry.token)}
                className={cn(
                  'group flex w-full items-center gap-2 rounded px-2 py-1 text-left transition-colors hover:bg-zinc-800/80',
                  isPastCutoff && 'opacity-50'
                )}
              >
                {/* Rank */}
                <span className="w-6 shrink-0 text-right text-xs text-zinc-500">
                  {index + 1}
                </span>

                {/* Token */}
                <span
                  className={cn(
                    'w-28 shrink-0 truncate font-mono text-sm',
                    getTokenColor(entry.logprob)
                  )}
                >
                  {formatTokenDisplay(entry.token)}
                </span>

                {/* Probability bar */}
                <div className="relative flex-1 h-5">
                  <div
                    className={cn(
                      'absolute inset-y-0 left-0 rounded-sm transition-all',
                      getBarColor(entry.probability)
                    )}
                    style={{ width: `${barWidth}%` }}
                  />
                  {/* Cumulative probability line */}
                  <div
                    className="absolute top-0 h-full w-0.5 bg-violet-500/70"
                    style={{ left: `${Math.min(entry.cumulativeProbability * 100, 100)}%` }}
                  />
                </div>

                {/* Percentage */}
                <span className="w-16 shrink-0 text-right font-mono text-xs text-zinc-400">
                  {(entry.probability * 100).toFixed(2)}%
                </span>

                {/* Logprob */}
                <span className="w-14 shrink-0 text-right font-mono text-xs text-zinc-500">
                  {entry.logprob.toFixed(3)}
                </span>
              </button>
            </TooltipTrigger>
            <TooltipContent side="right">
              <div className="space-y-1 text-xs">
                <div>
                  Token: <span className="font-mono">{JSON.stringify(entry.token)}</span>
                </div>
                <div>Probability: {(entry.probability * 100).toFixed(4)}%</div>
                <div>Log-prob: {entry.logprob.toFixed(6)}</div>
                <div>Cumulative: {(entry.cumulativeProbability * 100).toFixed(2)}%</div>
                <div>Rank: #{index + 1}</div>
              </div>
            </TooltipContent>
          </Tooltip>
        )
      })}

      {/* Cutoff indicators legend */}
      <div className="flex items-center gap-4 border-t border-zinc-800 pt-2 mt-2">
        <div className="flex items-center gap-1.5">
          <div className="h-3 w-0.5 bg-violet-500/70" />
          <span className="text-xs text-zinc-500">Cumulative probability</span>
        </div>
        <span className="text-xs text-zinc-500">
          Top-p ({topP}): {topPCutoffIndex >= 0 ? topPCutoffIndex + 1 : predictions.length} tokens
        </span>
        <span className="text-xs text-zinc-500">
          Top-k ({topK}): {Math.min(topK, predictions.length)} tokens
        </span>
      </div>

      <div className="text-xs text-zinc-500 pt-1">
        Total probability of top-{predictions.length}: {(totalProbability * 100).toFixed(2)}%
      </div>
    </div>
  )
}

function getBarColor(probability: number): string {
  if (probability >= 0.5) return 'bg-emerald-500/40'
  if (probability >= 0.2) return 'bg-emerald-400/30'
  if (probability >= 0.1) return 'bg-yellow-500/30'
  if (probability >= 0.05) return 'bg-orange-500/25'
  if (probability >= 0.01) return 'bg-red-400/20'
  return 'bg-red-600/15'
}

function formatTokenDisplay(token: string): string {
  // Make whitespace characters visible
  return token
    .replace(/ /g, '\u2423')  // open box for space
    .replace(/\n/g, '\u21B5') // return symbol for newline
    .replace(/\t/g, '\u21E5') // tab symbol
}
