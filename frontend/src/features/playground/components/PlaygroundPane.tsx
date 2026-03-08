import { useCallback, useEffect, useRef } from 'react'
import { X, Loader2, Square } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { useStreamChat } from '../hooks/useStreamChat'
import { usePlaygroundStore } from '../store'
import type { SendMessageRequest, Message } from '../types'

interface PlaygroundPaneProps {
  paneId: string
  instanceId: string
  instanceName: string
  sharedInput: string | null
  onRemove: () => void
  onStreamDone: (paneId: string) => void
}

export function PlaygroundPane({
  paneId,
  instanceId,
  instanceName,
  sharedInput,
  onRemove,
  onStreamDone,
}: PlaygroundPaneProps) {
  const store = usePlaygroundStore()
  const stream = useStreamChat()

  const messages: Message[] = stream.completedConversation?.messages ?? []

  const handleSend = useCallback(
    async (content: string) => {
      const request: SendMessageRequest = {
        conversationId: stream.conversationId ?? undefined,
        instanceId,
        message: content,
        systemPrompt: store.systemPrompt || undefined,
        temperature: store.temperature,
        topP: store.topP,
        topK: store.topK,
        maxTokens: store.maxTokens,
        stopSequences: store.stopSequences.length > 0 ? store.stopSequences : undefined,
        frequencyPenalty: store.frequencyPenalty,
        presencePenalty: store.presencePenalty,
        logprobs: store.logprobs,
        topLogprobs: store.logprobs ? store.topLogprobs : undefined,
      }

      await stream.send(request)
      onStreamDone(paneId)
    },
    [instanceId, store, stream, paneId, onStreamDone]
  )

  // Trigger send when sharedInput changes
  const lastSentRef = useRef('')
  useEffect(() => {
    if (sharedInput && sharedInput !== lastSentRef.current && !stream.isStreaming) {
      lastSentRef.current = sharedInput
      handleSend(sharedInput)
    }
  }, [sharedInput]) // eslint-disable-line react-hooks/exhaustive-deps

  const lastAssistant = messages.filter((m) => m.role === 'Assistant').at(-1)
  const displayContent = stream.isStreaming
    ? stream.streamingContent
    : lastAssistant?.content ?? ''

  const stats = lastAssistant
    ? {
        tokens: lastAssistant.tokenCount,
        latency: lastAssistant.latencyMs,
        tps: lastAssistant.tokensPerSecond,
      }
    : null

  return (
    <div className="flex flex-col h-full rounded-lg border border-border bg-card">
      {/* Pane Header */}
      <div className="flex items-center justify-between border-b border-border px-3 py-2">
        <Badge variant="secondary" className="font-mono text-xs">
          {instanceName}
        </Badge>
        <div className="flex items-center gap-1">
          {stream.isStreaming && (
            <Button
              size="sm"
              variant="ghost"
              onClick={stream.stop}
              className="h-6 w-6 p-0 text-red-400"
            >
              <Square className="h-3 w-3" />
            </Button>
          )}
          <Button
            size="sm"
            variant="ghost"
            onClick={onRemove}
            className="h-6 w-6 p-0 text-zinc-500"
          >
            <X className="h-3 w-3" />
          </Button>
        </div>
      </div>

      {/* Pane Body */}
      <ScrollArea className="flex-1">
        <div className="p-3">
          {stream.isStreaming && !displayContent && (
            <div className="flex items-center gap-2 text-xs text-zinc-500">
              <Loader2 className="h-3 w-3 animate-spin" />
              Generating...
            </div>
          )}

          {displayContent && (
            <pre className="text-sm text-zinc-300 whitespace-pre-wrap font-sans">
              {displayContent}
            </pre>
          )}

          {stream.error && (
            <div className="rounded bg-red-950/30 border border-red-500/20 p-2 text-xs text-red-400 mt-2">
              {stream.error}
            </div>
          )}
        </div>
      </ScrollArea>

      {/* Pane Footer: Stats */}
      {stats && (
        <div className="flex gap-3 border-t border-border px-3 py-1.5 text-[10px] text-zinc-500">
          {stats.latency != null && <span>{stats.latency}ms</span>}
          {stats.tokens != null && <span>{stats.tokens} tokens</span>}
          {stats.tps != null && <span>{stats.tps.toFixed(1)} t/s</span>}
        </div>
      )}
    </div>
  )
}
