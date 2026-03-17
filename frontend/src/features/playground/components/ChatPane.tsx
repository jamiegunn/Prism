import { useEffect, useRef } from 'react'
import { MessageSquare } from 'lucide-react'
import { cn } from '@/lib/utils'
import { ScrollArea } from '@/components/ui/scroll-area'
import { ChatMessage } from './ChatMessage'
import type { Message } from '../types'
import type { TokenLogprob } from '@/services/types/logprobs'

/** Strip <think>/<thinking> blocks and collapse excessive newlines from streaming content */
function cleanStreamingContent(raw: string): string {
  // Remove completed think blocks
  let cleaned = raw.replace(/<think(?:ing)?>[\s\S]*?<\/think(?:ing)?>/gi, '')
  // If there's an unclosed <think> or <thinking> tag, hide everything after it
  const openMatch = cleaned.match(/<think(?:ing)?>/i)
  if (openMatch && openMatch.index !== undefined) {
    cleaned = cleaned.substring(0, openMatch.index)
  }
  // Remove any stray closing tags
  cleaned = cleaned.replace(/<\/think(?:ing)?>/gi, '')
  cleaned = cleaned.replace(/\n{3,}/g, '\n\n')
  return cleaned.trim()
}

interface ChatPaneProps {
  messages: Message[]
  streamingContent: string
  streamingTokens: TokenLogprob[] // kept for future use
  isStreaming: boolean
  onSelectMessageForLogprobs?: (message: Message) => void
  onTokenClick?: (message: Message, tokenIndex: number) => void
  className?: string
}

export function ChatPane({
  messages,
  streamingContent,
  streamingTokens: _streamingTokens,
  isStreaming,
  onSelectMessageForLogprobs,
  onTokenClick,
  className,
}: ChatPaneProps) {
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [messages, streamingContent])

  if (messages.length === 0 && !isStreaming) {
    return (
      <div className={cn('flex flex-1 items-center justify-center', className)}>
        <div className="text-center">
          <MessageSquare className="mx-auto h-12 w-12 text-zinc-700 mb-4" />
          <h3 className="text-lg font-medium text-zinc-400">Start a conversation</h3>
          <p className="mt-1 text-sm text-zinc-600">
            Select a model and type a message to begin.
          </p>
        </div>
      </div>
    )
  }

  return (
    <ScrollArea className={cn('flex-1 px-4', className)}>
      <div className="mx-auto max-w-3xl py-4 space-y-1">
        {messages.map((message) => (
          <ChatMessage
            key={message.id}
            message={message}
            onSelectForLogprobs={onSelectMessageForLogprobs}
            onTokenClick={onTokenClick}
          />
        ))}

        {/* Streaming ghost message */}
        {isStreaming && streamingContent && (() => {
          const cleaned = cleanStreamingContent(streamingContent)
          if (!cleaned) return null // still inside <think> block, show nothing yet
          return (
            <div className="flex gap-3 py-3 justify-start">
              <div className="max-w-[80%] rounded-lg bg-zinc-800 px-4 py-3 text-zinc-200">
                <p className="text-sm whitespace-pre-wrap break-words">
                  {cleaned}
                  <span className="inline-block h-4 w-1.5 ml-0.5 bg-violet-500 animate-pulse" />
                </p>
              </div>
            </div>
          )
        })()}

        {/* Streaming indicator with no content yet */}
        {isStreaming && !streamingContent && (
          <div className="flex gap-3 py-3 justify-start">
            <div className="rounded-lg bg-zinc-800 px-4 py-3">
              <div className="flex items-center gap-1.5">
                <div className="h-2 w-2 rounded-full bg-violet-500 animate-pulse" />
                <div className="h-2 w-2 rounded-full bg-violet-500 animate-pulse [animation-delay:150ms]" />
                <div className="h-2 w-2 rounded-full bg-violet-500 animate-pulse [animation-delay:300ms]" />
              </div>
            </div>
          </div>
        )}

        <div ref={bottomRef} />
      </div>
    </ScrollArea>
  )
}
