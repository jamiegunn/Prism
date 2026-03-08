import { useState } from 'react'
import { cn } from '@/lib/utils'
import { getTokenBgColor, getTokenColor } from '@/lib/logprobs'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import { GitBranch, Trash2, TreePine, List } from 'lucide-react'
import { useTokenExplorerStore } from '../store'
import type { BranchExploration } from '../types'

export function BranchTreeView() {
  const { branches, clearBranches, prompt, stepHistory } = useTokenExplorerStore()
  const [viewMode, setViewMode] = useState<'tree' | 'list'>('tree')

  if (branches.length === 0) {
    return (
      <div className="flex h-64 flex-col items-center justify-center gap-2 text-zinc-500">
        <GitBranch className="h-8 w-8 text-zinc-600" />
        <p className="text-sm">No branches explored yet.</p>
        <p className="text-xs text-zinc-600">
          Click a token in the Predictions tab to start a branch exploration.
        </p>
      </div>
    )
  }

  const greedyPath = stepHistory.map((s) => s.token).join('')

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <h4 className="text-sm font-medium text-zinc-300">
          Explored Branches ({branches.length})
        </h4>
        <div className="flex items-center gap-2">
          <div className="flex rounded-md border border-zinc-700">
            <button
              onClick={() => setViewMode('tree')}
              className={cn(
                'flex items-center gap-1 px-2 py-1 text-xs transition-colors',
                viewMode === 'tree'
                  ? 'bg-zinc-700 text-zinc-200'
                  : 'text-zinc-500 hover:text-zinc-300'
              )}
            >
              <TreePine className="h-3 w-3" />
              Tree
            </button>
            <button
              onClick={() => setViewMode('list')}
              className={cn(
                'flex items-center gap-1 px-2 py-1 text-xs transition-colors',
                viewMode === 'list'
                  ? 'bg-zinc-700 text-zinc-200'
                  : 'text-zinc-500 hover:text-zinc-300'
              )}
            >
              <List className="h-3 w-3" />
              List
            </button>
          </div>
          <Button
            variant="outline"
            size="sm"
            onClick={clearBranches}
            className="gap-1.5"
          >
            <Trash2 className="h-3.5 w-3.5" />
            Clear All
          </Button>
        </div>
      </div>

      <ScrollArea className="h-[calc(100vh-20rem)]">
        {viewMode === 'tree' ? (
          <GenerationTree
            prompt={prompt}
            greedyPath={greedyPath}
            branches={branches.map((b) => ({ token: b.token, exploration: b.exploration }))}
          />
        ) : (
          <div className="space-y-3 pr-2">
            {branches.map((branch, index) => (
              <BranchCard
                key={`${branch.token}-${index}`}
                token={branch.token}
                exploration={branch.exploration}
                index={index}
              />
            ))}
          </div>
        )}
      </ScrollArea>
    </div>
  )
}

interface GenerationTreeProps {
  prompt: string
  greedyPath: string
  branches: { token: string; exploration: BranchExploration }[]
}

function GenerationTree({ prompt, greedyPath, branches }: GenerationTreeProps) {
  const promptPreview = prompt.length > 40 ? '...' + prompt.slice(-40) : prompt

  return (
    <div className="space-y-1 pr-2 font-mono text-xs">
      {/* Root: prompt stem */}
      <div className="flex items-center gap-2 rounded-md border border-zinc-800 bg-zinc-900/80 px-3 py-2">
        <span className="text-zinc-500">Prompt:</span>
        <span className="truncate text-zinc-300">{promptPreview}</span>
      </div>

      {/* Greedy path (if step history exists) */}
      {greedyPath && (
        <div className="ml-6 border-l-2 border-emerald-700/50 pl-4">
          <div className="flex items-center gap-2 rounded-md border border-zinc-800 bg-zinc-900/50 px-3 py-1.5">
            <span className="text-emerald-500">greedy</span>
            <span className="text-zinc-300">{greedyPath}</span>
          </div>
        </div>
      )}

      {/* Branch paths */}
      {branches.map((branch, index) => {
        const continuation = branch.exploration.tokens
          .map((t) => t.token)
          .join('')
        const ppl = branch.exploration.perplexity

        return (
          <div
            key={`${branch.token}-${index}`}
            className="ml-6 border-l-2 border-violet-700/50 pl-4"
          >
            <div className="rounded-md border border-zinc-800 bg-zinc-900/50 px-3 py-1.5">
              <div className="flex items-center gap-2">
                <GitBranch className="h-3 w-3 shrink-0 text-violet-400" />
                <span className="text-violet-400">
                  &quot;{branch.token}&quot;
                </span>
                <span className="text-zinc-500">&rarr;</span>
                <span className="truncate text-zinc-400">
                  {continuation.slice(0, 60)}
                  {continuation.length > 60 ? '...' : ''}
                </span>
                {ppl !== null && (
                  <Badge
                    variant="outline"
                    className="ml-auto shrink-0 text-[10px]"
                  >
                    PPL {ppl.toFixed(2)}
                  </Badge>
                )}
              </div>
            </div>
          </div>
        )
      })}

      {/* Legend */}
      <div className="mt-4 flex items-center gap-4 text-[10px] text-zinc-600">
        {greedyPath && (
          <span className="flex items-center gap-1">
            <span className="inline-block h-2 w-2 rounded-full bg-emerald-600" />
            Greedy path (step-through)
          </span>
        )}
        <span className="flex items-center gap-1">
          <span className="inline-block h-2 w-2 rounded-full bg-violet-600" />
          Explored branches
        </span>
      </div>
    </div>
  )
}

interface BranchCardProps {
  token: string
  exploration: BranchExploration
  index: number
}

function BranchCard({ token, exploration, index }: BranchCardProps) {
  return (
    <Card className="border-zinc-800 bg-zinc-900/50">
      <CardHeader className="p-4 pb-2">
        <CardTitle className="flex items-center justify-between text-sm">
          <div className="flex items-center gap-2">
            <GitBranch className="h-4 w-4 text-violet-400" />
            <span className="text-zinc-300">Branch #{index + 1}</span>
            <span className="text-zinc-500">&mdash;</span>
            <span className="font-mono text-violet-400">
              Forced: &quot;{token}&quot;
            </span>
          </div>
          <div className="flex items-center gap-2">
            {exploration.perplexity !== null && (
              <Badge variant="outline" className="font-mono text-xs">
                PPL: {exploration.perplexity.toFixed(2)}
              </Badge>
            )}
            <Badge variant="secondary" className="font-mono text-xs">
              {exploration.tokens.length} tokens
            </Badge>
          </div>
        </CardTitle>
      </CardHeader>
      <CardContent className="p-4 pt-0">
        <div className="rounded-md border border-zinc-800 bg-zinc-950 p-3">
          <div className="flex flex-wrap font-mono text-sm leading-relaxed">
            {exploration.tokens.map((branchToken, tokenIdx) => (
              <Tooltip key={tokenIdx}>
                <TooltipTrigger>
                  <span
                    className={cn(
                      'inline rounded-sm px-0.5',
                      getTokenBgColor(branchToken.logprob),
                      getTokenColor(branchToken.logprob),
                      tokenIdx === 0 && 'underline decoration-violet-500 underline-offset-4'
                    )}
                  >
                    {branchToken.token}
                  </span>
                </TooltipTrigger>
                <TooltipContent side="bottom">
                  <div className="space-y-1 text-xs">
                    <div>
                      Token: <span className="font-mono">{JSON.stringify(branchToken.token)}</span>
                    </div>
                    <div>Probability: {(branchToken.probability * 100).toFixed(4)}%</div>
                    <div>Log-prob: {branchToken.logprob.toFixed(6)}</div>
                    {tokenIdx === 0 && (
                      <div className="text-violet-400">Forced token</div>
                    )}
                    {branchToken.topAlternatives.length > 0 && (
                      <div className="border-t border-zinc-700 pt-1 mt-1">
                        <div className="text-zinc-400 mb-0.5">Top alternatives:</div>
                        {branchToken.topAlternatives.slice(0, 5).map((alt, altIdx) => (
                          <div key={altIdx} className="flex justify-between gap-3">
                            <span className="font-mono">{alt.token}</span>
                            <span>{(alt.probability * 100).toFixed(2)}%</span>
                          </div>
                        ))}
                      </div>
                    )}
                  </div>
                </TooltipContent>
              </Tooltip>
            ))}
          </div>
        </div>

        {exploration.generatedText && (
          <div className="mt-2 text-xs text-zinc-500">
            Full text: {exploration.generatedText.slice(0, 200)}
            {exploration.generatedText.length > 200 && '...'}
          </div>
        )}
      </CardContent>
    </Card>
  )
}
