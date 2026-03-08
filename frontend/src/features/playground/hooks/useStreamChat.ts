import { useState, useCallback, useRef } from 'react'
import type { SendMessageRequest, Message, Conversation } from '../types'
import type { TokenLogprob } from '@/services/types/logprobs'

interface StreamState {
  isStreaming: boolean
  streamingContent: string
  streamingTokens: TokenLogprob[]
  conversationId: string | null
  messageId: string | null
  error: string | null
  completedMessage: Message | null
  completedConversation: Conversation | null
}

const initialState: StreamState = {
  isStreaming: false,
  streamingContent: '',
  streamingTokens: [],
  conversationId: null,
  messageId: null,
  error: null,
  completedMessage: null,
  completedConversation: null,
}

export function useStreamChat() {
  const [state, setState] = useState<StreamState>(initialState)
  const abortRef = useRef<AbortController | null>(null)

  const send = useCallback(async (request: SendMessageRequest) => {
    setState({
      ...initialState,
      isStreaming: true,
    })

    abortRef.current = new AbortController()

    try {
      const response = await fetch('/api/v1/playground/chat', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
        signal: abortRef.current.signal,
      })

      if (!response.ok) {
        const errorBody = await response
          .json()
          .catch(() => ({ detail: response.statusText })) as Record<string, unknown>
        setState((prev) => ({
          ...prev,
          isStreaming: false,
          error: (errorBody.detail as string) ?? (errorBody.title as string) ?? 'Request failed',
        }))
        return
      }

      const reader = response.body?.getReader()
      if (!reader) {
        setState((prev) => ({ ...prev, isStreaming: false, error: 'No response body' }))
        return
      }

      const decoder = new TextDecoder()
      let buffer = ''
      let currentEventType = ''

      while (true) {
        const { done, value } = await reader.read()
        if (done) break

        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''

        for (const line of lines) {
          if (line.startsWith('event: ')) {
            currentEventType = line.slice(7).trim()
          } else if (line.startsWith('data: ')) {
            const data = line.slice(6)
            try {
              const parsed = JSON.parse(data) as Record<string, unknown>
              switch (currentEventType) {
                case 'started':
                  setState((prev) => ({
                    ...prev,
                    conversationId: parsed.conversationId as string,
                    messageId: parsed.messageId as string,
                  }))
                  break
                case 'token': {
                  const logprobData = parsed.logprob as Record<string, unknown> | undefined
                  setState((prev) => ({
                    ...prev,
                    streamingContent: prev.streamingContent + (parsed.content as string),
                    streamingTokens: logprobData
                      ? [
                          ...prev.streamingTokens,
                          {
                            token: logprobData.token as string,
                            logprob: logprobData.logprob as number,
                            probability: logprobData.probability as number,
                            topLogprobs: (
                              (logprobData.topAlternatives as Array<Record<string, unknown>>) ?? []
                            ).map((alt) => ({
                              token: alt.token as string,
                              logprob: alt.logprob as number,
                              probability: alt.probability as number,
                            })),
                          },
                        ]
                      : prev.streamingTokens,
                  }))
                  break
                }
                case 'completed':
                  setState((prev) => ({
                    ...prev,
                    isStreaming: false,
                    completedMessage: parsed.message as Message,
                    completedConversation: parsed.conversation as Conversation,
                  }))
                  break
                case 'error':
                  setState((prev) => ({
                    ...prev,
                    isStreaming: false,
                    error: parsed.error as string,
                  }))
                  break
              }
            } catch {
              /* skip unparseable SSE data */
            }
          }
        }
      }

      setState((prev) => ({ ...prev, isStreaming: false }))
    } catch (err: unknown) {
      if (err instanceof DOMException && err.name === 'AbortError') {
        setState((prev) => ({ ...prev, isStreaming: false }))
      } else {
        setState((prev) => ({
          ...prev,
          isStreaming: false,
          error: err instanceof Error ? err.message : String(err),
        }))
      }
    }
  }, [])

  const stop = useCallback(() => {
    abortRef.current?.abort()
    setState((prev) => ({ ...prev, isStreaming: false }))
  }, [])

  const reset = useCallback(() => {
    setState(initialState)
  }, [])

  return { ...state, send, stop, reset }
}
