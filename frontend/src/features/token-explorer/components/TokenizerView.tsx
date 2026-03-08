import { useState } from 'react'
import { Hash, Loader2, DollarSign, Undo2 } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import {
  Tooltip,
  TooltipTrigger,
  TooltipContent,
} from '@/components/ui/tooltip'
import { useTokenExplorerStore } from '../store'
import { useTokenize, useDetokenize } from '../api'
import type { TokenizeResult } from '../types'
import type { DetokenizeResult } from '../api'

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

function formatCost(tokenCount: number, pricePerMillion: number): string {
  const cost = (tokenCount / 1_000_000) * pricePerMillion
  if (cost < 0.0001) return '< $0.0001'
  return `$${cost.toFixed(4)}`
}

export function TokenizerView({ embedded = false }: { embedded?: boolean }) {
  const store = useTokenExplorerStore()
  const tokenizeMutation = useTokenize()
  const detokenizeMutation = useDetokenize()

  const [text, setText] = useState(store.prompt)
  const [result, setResult] = useState<TokenizeResult | null>(null)
  const [inputPrice, setInputPrice] = useState(0.15)
  const [outputPrice, setOutputPrice] = useState(0.60)
  const [tokenIdsInput, setTokenIdsInput] = useState('')
  const [detokenizeResult, setDetokenizeResult] = useState<DetokenizeResult | null>(null)

  function handleTokenize() {
    if (!store.instanceId || !text.trim()) return

    tokenizeMutation.mutate(
      { instanceId: store.instanceId, text },
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
      handleTokenize()
    }
  }

  function handleDetokenize() {
    if (!store.instanceId || !tokenIdsInput.trim()) return

    const ids = tokenIdsInput
      .split(/[\s,]+/)
      .map((s) => s.trim())
      .filter(Boolean)
      .map(Number)
      .filter((n) => !isNaN(n))

    if (ids.length === 0) return

    detokenizeMutation.mutate(
      { instanceId: store.instanceId, tokenIds: ids },
      {
        onSuccess: (data) => setDetokenizeResult(data),
      }
    )
  }

  const canTokenize =
    !!store.instanceId && text.trim().length > 0 && !tokenizeMutation.isPending

  const canDetokenize =
    !!store.instanceId && tokenIdsInput.trim().length > 0 && !detokenizeMutation.isPending

  const charsPerToken =
    result && result.tokenCount > 0
      ? (result.characterCount / result.tokenCount).toFixed(2)
      : null

  const content = (
    <div className={cn('space-y-4', !embedded && 'p-4')}>
        {/* Text input */}
        <div className="space-y-2">
          <label className="text-xs font-medium text-zinc-400">
            Text to tokenize
          </label>
          <Textarea
            value={text}
            onChange={(e) => setText(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Enter text to tokenize..."
            className="min-h-[100px] resize-y bg-zinc-800 text-sm"
          />
          <div className="flex items-center gap-2">
            <Button
              onClick={handleTokenize}
              disabled={!canTokenize}
              className="gap-2 bg-violet-600 hover:bg-violet-700"
              size="sm"
            >
              {tokenizeMutation.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Hash className="h-4 w-4" />
              )}
              Tokenize
            </Button>
            <p className="text-xs text-zinc-600">Ctrl+Enter</p>
          </div>
        </div>

        {tokenizeMutation.isError && (
          <div className="rounded-md border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">
            {tokenizeMutation.error?.message ?? 'Tokenization failed'}
          </div>
        )}

        {/* Token blocks */}
        {result && (
          <>
            {/* Stats bar */}
            <div className="flex flex-wrap items-center gap-3">
              <Badge variant="secondary" className="bg-zinc-800 text-zinc-300">
                {result.tokenCount} tokens
              </Badge>
              <Badge variant="secondary" className="bg-zinc-800 text-zinc-300">
                {result.characterCount} characters
              </Badge>
              <Badge variant="secondary" className="bg-zinc-800 text-zinc-300">
                {result.byteCount} bytes
              </Badge>
              {charsPerToken && (
                <Badge
                  variant="secondary"
                  className="bg-zinc-800 text-zinc-300"
                >
                  ratio: {charsPerToken} chars/token
                </Badge>
              )}
              <Badge variant="outline" className="text-zinc-500">
                {result.modelId}
              </Badge>
            </div>

            <Separator />

            {/* Rendered tokens */}
            <div className="space-y-2">
              <label className="text-xs font-medium text-zinc-400">
                Tokenized output
              </label>
              <div className="flex flex-wrap gap-0.5 rounded-md border border-zinc-800 bg-zinc-950 p-3">
                {result.tokens.map((token, index) => (
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
                    <TooltipContent side="bottom" className="max-w-xs">
                      <div className="space-y-1 font-mono text-xs">
                        <div>
                          <span className="text-zinc-400">ID: </span>
                          <span className="text-zinc-200">{token.id}</span>
                        </div>
                        <div>
                          <span className="text-zinc-400">Bytes: </span>
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
                          <span className="text-zinc-400">Unicode: </span>
                          <span className="text-zinc-200">
                            {unicodeCodepoints(token.text)}
                          </span>
                        </div>
                      </div>
                    </TooltipContent>
                  </Tooltip>
                ))}
              </div>
            </div>

            <Separator />

            {/* Token cost estimator */}
            <Card className="border-zinc-800 bg-zinc-900">
              <CardHeader className="pb-3 pt-4 px-4">
                <CardTitle className="flex items-center gap-2 text-sm font-medium text-zinc-300">
                  <DollarSign className="h-4 w-4 text-emerald-500" />
                  Token Cost Estimator
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-3 px-4 pb-4">
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <label className="text-xs text-zinc-500">
                      Input price ($/1M tokens)
                    </label>
                    <Input
                      type="number"
                      step="0.01"
                      min="0"
                      value={inputPrice}
                      onChange={(e) =>
                        setInputPrice(Number(e.target.value))
                      }
                      className="h-8 bg-zinc-800 text-sm"
                    />
                  </div>
                  <div className="space-y-1">
                    <label className="text-xs text-zinc-500">
                      Output price ($/1M tokens)
                    </label>
                    <Input
                      type="number"
                      step="0.01"
                      min="0"
                      value={outputPrice}
                      onChange={(e) =>
                        setOutputPrice(Number(e.target.value))
                      }
                      className="h-8 bg-zinc-800 text-sm"
                    />
                  </div>
                </div>

                <div className="rounded-md border border-zinc-800 bg-zinc-950 p-3">
                  <div className="grid grid-cols-3 gap-4 text-center">
                    <div>
                      <p className="text-xs text-zinc-500">As input</p>
                      <p className="font-mono text-sm font-medium text-emerald-400">
                        {formatCost(result.tokenCount, inputPrice)}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-zinc-500">As output</p>
                      <p className="font-mono text-sm font-medium text-emerald-400">
                        {formatCost(result.tokenCount, outputPrice)}
                      </p>
                    </div>
                    <div>
                      <p className="text-xs text-zinc-500">Tokens</p>
                      <p className="font-mono text-sm font-medium text-zinc-300">
                        {result.tokenCount.toLocaleString()}
                      </p>
                    </div>
                  </div>
                </div>

                <p className="text-xs text-zinc-600">
                  Common rates: GPT-4o $2.50/$10, Claude Sonnet $3/$15,
                  Llama 3 (local) $0/$0
                </p>
              </CardContent>
            </Card>
          </>
        )}

        <Separator />

        {/* Detokenize section */}
        <div className="space-y-2">
          <label className="text-xs font-medium text-zinc-400">
            Detokenize — Token IDs to text
          </label>
          <Textarea
            value={tokenIdsInput}
            onChange={(e) => setTokenIdsInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
                e.preventDefault()
                handleDetokenize()
              }
            }}
            placeholder="Enter token IDs separated by spaces or commas (e.g., 1234 5678 91011)..."
            className="min-h-[60px] resize-y bg-zinc-800 font-mono text-sm"
            rows={2}
          />
          <div className="flex items-center gap-2">
            <Button
              onClick={handleDetokenize}
              disabled={!canDetokenize}
              className="gap-2 bg-violet-600 hover:bg-violet-700"
              size="sm"
            >
              {detokenizeMutation.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Undo2 className="h-4 w-4" />
              )}
              Detokenize
            </Button>
            {result && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => {
                  const ids = result.tokens.map((t) => t.id).join(' ')
                  setTokenIdsInput(ids)
                }}
                className="gap-1.5 text-xs"
              >
                Use IDs from above
              </Button>
            )}
          </div>
        </div>

        {detokenizeMutation.isError && (
          <div className="rounded-md border border-red-800 bg-red-950/50 px-3 py-2 text-sm text-red-300">
            {detokenizeMutation.error?.message ?? 'Detokenization failed'}
          </div>
        )}

        {detokenizeResult && (
          <div className="space-y-2">
            <div className="flex items-center gap-3">
              <Badge variant="secondary" className="bg-zinc-800 text-zinc-300">
                {detokenizeResult.tokenIds.length} token IDs
              </Badge>
              <Badge variant="outline" className="text-zinc-500">
                {detokenizeResult.modelId}
              </Badge>
            </div>
            <div className="rounded-md border border-zinc-800 bg-zinc-950 p-3">
              <p className="whitespace-pre-wrap font-mono text-sm text-zinc-200">
                {detokenizeResult.text}
              </p>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!result && !tokenizeMutation.isPending && !detokenizeResult && (
          <div className="flex h-40 flex-col items-center justify-center gap-2 text-zinc-500">
            <Hash className="h-8 w-8 text-zinc-600" />
            <p className="text-sm">No tokenization yet.</p>
            <p className="text-xs text-zinc-600">
              Enter text above and click Tokenize to see token boundaries.
            </p>
          </div>
        )}
    </div>
  )

  if (embedded) return <ScrollArea className="h-full">{content}</ScrollArea>

  return <ScrollArea className="h-full">{content}</ScrollArea>
}
