import { cn } from '@/lib/utils'
import { calculateEntropy, logprobToProb } from '@/lib/logprobs'
import type { TokenLogprob } from '@/services/types/logprobs'

interface AlternativeTokensPanelProps {
  token: TokenLogprob
}

export function AlternativeTokensPanel({ token }: AlternativeTokensPanelProps) {
  const alternatives = token.topLogprobs
  const chosenToken = token.token
  const probabilities = alternatives.map((alt) => alt.probability ?? logprobToProb(alt.logprob))
  const entropy = calculateEntropy(probabilities)
  const isGreedy =
    alternatives.length > 0 &&
    alternatives[0].token === chosenToken

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-3">
        <div className="text-xs text-zinc-400">
          Entropy: <span className="font-mono text-zinc-200">{entropy.toFixed(3)} bits</span>
        </div>
        <div className="text-xs">
          {isGreedy ? (
            <span className="text-emerald-400">Greedy (top-1 chosen)</span>
          ) : (
            <span className="text-amber-400">Sampled (not top-1)</span>
          )}
        </div>
      </div>

      <div className="space-y-1.5">
        {alternatives.map((alt, index) => {
          const probability = alt.probability ?? logprobToProb(alt.logprob)
          const isChosen = alt.token === chosenToken
          const maxProb = Math.max(
            ...alternatives.map((a) => a.probability ?? logprobToProb(a.logprob))
          )
          const barWidth = maxProb > 0 ? (probability / maxProb) * 100 : 0

          return (
            <div key={index} className="flex items-center gap-2">
              <span
                className={cn(
                  'w-24 shrink-0 truncate text-right font-mono text-xs',
                  isChosen ? 'text-violet-400 font-semibold' : 'text-zinc-400'
                )}
                title={JSON.stringify(alt.token)}
              >
                {alt.token.replace(/ /g, '\u00B7')}
              </span>
              <div className="flex-1 h-5 rounded bg-zinc-800 overflow-hidden">
                <div
                  className={cn(
                    'h-full rounded transition-all',
                    isChosen ? 'bg-violet-600' : 'bg-zinc-600'
                  )}
                  style={{ width: `${barWidth}%` }}
                />
              </div>
              <span className="w-16 shrink-0 text-right font-mono text-xs text-zinc-400">
                {(probability * 100).toFixed(1)}%
              </span>
            </div>
          )
        })}
      </div>

      {alternatives.length === 0 && (
        <p className="text-xs text-zinc-500">No alternative tokens available.</p>
      )}
    </div>
  )
}
