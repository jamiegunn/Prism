import { Info } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Card, CardContent } from '@/components/ui/card'
import { Separator } from '@/components/ui/separator'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import { calculateEntropy } from '@/lib/logprobs'
import { useTokenExplorerStore } from '../store'
import type { TokenPredictionEntry } from '../types'

export function SamplingVisualization() {
  const { currentPredictions, topP, topK } = useTokenExplorerStore()

  if (!currentPredictions || currentPredictions.predictions.length === 0) {
    return (
      <div className="space-y-4">
        <h3 className="text-sm font-semibold text-zinc-300">Sampling Analysis</h3>
        <p className="text-xs text-zinc-500">
          Run a prediction to see sampling statistics.
        </p>
      </div>
    )
  }

  const predictions = currentPredictions.predictions

  const stats = computeStats(predictions, topP, topK)

  return (
    <div className="space-y-4">
      <h3 className="text-sm font-semibold text-zinc-300">Sampling Analysis</h3>

      <StatCard
        label="Effective Vocab"
        tooltip="The number of tokens that have a non-trivial probability (> 1%). A small number means the model is focused on a few choices; a large number means many tokens are plausible."
        value={String(stats.effectiveVocab)}
        description="Tokens with P > 1%"
      />

      <StatCard
        label="Entropy"
        tooltip="Shannon entropy of the probability distribution, measured in bits. Low entropy (near 0) means the model is very certain. High entropy means probability is spread across many tokens. Maximum entropy would mean all tokens are equally likely."
        value={stats.entropy.toFixed(3)}
        unit="bits"
        description="Distribution uncertainty"
      />

      <Separator />

      <StatCard
        label="Top-p Coverage"
        tooltip="How many tokens are needed to reach the top-p probability threshold you set in the left panel. Fewer tokens means a more concentrated distribution. This shows the actual nucleus that top-p sampling would use."
        value={String(stats.topPTokenCount)}
        unit={`tokens for p=${topP}`}
        description={`${(stats.topPMass * 100).toFixed(1)}% probability mass`}
      />

      <StatCard
        label="Top-k Effect"
        tooltip="The total probability mass captured by the top-k tokens. If this is close to 100%, the top-k cutoff has little effect. If it's much lower, top-k is discarding significant probability mass from less likely tokens."
        value={`${(stats.topKMass * 100).toFixed(1)}%`}
        unit={`in top-${topK}`}
        description={`${stats.topKActual} tokens available`}
      />

      <Separator />

      <StatCard
        label="Max Probability"
        tooltip="The probability assigned to the single most likely next token. A high value means the model has a strong preference. A low value means many tokens are competitive, and sampling will produce more varied results."
        value={`${(stats.maxProb * 100).toFixed(2)}%`}
        description={`Token: "${stats.maxToken}"`}
      />

      <StatCard
        label="Model Confidence"
        tooltip="A qualitative summary of how certain the model is, derived from the max probability. Very High (>80%) means the next token is almost predetermined. Very Low (<10%) means the model is genuinely uncertain and output will vary significantly across samples."
        value={getConfidenceLabel(stats.maxProb)}
        description={getConfidenceDescription(stats.maxProb)}
        color={getConfidenceColor(stats.maxProb)}
      />

      <Separator />

      <div className="space-y-2">
        <Tooltip>
          <TooltipTrigger>
            <span className="inline-flex items-center gap-1 text-xs font-medium text-zinc-400">
              Distribution Shape
              <Info className="h-3 w-3 text-zinc-600" />
            </span>
          </TooltipTrigger>
          <TooltipContent side="bottom" className="w-72 whitespace-normal text-xs leading-relaxed">
            A miniature stacked bar showing how probability is distributed. A single dominant bar means the model is certain. Many small bars means probability is spread widely. The gray area represents all tokens not in the top predictions.
          </TooltipContent>
        </Tooltip>
        <DistributionMiniBar predictions={predictions} />
      </div>

      <div className="space-y-2">
        <h4 className="text-xs font-medium text-zinc-400">Model</h4>
        <p className="font-mono text-xs text-zinc-300 break-all">
          {currentPredictions.modelId}
        </p>
        <p className="text-xs text-zinc-500">
          {currentPredictions.inputTokenCount} input tokens
        </p>
      </div>
    </div>
  )
}

interface StatCardProps {
  label: string
  tooltip?: string
  value: string
  unit?: string
  description?: string
  color?: string
}

function StatCard({ label, tooltip, value, unit, description, color }: StatCardProps) {
  return (
    <Card className="border-zinc-800 bg-zinc-900/50">
      <CardContent className="p-3">
        {tooltip ? (
          <Tooltip>
            <TooltipTrigger>
              <span className="inline-flex items-center gap-1 text-xs font-medium text-zinc-400">
                {label}
                <Info className="h-3 w-3 text-zinc-600" />
              </span>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="w-72 whitespace-normal text-xs leading-relaxed">
              {tooltip}
            </TooltipContent>
          </Tooltip>
        ) : (
          <div className="text-xs font-medium text-zinc-400">{label}</div>
        )}
        <div className="flex items-baseline gap-1.5 mt-0.5">
          <span className={cn('text-lg font-bold', color ?? 'text-zinc-100')}>
            {value}
          </span>
          {unit && <span className="text-xs text-zinc-500">{unit}</span>}
        </div>
        {description && (
          <div className="text-xs text-zinc-500 mt-0.5">{description}</div>
        )}
      </CardContent>
    </Card>
  )
}

interface DistributionMiniBarProps {
  predictions: TokenPredictionEntry[]
}

function DistributionMiniBar({ predictions }: DistributionMiniBarProps) {
  // Show a mini stacked bar of probabilities
  const maxBars = 20
  const items = predictions.slice(0, maxBars)

  return (
    <div className="flex h-8 gap-px rounded overflow-hidden">
      {items.map((entry, index) => (
        <div
          key={index}
          className={cn('transition-all', getSegmentColor(entry.probability))}
          style={{
            width: `${entry.probability * 100}%`,
            minWidth: entry.probability > 0 ? '2px' : '0',
          }}
          title={`${entry.token}: ${(entry.probability * 100).toFixed(2)}%`}
        />
      ))}
      {/* Remaining probability */}
      {predictions.length > 0 && (
        <div
          className="bg-zinc-800 flex-1"
          title={`Other tokens: ${((1 - (predictions[predictions.length - 1]?.cumulativeProbability ?? 0)) * 100).toFixed(2)}%`}
        />
      )}
    </div>
  )
}

function getSegmentColor(probability: number): string {
  if (probability >= 0.3) return 'bg-emerald-500'
  if (probability >= 0.1) return 'bg-emerald-400'
  if (probability >= 0.05) return 'bg-yellow-500'
  if (probability >= 0.02) return 'bg-orange-500'
  return 'bg-red-500'
}

interface ComputedStats {
  effectiveVocab: number
  entropy: number
  topPTokenCount: number
  topPMass: number
  topKMass: number
  topKActual: number
  maxProb: number
  maxToken: string
}

function computeStats(
  predictions: TokenPredictionEntry[],
  topP: number,
  topK: number
): ComputedStats {
  const probs = predictions.map((p) => p.probability)

  // Effective vocabulary: tokens with probability > 1%
  const effectiveVocab = predictions.filter((p) => p.probability > 0.01).length

  // Entropy from available probabilities
  const entropy = calculateEntropy(probs)

  // Top-p: how many tokens to reach the topP threshold
  let topPTokenCount = predictions.length
  let topPMass = 0
  for (let i = 0; i < predictions.length; i++) {
    topPMass += predictions[i].probability
    if (topPMass >= topP) {
      topPTokenCount = i + 1
      break
    }
  }

  // Top-k: probability mass in the top-k tokens
  const topKActual = Math.min(topK, predictions.length)
  const topKMass = predictions
    .slice(0, topKActual)
    .reduce((sum, p) => sum + p.probability, 0)

  const maxProb = predictions[0]?.probability ?? 0
  const maxToken = predictions[0]?.token ?? ''

  return {
    effectiveVocab,
    entropy,
    topPTokenCount,
    topPMass,
    topKMass,
    topKActual,
    maxProb,
    maxToken,
  }
}

function getConfidenceLabel(maxProb: number): string {
  if (maxProb >= 0.8) return 'Very High'
  if (maxProb >= 0.5) return 'High'
  if (maxProb >= 0.2) return 'Moderate'
  if (maxProb >= 0.1) return 'Low'
  return 'Very Low'
}

function getConfidenceDescription(maxProb: number): string {
  if (maxProb >= 0.8) return 'Model is very certain about next token'
  if (maxProb >= 0.5) return 'Model has a clear preference'
  if (maxProb >= 0.2) return 'Multiple viable continuations'
  if (maxProb >= 0.1) return 'Uncertain between many options'
  return 'Highly uncertain, wide distribution'
}

function getConfidenceColor(maxProb: number): string {
  if (maxProb >= 0.8) return 'text-emerald-400'
  if (maxProb >= 0.5) return 'text-emerald-300'
  if (maxProb >= 0.2) return 'text-yellow-400'
  if (maxProb >= 0.1) return 'text-orange-400'
  return 'text-red-400'
}
