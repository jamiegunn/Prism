import { useState } from 'react'
import { Eye, BarChart3, AlertTriangle, Activity } from 'lucide-react'
import { cn } from '@/lib/utils'
import { getTokenBgColor, getTokenColor, calculateEntropy, isSurpriseToken } from '@/lib/logprobs'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import type { Message } from '../types'
import type { TokenLogprob } from '@/services/types/logprobs'
import { LogprobsPanel } from './LogprobsPanel'

interface ChatMessageProps {
  message: Message
  onSelectForLogprobs?: (message: Message) => void
}

/** Strip <think>/<thinking> blocks and clean up excessive newlines */
function cleanContent(raw: string): string {
  // Remove <think>...</think> and <thinking>...</thinking> blocks (including multiline)
  let cleaned = raw.replace(/<think(?:ing)?>[\s\S]*?<\/think(?:ing)?>/gi, '')
  // Remove standalone unclosed/closing tags
  cleaned = cleaned.replace(/<\/?think(?:ing)?>/gi, '')
  // Collapse 3+ newlines into 2
  cleaned = cleaned.replace(/\n{3,}/g, '\n\n')
  // Trim leading/trailing whitespace
  return cleaned.trim()
}

type HeatmapMode = 'off' | 'heatmap' | 'entropy' | 'surprise'

export function ChatMessage({ message, onSelectForLogprobs }: ChatMessageProps) {
  const [showLogprobs, setShowLogprobs] = useState(false)
  const [heatmapMode, setHeatmapMode] = useState<HeatmapMode>('off')

  if (message.role === 'System') {
    return (
      <div className="flex justify-center py-2">
        <div className="max-w-lg rounded-md bg-zinc-800/50 px-4 py-2 text-center">
          <p className="text-xs font-medium text-zinc-500 mb-1">System</p>
          <p className="text-sm italic text-zinc-400 whitespace-pre-wrap">{message.content}</p>
        </div>
      </div>
    )
  }

  const isUser = message.role === 'User'
  const hasLogprobs = !isUser && message.logprobsData && message.logprobsData.tokens.length > 0
  const displayContent = isUser ? message.content : cleanContent(message.content)

  return (
    <div className={cn('flex gap-3 py-3', isUser ? 'justify-end' : 'justify-start')}>
      <div
        className={cn(
          'max-w-[80%] rounded-lg px-4 py-3',
          isUser ? 'bg-violet-600/20 text-zinc-100' : 'bg-zinc-800 text-zinc-200'
        )}
      >
        {/* Message content: plain text or heatmap overlay */}
        {hasLogprobs && heatmapMode !== 'off' ? (
          <TokenHeatmapContent tokens={message.logprobsData!.tokens} mode={heatmapMode} />
        ) : (
          <p className="text-sm whitespace-pre-wrap break-words">{displayContent}</p>
        )}

        {/* Action buttons for assistant messages */}
        {hasLogprobs && (
          <div className="mt-2 flex flex-wrap items-center gap-1.5 border-t border-zinc-700/50 pt-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setHeatmapMode(heatmapMode === 'heatmap' ? 'off' : 'heatmap')}
              className={cn('h-5 gap-1 px-1.5 text-[10px]', heatmapMode === 'heatmap' ? 'text-amber-300' : 'text-amber-400 hover:text-amber-300')}
            >
              <BarChart3 className="h-3 w-3" />
              Heatmap
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setHeatmapMode(heatmapMode === 'entropy' ? 'off' : 'entropy')}
              className={cn('h-5 gap-1 px-1.5 text-[10px]', heatmapMode === 'entropy' ? 'text-cyan-300' : 'text-cyan-400 hover:text-cyan-300')}
            >
              <Activity className="h-3 w-3" />
              Entropy
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setHeatmapMode(heatmapMode === 'surprise' ? 'off' : 'surprise')}
              className={cn('h-5 gap-1 px-1.5 text-[10px]', heatmapMode === 'surprise' ? 'text-rose-300' : 'text-rose-400 hover:text-rose-300')}
            >
              <AlertTriangle className="h-3 w-3" />
              Surprises
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowLogprobs(!showLogprobs)}
              className="h-5 gap-1 px-1.5 text-[10px] text-violet-400 hover:text-violet-300"
            >
              <Eye className="h-3 w-3" />
              {showLogprobs ? 'Hide' : 'View'} Logprobs
            </Button>
            {onSelectForLogprobs && (
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onSelectForLogprobs(message)}
                className="h-5 gap-1 px-1.5 text-[10px] text-zinc-400 hover:text-zinc-200"
              >
                Open in Panel
              </Button>
            )}
          </div>
        )}

        {/* Inline logprobs analysis */}
        {showLogprobs && message.logprobsData && (
          <div className="mt-3 border-t border-zinc-700/50 pt-3">
            <LogprobsPanel
              logprobsData={message.logprobsData}
              perplexity={message.perplexity}
            />
          </div>
        )}
      </div>
    </div>
  )
}

interface TokenHeatmapContentProps {
  tokens: TokenLogprob[]
  mode: HeatmapMode
}

function getTokenEntropy(token: TokenLogprob): number {
  if (token.topLogprobs.length === 0) return 0
  const probs = token.topLogprobs.map((t) => t.probability)
  return calculateEntropy(probs)
}

function getEntropyColor(entropy: number): string {
  if (entropy < 0.5) return 'text-cyan-200'
  if (entropy < 1.0) return 'text-cyan-300'
  if (entropy < 2.0) return 'text-yellow-300'
  if (entropy < 3.0) return 'text-orange-300'
  return 'text-red-300'
}

function getEntropyBgColor(entropy: number): string {
  if (entropy < 0.5) return 'bg-cyan-400/10'
  if (entropy < 1.0) return 'bg-cyan-400/15'
  if (entropy < 2.0) return 'bg-yellow-400/15'
  if (entropy < 3.0) return 'bg-orange-400/15'
  return 'bg-red-400/20'
}

function getEntropyBarHeight(entropy: number, maxEntropy: number): number {
  if (maxEntropy === 0) return 0
  return Math.min(100, (entropy / maxEntropy) * 100)
}

function TokenHeatmapContent({ tokens, mode }: TokenHeatmapContentProps) {
  const maxEntropy = Math.max(...tokens.map(getTokenEntropy), 0.01)

  return (
    <div className="text-sm leading-relaxed">
      <div className="flex flex-wrap gap-0">
        {tokens.map((token, index) => {
          const entropy = getTokenEntropy(token)
          const isSurprise = isSurpriseToken(token.probability)

          let bgClass: string
          let textClass: string
          let showBar = false

          if (mode === 'entropy') {
            bgClass = getEntropyBgColor(entropy)
            textClass = getEntropyColor(entropy)
            showBar = true
          } else if (mode === 'surprise') {
            bgClass = isSurprise ? 'bg-rose-500/25' : 'bg-transparent'
            textClass = isSurprise ? 'text-rose-300 underline decoration-rose-500/50 decoration-wavy underline-offset-2' : 'text-zinc-400'
          } else {
            bgClass = getTokenBgColor(token.logprob)
            textClass = getTokenColor(token.logprob)
          }

          return (
            <Tooltip key={index}>
              <TooltipTrigger>
                <span className="relative inline-block">
                  <span
                    className={cn(
                      'rounded-sm px-0.5 py-0.5 font-mono text-sm cursor-pointer transition-all hover:opacity-80',
                      bgClass,
                      textClass,
                      showBar && 'pb-2'
                    )}
                  >
                    {token.token.replace(/ /g, '\u00B7').replace(/\n/g, '\u21B5\n')}
                  </span>
                  {showBar && (
                    <span
                      className="absolute bottom-0 left-0 right-0 h-1 rounded-b-sm transition-all"
                      style={{
                        background: entropy < 1 ? 'rgb(34 211 238 / 0.4)' : entropy < 2 ? 'rgb(250 204 21 / 0.5)' : entropy < 3 ? 'rgb(251 146 60 / 0.5)' : 'rgb(248 113 113 / 0.6)',
                        width: `${getEntropyBarHeight(entropy, maxEntropy)}%`,
                      }}
                    />
                  )}
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
                    <span className="text-zinc-400">Prob:</span>{' '}
                    <span className="font-mono">{(token.probability * 100).toFixed(2)}%</span>
                  </div>
                  <div>
                    <span className="text-zinc-400">Entropy:</span>{' '}
                    <span className="font-mono">{entropy.toFixed(3)} bits</span>
                  </div>
                  {isSurprise && (
                    <div className="text-rose-400 font-medium">Surprise token (prob &lt; 10%)</div>
                  )}
                  {token.topLogprobs.length > 0 && (
                    <div className="border-t border-zinc-700 pt-1 mt-1">
                      <span className="text-zinc-500">Alternatives:</span>
                      {token.topLogprobs.slice(0, 5).map((alt, i) => (
                        <div key={i} className="flex justify-between gap-3">
                          <span className="font-mono truncate">
                            {alt.token === token.token ? (
                              <span className="text-violet-400">{JSON.stringify(alt.token)}</span>
                            ) : (
                              JSON.stringify(alt.token)
                            )}
                          </span>
                          <span className="font-mono text-zinc-500">
                            {(alt.probability * 100).toFixed(1)}%
                          </span>
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              </TooltipContent>
            </Tooltip>
          )
        })}
      </div>
    </div>
  )
}
