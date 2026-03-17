import { useState } from 'react'
import { cn } from '@/lib/utils'
import { getTokenBgColor, getTokenColor } from '@/lib/logprobs'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { GitCompare, ChevronDown } from 'lucide-react'
import { PerplexityBadge } from '@/components/logprobs/PerplexityBadge'
import { useTokenExplorerStore } from '../store'
import type { BranchExploration, BranchToken } from '../types'

interface BranchEntry {
  token: string
  exploration: BranchExploration
}

export function BranchDiffView() {
  const { branches } = useTokenExplorerStore()
  const [leftIdx, setLeftIdx] = useState(0)
  const [rightIdx, setRightIdx] = useState(Math.min(1, branches.length - 1))

  if (branches.length < 2) {
    return (
      <div className="flex h-64 flex-col items-center justify-center gap-2 text-zinc-500">
        <GitCompare className="h-8 w-8 text-zinc-600" />
        <p className="text-sm">Need at least 2 branches to diff.</p>
        <p className="text-xs text-zinc-600">
          Create branches from the Predictions tab, then compare them here.
        </p>
      </div>
    )
  }

  const left = branches[leftIdx]
  const right = branches[rightIdx]
  const maxLen = Math.max(left.exploration.tokens.length, right.exploration.tokens.length)

  // Find first divergence point (token 0 is always different since it's the forced token)
  let firstDivergence = 0
  for (let i = 1; i < maxLen; i++) {
    const lToken = left.exploration.tokens[i]?.token
    const rToken = right.exploration.tokens[i]?.token
    if (lToken !== rToken) {
      firstDivergence = i
      break
    }
  }

  return (
    <div className="space-y-3">
      {/* Branch selectors */}
      <div className="flex items-center gap-3">
        <BranchSelector
          branches={branches}
          selectedIdx={leftIdx}
          onChange={setLeftIdx}
          label="Left"
          color="violet"
        />
        <GitCompare className="h-4 w-4 text-zinc-600 shrink-0" />
        <BranchSelector
          branches={branches}
          selectedIdx={rightIdx}
          onChange={setRightIdx}
          label="Right"
          color="cyan"
        />
      </div>

      {/* Summary stats */}
      <div className="flex items-center gap-4 text-xs text-zinc-500">
        <span>
          First divergence at position {firstDivergence}
        </span>
        <span className="text-zinc-700">|</span>
        <span>
          Left: {left.exploration.tokens.length} tokens
          {left.exploration.perplexity != null && (
            <> &middot; PPL {left.exploration.perplexity.toFixed(2)}</>
          )}
        </span>
        <span className="text-zinc-700">|</span>
        <span>
          Right: {right.exploration.tokens.length} tokens
          {right.exploration.perplexity != null && (
            <> &middot; PPL {right.exploration.perplexity.toFixed(2)}</>
          )}
        </span>
      </div>

      {/* Side-by-side diff */}
      <ScrollArea className="h-[calc(100vh-22rem)]">
        <div className="grid grid-cols-2 gap-2">
          {/* Left header */}
          <div className="flex items-center gap-2 px-3 py-1.5 bg-violet-950/20 rounded-t border border-violet-900/30">
            <Badge variant="outline" className="text-violet-400 border-violet-700 text-[10px]">
              Branch #{leftIdx + 1}
            </Badge>
            <span className="text-xs font-mono text-violet-400">
              forced: &quot;{left.token}&quot;
            </span>
            {left.exploration.perplexity != null && (
              <PerplexityBadge perplexity={left.exploration.perplexity} />
            )}
          </div>
          {/* Right header */}
          <div className="flex items-center gap-2 px-3 py-1.5 bg-cyan-950/20 rounded-t border border-cyan-900/30">
            <Badge variant="outline" className="text-cyan-400 border-cyan-700 text-[10px]">
              Branch #{rightIdx + 1}
            </Badge>
            <span className="text-xs font-mono text-cyan-400">
              forced: &quot;{right.token}&quot;
            </span>
            {right.exploration.perplexity != null && (
              <PerplexityBadge perplexity={right.exploration.perplexity} />
            )}
          </div>

          {/* Token rows */}
          {Array.from({ length: maxLen }).map((_, i) => {
            const lToken = left.exploration.tokens[i]
            const rToken = right.exploration.tokens[i]
            const same = lToken?.token === rToken?.token
            const isForced = i === 0

            return (
              <DiffRow
                key={i}
                position={i}
                leftToken={lToken}
                rightToken={rToken}
                same={same}
                isForced={isForced}
              />
            )
          })}
        </div>
      </ScrollArea>
    </div>
  )
}

function DiffRow({
  position,
  leftToken,
  rightToken,
  same,
  isForced,
}: {
  position: number
  leftToken?: BranchToken
  rightToken?: BranchToken
  same: boolean
  isForced: boolean
}) {
  return (
    <>
      <TokenCell token={leftToken} position={position} same={same} isForced={isForced} side="left" />
      <TokenCell token={rightToken} position={position} same={same} isForced={isForced} side="right" />
    </>
  )
}

function TokenCell({
  token,
  position,
  same,
  isForced,
  side,
}: {
  token?: BranchToken
  position: number
  same: boolean
  isForced: boolean
  side: 'left' | 'right'
}) {
  if (!token) {
    return (
      <div className="px-3 py-1 border-b border-zinc-800/50 text-zinc-700 text-xs">
        &mdash;
      </div>
    )
  }

  const bgHighlight = same
    ? 'bg-zinc-900/30'
    : side === 'left'
      ? 'bg-violet-950/15'
      : 'bg-cyan-950/15'

  return (
    <div className={cn(
      'flex items-center gap-2 px-3 py-1 border-b border-zinc-800/50 text-xs font-mono',
      bgHighlight
    )}>
      <span className="text-zinc-600 w-5 text-right tabular-nums">{position}</span>
      <span
        className={cn(
          'px-1 rounded-sm',
          getTokenBgColor(token.logprob),
          getTokenColor(token.logprob),
          isForced && 'underline decoration-dotted decoration-violet-500 underline-offset-2'
        )}
      >
        {token.token.replace(/ /g, '\u00B7').replace(/\n/g, '\\n')}
      </span>
      <span className="text-zinc-600 tabular-nums ml-auto">
        {(token.probability * 100).toFixed(1)}%
      </span>
      {!same && (
        <span className={cn(
          'text-[9px] font-sans',
          side === 'left' ? 'text-violet-500' : 'text-cyan-500'
        )}>
          diff
        </span>
      )}
    </div>
  )
}

function BranchSelector({
  branches,
  selectedIdx,
  onChange,
  label,
  color,
}: {
  branches: BranchEntry[]
  selectedIdx: number
  onChange: (idx: number) => void
  label: string
  color: 'violet' | 'cyan'
}) {
  const colorClass = color === 'violet' ? 'text-violet-400' : 'text-cyan-400'

  return (
    <div className="flex-1">
      <label className={cn('text-[10px] uppercase tracking-wider mb-1 block', colorClass)}>
        {label}
      </label>
      <div className="relative">
        <select
          value={selectedIdx}
          onChange={(e) => onChange(Number(e.target.value))}
          className="w-full bg-zinc-900 border border-zinc-700 rounded px-3 py-1.5 text-sm text-zinc-300 appearance-none pr-8"
        >
          {branches.map((b, i) => (
            <option key={i} value={i}>
              Branch #{i + 1}: &quot;{b.token}&quot;
              {b.exploration.perplexity != null ? ` (PPL ${b.exploration.perplexity.toFixed(2)})` : ''}
            </option>
          ))}
        </select>
        <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-zinc-500 pointer-events-none" />
      </div>
    </div>
  )
}
