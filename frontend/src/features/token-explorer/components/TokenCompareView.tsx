import { useState } from 'react'
import { GitCompareArrows, Loader2, AlertCircle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Badge } from '@/components/ui/badge'
import {
  Tooltip,
  TooltipTrigger,
  TooltipContent,
} from '@/components/ui/tooltip'
import { useInstances } from '@/features/playground/api'
import { useCompareTokenize } from '../api'
import type { CompareTokenizeResult } from '../types'

const TOKEN_COLORS = [
  'bg-violet-500/20 text-violet-200',
  'bg-emerald-500/20 text-emerald-200',
] as const

function unicodeCodepoints(text: string): string {
  return [...text]
    .map((char) => {
      const cp = char.codePointAt(0)
      if (cp === undefined) return ''
      return cp > 0xffff
        ? `U+${cp.toString(16).toUpperCase().padStart(6, '0')}`
        : `U+${cp.toString(16).toUpperCase().padStart(4, '0')}`
    })
    .join(' ')
}

export function TokenCompareView({ embedded = false }: { embedded?: boolean }) {
  const instances = useInstances()
  const compareMutation = useCompareTokenize()

  const [text, setText] = useState('')
  const [selectedIds, setSelectedIds] = useState<string[]>([])
  const [result, setResult] = useState<CompareTokenizeResult | null>(null)

  function toggleInstance(id: string) {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
    )
  }

  function handleCompare() {
    if (selectedIds.length < 2 || !text.trim()) return

    compareMutation.mutate(
      { instanceIds: selectedIds, text },
      {
        onSuccess: (data) => {
          setResult(data)
        },
      }
    )
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault()
      handleCompare()
    }
  }

  const canCompare =
    selectedIds.length >= 2 &&
    text.trim().length > 0 &&
    !compareMutation.isPending

  // Find the min token count for highlighting differences
  const tokenCounts =
    result?.results
      .filter((r) => r.tokenization !== null)
      .map((r) => r.tokenization!.tokenCount) ?? []
  const minTokenCount = tokenCounts.length > 0 ? Math.min(...tokenCounts) : 0

  const content = (
    <div className={cn('space-y-4', !embedded && 'p-4')}>
        {/* Instance selector */}
        <div className="space-y-2">
          <label className="text-xs font-medium text-zinc-400">
            Select instances to compare (at least 2)
          </label>
          <div className="flex flex-wrap gap-2">
            {instances.data?.map((instance) => {
              const isSelected = selectedIds.includes(instance.id)
              return (
                <button
                  key={instance.id}
                  type="button"
                  onClick={() => toggleInstance(instance.id)}
                  className={cn(
                    'rounded-md border px-3 py-1.5 text-xs font-medium transition-colors',
                    isSelected
                      ? 'border-violet-500 bg-violet-500/20 text-violet-300'
                      : 'border-zinc-700 bg-zinc-800 text-zinc-400 hover:border-zinc-600 hover:text-zinc-300'
                  )}
                >
                  {instance.name}
                  {instance.modelId ? ` (${instance.modelId})` : ''}
                </button>
              )
            })}
          </div>
          {selectedIds.length > 0 && selectedIds.length < 2 && (
            <p className="text-xs text-amber-500">
              Select at least one more instance.
            </p>
          )}
        </div>

        {/* Text input */}
        <div className="space-y-2">
          <label className="text-xs font-medium text-zinc-400">
            Text to compare
          </label>
          <Textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter text to compare tokenization across models..."
            className="min-h-[80px] resize-y bg-zinc-800 text-sm"
          />
          <div className="flex items-center gap-2">
            <Button
              onClick={handleCompare}
              disabled={!canCompare}
              className="gap-2 bg-violet-600 hover:bg-violet-700"
              size="sm"
            >
              {compareMutation.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <GitCompareArrows className="h-4 w-4" />
              )}
              Compare
            </Button>
            <p className="text-xs text-zinc-600">Ctrl+Enter</p>
          </div>
        </div>

        {compareMutation.isError && (
          <div className="rounded-md border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">
            {compareMutation.error?.message ?? 'Comparison failed'}
          </div>
        )}

        {/* Comparison results */}
        {result && (
          <>
            <Separator />

            {/* Stats comparison table */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-zinc-400">
                Comparison summary
              </label>
              <div className="overflow-hidden rounded-md border border-zinc-800">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-zinc-800 bg-zinc-900">
                      <th className="px-3 py-2 text-left text-xs font-medium text-zinc-400">
                        Instance
                      </th>
                      <th className="px-3 py-2 text-left text-xs font-medium text-zinc-400">
                        Model
                      </th>
                      <th className="px-3 py-2 text-right text-xs font-medium text-zinc-400">
                        Tokens
                      </th>
                      <th className="px-3 py-2 text-right text-xs font-medium text-zinc-400">
                        Chars/Token
                      </th>
                      <th className="px-3 py-2 text-right text-xs font-medium text-zinc-400">
                        Bytes
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {result.results.map((entry) => {
                      const tok = entry.tokenization
                      const isMin =
                        tok !== null && tok.tokenCount === minTokenCount
                      return (
                        <tr
                          key={entry.instanceId}
                          className="border-b border-zinc-800/50 last:border-b-0"
                        >
                          <td className="px-3 py-2 text-zinc-200">
                            {entry.instanceName}
                          </td>
                          <td className="px-3 py-2 font-mono text-xs text-zinc-400">
                            {entry.modelId}
                          </td>
                          {tok ? (
                            <>
                              <td
                                className={cn(
                                  'px-3 py-2 text-right font-mono',
                                  isMin
                                    ? 'font-medium text-emerald-400'
                                    : 'text-zinc-300'
                                )}
                              >
                                {tok.tokenCount}
                                {isMin && tokenCounts.length > 1 && (
                                  <span className="ml-1 text-xs text-emerald-500">
                                    (fewest)
                                  </span>
                                )}
                              </td>
                              <td className="px-3 py-2 text-right font-mono text-zinc-300">
                                {(
                                  tok.characterCount / tok.tokenCount
                                ).toFixed(2)}
                              </td>
                              <td className="px-3 py-2 text-right font-mono text-zinc-300">
                                {tok.byteCount}
                              </td>
                            </>
                          ) : (
                            <td
                              colSpan={3}
                              className="px-3 py-2 text-right text-red-400"
                            >
                              <span className="inline-flex items-center gap-1">
                                <AlertCircle className="h-3 w-3" />
                                {entry.error ?? 'Failed'}
                              </span>
                            </td>
                          )}
                        </tr>
                      )
                    })}
                  </tbody>
                </table>
              </div>
            </div>

            <Separator />

            {/* Side-by-side token visualization */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-zinc-400">
                Token boundaries by model
              </label>
              <div className="grid gap-3">
                {result.results.map((entry) => (
                  <div
                    key={entry.instanceId}
                    className="rounded-md border border-zinc-800 bg-zinc-950 p-3"
                  >
                    <div className="mb-2 flex items-center gap-2">
                      <span className="text-xs font-medium text-zinc-300">
                        {entry.instanceName}
                      </span>
                      {entry.tokenization && (
                        <Badge
                          variant="secondary"
                          className="bg-zinc-800 text-zinc-400"
                        >
                          {entry.tokenization.tokenCount} tokens
                        </Badge>
                      )}
                    </div>
                    {entry.tokenization ? (
                      <div className="flex flex-wrap gap-0.5">
                        {entry.tokenization.tokens.map((token, index) => (
                          <Tooltip key={index}>
                            <TooltipTrigger>
                              <span
                                className={cn(
                                  'inline-block cursor-default rounded px-1 py-0.5 font-mono text-sm leading-relaxed',
                                  TOKEN_COLORS[index % 2]
                                )}
                              >
                                {token.displayText}
                              </span>
                            </TooltipTrigger>
                            <TooltipContent
                              side="bottom"
                              className="max-w-xs"
                            >
                              <div className="space-y-1 font-mono text-xs">
                                <div>
                                  <span className="text-zinc-400">ID: </span>
                                  <span className="text-zinc-200">
                                    {token.id}
                                  </span>
                                </div>
                                <div>
                                  <span className="text-zinc-400">
                                    Bytes:{' '}
                                  </span>
                                  <span className="text-zinc-200">
                                    {token.byteLength}
                                  </span>
                                </div>
                                <div>
                                  <span className="text-zinc-400">Hex: </span>
                                  <span className="text-zinc-200">
                                    {token.hexBytes}
                                  </span>
                                </div>
                                <div>
                                  <span className="text-zinc-400">
                                    Unicode:{' '}
                                  </span>
                                  <span className="text-zinc-200">
                                    {unicodeCodepoints(token.text)}
                                  </span>
                                </div>
                              </div>
                            </TooltipContent>
                          </Tooltip>
                        ))}
                      </div>
                    ) : (
                      <p className="text-xs text-red-400">
                        <AlertCircle className="mr-1 inline-block h-3 w-3" />
                        {entry.error ?? 'Tokenization failed'}
                      </p>
                    )}
                  </div>
                ))}
              </div>
            </div>
          </>
        )}

        {/* Empty state */}
        {!result && !compareMutation.isPending && (
          <div className="flex h-40 flex-col items-center justify-center gap-2 text-zinc-500">
            <GitCompareArrows className="h-8 w-8 text-zinc-600" />
            <p className="text-sm">No comparison yet.</p>
            <p className="text-xs text-zinc-600">
              Select 2+ instances, enter text, and click Compare.
            </p>
          </div>
        )}
    </div>
  )

  if (embedded) return <ScrollArea className="h-full">{content}</ScrollArea>

  return <ScrollArea className="h-full">{content}</ScrollArea>
}
