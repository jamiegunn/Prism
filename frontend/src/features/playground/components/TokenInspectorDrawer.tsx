import { useState } from 'react'
import { X, ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { cn } from '@/lib/utils'
import { logprobToProb, calculateEntropy, isSurpriseToken, getTokenColor } from '@/lib/logprobs'
import type { LogprobsData } from '@/services/types/logprobs'

interface TokenInspectorDrawerProps {
  logprobsData: LogprobsData
  selectedIndex: number | null
  onSelectIndex: (index: number | null) => void
  onClose: () => void
  className?: string
}

export function TokenInspectorDrawer({
  logprobsData,
  selectedIndex,
  onSelectIndex,
  onClose,
  className,
}: TokenInspectorDrawerProps) {
  const [showAllAlternatives, setShowAllAlternatives] = useState(false)
  const tokens = logprobsData.tokens
  const token = selectedIndex !== null ? tokens[selectedIndex] : null

  const handlePrev = () => {
    if (selectedIndex !== null && selectedIndex > 0) {
      onSelectIndex(selectedIndex - 1)
    }
  }

  const handleNext = () => {
    if (selectedIndex !== null && selectedIndex < tokens.length - 1) {
      onSelectIndex(selectedIndex + 1)
    }
  }

  if (!token) {
    return (
      <div className={cn('w-80 border-l border-zinc-800 bg-zinc-900/70 flex flex-col', className)}>
        <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800">
          <span className="text-sm font-medium text-zinc-300">Token Inspector</span>
          <Button variant="ghost" size="icon" onClick={onClose} className="h-6 w-6">
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
        <div className="flex-1 flex items-center justify-center text-zinc-500 text-sm p-4 text-center">
          Click a token in the heatmap to inspect it
        </div>
      </div>
    )
  }

  const prob = logprobToProb(token.logprob)
  const probs = token.topLogprobs?.map((t) => logprobToProb(t.logprob)) ?? []
  const entropy = calculateEntropy(probs)
  const surprise = isSurpriseToken(prob)
  const alternatives = showAllAlternatives
    ? (token.topLogprobs ?? [])
    : (token.topLogprobs ?? []).slice(0, 10)
  const maxAltProb = Math.max(...(token.topLogprobs ?? []).map((t) => logprobToProb(t.logprob)), 0.001)

  return (
    <div className={cn('w-80 border-l border-zinc-800 bg-zinc-900/70 flex flex-col', className)}>
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-zinc-800">
        <span className="text-sm font-medium text-zinc-300">Token Inspector</span>
        <div className="flex items-center gap-1">
          <Button variant="ghost" size="icon" onClick={handlePrev} disabled={selectedIndex === 0} className="h-6 w-6">
            <ChevronLeft className="h-3.5 w-3.5" />
          </Button>
          <span className="text-xs text-zinc-500 tabular-nums">
            {(selectedIndex ?? 0) + 1}/{tokens.length}
          </span>
          <Button variant="ghost" size="icon" onClick={handleNext} disabled={selectedIndex === tokens.length - 1} className="h-6 w-6">
            <ChevronRight className="h-3.5 w-3.5" />
          </Button>
          <Button variant="ghost" size="icon" onClick={onClose} className="h-6 w-6 ml-1">
            <X className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-4 space-y-4">
          {/* Token display */}
          <div className="text-center">
            <span
              className="inline-block px-3 py-2 rounded text-lg font-mono border border-zinc-700"
              style={{ color: getTokenColor(token.logprob) }}
            >
              {token.token.replace(/ /g, '\u00B7').replace(/\n/g, '\\n')}
            </span>
          </div>

          {/* Metrics grid */}
          <div className="grid grid-cols-2 gap-2">
            <MetricCard label="Logprob" value={token.logprob.toFixed(4)} />
            <MetricCard label="Probability" value={`${(prob * 100).toFixed(2)}%`} />
            <MetricCard label="Entropy" value={entropy.toFixed(3)} unit="nats" />
            <MetricCard label="Position" value={String(selectedIndex)} />
          </div>

          {/* Badges */}
          <div className="flex gap-2 flex-wrap">
            {surprise && (
              <Badge variant="outline" className="border-red-700 text-red-400 text-xs">
                Surprise
              </Badge>
            )}
            <Badge
              variant="outline"
              className={cn(
                'text-xs',
                entropy < 0.5 ? 'border-emerald-700 text-emerald-400' :
                entropy < 1.5 ? 'border-amber-700 text-amber-400' :
                'border-red-700 text-red-400'
              )}
            >
              {entropy < 0.5 ? 'Confident' : entropy < 1.5 ? 'Uncertain' : 'Very Uncertain'}
            </Badge>
          </div>

          {/* Alternatives */}
          {(token.topLogprobs?.length ?? 0) > 0 && (
            <div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-medium text-zinc-400">
                  Top Alternatives ({token.topLogprobs?.length ?? 0})
                </span>
              </div>
              <div className="space-y-1">
                {alternatives.map((alt, i) => {
                  const altProb = logprobToProb(alt.logprob)
                  const isChosen = alt.token === token.token
                  const barWidth = (altProb / maxAltProb) * 100

                  return (
                    <div key={i} className={cn(
                      'flex items-center gap-2 px-2 py-1 rounded text-xs',
                      isChosen && 'bg-violet-950/40 border border-violet-800/50'
                    )}>
                      <span className="font-mono w-24 truncate text-zinc-300" title={alt.token}>
                        {alt.token.replace(/ /g, '\u00B7')}
                      </span>
                      <div className="flex-1 h-3 bg-zinc-800 rounded overflow-hidden">
                        <div
                          className={cn('h-full rounded', isChosen ? 'bg-violet-600' : 'bg-zinc-600')}
                          style={{ width: `${barWidth}%` }}
                        />
                      </div>
                      <span className="text-zinc-500 tabular-nums w-14 text-right">
                        {(altProb * 100).toFixed(1)}%
                      </span>
                    </div>
                  )
                })}
                {(token.topLogprobs?.length ?? 0) > 10 && !showAllAlternatives && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setShowAllAlternatives(true)}
                    className="w-full text-xs text-zinc-500 h-6"
                  >
                    Show all {token.topLogprobs?.length}
                  </Button>
                )}
              </div>
            </div>
          )}

          {/* Context: surrounding tokens */}
          <div>
            <span className="text-xs font-medium text-zinc-400 mb-2 block">Context</span>
            <div className="flex flex-wrap gap-0.5">
              {tokens.slice(Math.max(0, (selectedIndex ?? 0) - 5), (selectedIndex ?? 0) + 6).map((t, i) => {
                const actualIndex = Math.max(0, (selectedIndex ?? 0) - 5) + i
                return (
                  <button
                    key={actualIndex}
                    onClick={() => onSelectIndex(actualIndex)}
                    className={cn(
                      'px-1 py-0.5 rounded text-xs font-mono cursor-pointer',
                      actualIndex === selectedIndex
                        ? 'bg-violet-900/50 ring-1 ring-violet-500 text-violet-300'
                        : 'text-zinc-400 hover:bg-zinc-800'
                    )}
                  >
                    {t.token.replace(/ /g, '\u00B7').replace(/\n/g, '\\n')}
                  </button>
                )
              })}
            </div>
          </div>
        </div>
      </ScrollArea>
    </div>
  )
}

function MetricCard({ label, value, unit }: { label: string; value: string; unit?: string }) {
  return (
    <div className="bg-zinc-800/50 rounded px-3 py-2">
      <div className="text-[10px] text-zinc-500 uppercase tracking-wider">{label}</div>
      <div className="text-sm font-mono text-zinc-200">
        {value}
        {unit && <span className="text-zinc-500 text-xs ml-1">{unit}</span>}
      </div>
    </div>
  )
}
