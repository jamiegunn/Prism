import { useState, useCallback } from 'react'
import { useNavigate } from 'react-router-dom'
import { useQueryClient } from '@tanstack/react-query'
import {
  PanelLeftClose,
  PanelLeftOpen,
  PanelRightClose,
  PanelRightOpen,
  PanelBottomClose,
  PanelBottomOpen,
  Download,
  BarChart3,
  Columns3,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { useConversation } from './api'
import { useStreamChat } from './hooks/useStreamChat'
import { usePlaygroundStore } from './store'
import { ConversationHistory } from './components/ConversationHistory'
import { ChatPane } from './components/ChatPane'
import { ChatInput } from './components/ChatInput'
import { SystemPromptEditor } from './components/SystemPromptEditor'
import { ParameterSidebar } from './components/ParameterSidebar'
import { MessageStatsPanel } from './components/MessageStatsPanel'
import { LogprobsPanel } from './components/LogprobsPanel'
import type { SendMessageRequest, Message } from './types'

export function PlaygroundPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const store = usePlaygroundStore()
  const stream = useStreamChat()

  const [showLeftPanel, setShowLeftPanel] = useState(true)
  const [showRightPanel, setShowRightPanel] = useState(true)
  const [showStatsPanel, setShowStatsPanel] = useState(true)
  const [showBottomPanel, setShowBottomPanel] = useState(false)
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)
  const [logprobsMessage, setLogprobsMessage] = useState<Message | null>(null)

  const conversationQuery = useConversation(
    stream.completedConversation?.id ?? activeConversationId
  )

  const currentConversation = stream.completedConversation ?? conversationQuery.data ?? null
  const messages: Message[] = currentConversation?.messages ?? []

  const handleSendMessage = useCallback(
    async (content: string) => {
      if (!store.selectedInstanceId) return

      const request: SendMessageRequest = {
        conversationId: activeConversationId ?? stream.conversationId ?? undefined,
        instanceId: store.selectedInstanceId,
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

      await queryClient.invalidateQueries({ queryKey: ['playground', 'conversations'] })
    },
    [store, activeConversationId, stream, queryClient]
  )

  const handleSelectConversation = useCallback(
    (id: string) => {
      setActiveConversationId(id)
      setLogprobsMessage(null)
      stream.reset()
    },
    [stream]
  )

  const handleNewConversation = useCallback(() => {
    setActiveConversationId(null)
    setLogprobsMessage(null)
    stream.reset()
  }, [stream])

  const handleSelectMessageForLogprobs = useCallback((message: Message) => {
    setLogprobsMessage(message)
    setShowBottomPanel(true)
  }, [])

  const handleExport = useCallback(async () => {
    const convId = currentConversation?.id
    if (!convId) return

    try {
      const response = await fetch(
        `/api/v1/playground/conversations/${convId}/export?format=json`
      )
      if (!response.ok) return
      const blob = await response.blob()
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `conversation-${convId}.json`
      anchor.click()
      URL.revokeObjectURL(url)
    } catch {
      /* export failed silently */
    }
  }, [currentConversation])

  // Update active conversation ID when streaming produces one
  if (stream.completedConversation && !activeConversationId) {
    setActiveConversationId(stream.completedConversation.id)
  }

  // Auto-select last assistant message for logprobs panel when it completes
  const lastAssistantMessage = messages
    .filter((m) => m.role === 'Assistant' && m.logprobsData)
    .at(-1)

  return (
    <div className="flex h-[calc(100vh-3.5rem)] flex-col">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-zinc-800 px-4 py-2">
        <div className="flex items-center gap-3">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setShowLeftPanel(!showLeftPanel)}
            className="h-8 w-8"
          >
            {showLeftPanel ? (
              <PanelLeftClose className="h-4 w-4" />
            ) : (
              <PanelLeftOpen className="h-4 w-4" />
            )}
          </Button>
          <h1 className="text-lg font-semibold text-zinc-100">Inference Playground</h1>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => navigate('/playground/compare')}
            className="h-8 gap-1.5 text-xs text-zinc-400"
          >
            <Columns3 className="h-3.5 w-3.5" />
            Compare
          </Button>
        </div>

        <div className="flex items-center gap-2">
          {messages.length > 0 && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowStatsPanel(!showStatsPanel)}
              className={cn('h-8 gap-1.5 text-xs', showStatsPanel && 'text-emerald-400')}
            >
              <BarChart3 className="h-3.5 w-3.5" />
              Stats
            </Button>
          )}
          {lastAssistantMessage && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                setShowBottomPanel(!showBottomPanel)
                if (!logprobsMessage && lastAssistantMessage) {
                  setLogprobsMessage(lastAssistantMessage)
                }
              }}
              className="h-8 gap-1.5 text-xs"
            >
              {showBottomPanel ? (
                <PanelBottomClose className="h-3.5 w-3.5" />
              ) : (
                <PanelBottomOpen className="h-3.5 w-3.5" />
              )}
              Logprobs
            </Button>
          )}
          {currentConversation && (
            <Button
              variant="ghost"
              size="sm"
              onClick={handleExport}
              className="h-8 gap-1.5 text-xs"
            >
              <Download className="h-3.5 w-3.5" />
              Export
            </Button>
          )}
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setShowRightPanel(!showRightPanel)}
            className="h-8 w-8"
          >
            {showRightPanel ? (
              <PanelRightClose className="h-4 w-4" />
            ) : (
              <PanelRightOpen className="h-4 w-4" />
            )}
          </Button>
        </div>
      </div>

      {/* Main Layout */}
      <div className="flex flex-1 min-h-0">
        {/* Left Panel: Conversation History */}
        {showLeftPanel && (
          <div className="w-72 shrink-0 border-r border-zinc-800 bg-zinc-900/50">
            <ConversationHistory
              selectedId={activeConversationId}
              onSelect={handleSelectConversation}
              onNewConversation={handleNewConversation}
            />
          </div>
        )}

        {/* Center: Chat Area + LogprobsPanel */}
        <div className="flex flex-1 min-w-0 flex-col">
          <SystemPromptEditor />

          <ChatPane
            messages={messages}
            streamingContent={stream.streamingContent}
            streamingTokens={stream.streamingTokens}
            isStreaming={stream.isStreaming}
            onSelectMessageForLogprobs={handleSelectMessageForLogprobs}
            className="flex-1 min-h-0"
          />

          {/* Collapsible Bottom Logprobs Panel */}
          {showBottomPanel && logprobsMessage?.logprobsData && (
            <div className="border-t border-zinc-800 bg-zinc-900/70 max-h-64 overflow-y-auto overflow-x-hidden p-4">
              <div className="flex items-center justify-between mb-2">
                <span className="text-xs font-medium text-zinc-300">
                  Logprobs Analysis &mdash; Message #{logprobsMessage.sortOrder}
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setShowBottomPanel(false)}
                  className="h-6 px-2 text-xs text-zinc-500 hover:text-zinc-200"
                >
                  Close
                </Button>
              </div>
              <LogprobsPanel
                logprobsData={logprobsMessage.logprobsData}
                perplexity={logprobsMessage.perplexity}
              />
            </div>
          )}

          {stream.error && (
            <div className="border-t border-red-800/50 bg-red-950/30 px-4 py-2">
              <p className="text-xs text-red-400">{stream.error}</p>
            </div>
          )}

          <ChatInput
            onSend={handleSendMessage}
            onStop={stream.stop}
            isStreaming={stream.isStreaming}
            disabled={!store.selectedInstanceId}
          />
        </div>

        {/* Stats Panel (between chat and parameters) */}
        {showStatsPanel && messages.length > 0 && (
          <MessageStatsPanel messages={messages} className="w-56 shrink-0" />
        )}

        {/* Right Panel: Parameters */}
        {showRightPanel && (
          <div className="w-80 shrink-0 border-l border-zinc-800 bg-zinc-900/50">
            <ParameterSidebar />
          </div>
        )}
      </div>
    </div>
  )
}
