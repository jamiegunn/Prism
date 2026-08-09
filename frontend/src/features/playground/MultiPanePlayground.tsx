import { useState, useCallback } from 'react'
import { Plus, Send, ArrowLeft } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { toast } from 'sonner'
import { useInstances } from '@/features/models/api'
import { PlaygroundPane } from './components/PlaygroundPane'
import { PaneComparisonSummary } from './components/PaneComparisonSummary'
import { SystemPromptEditor } from './components/SystemPromptEditor'
import type { Message } from './types'

interface PaneConfig {
  id: string
  instanceId: string
  instanceName: string
}

export function MultiPanePlayground() {
  const navigate = useNavigate()
  const { data: instances } = useInstances()
  const [panes, setPanes] = useState<PaneConfig[]>([])
  const [inputText, setInputText] = useState('')
  const [sharedInput, setSharedInput] = useState<string | null>(null)
  // Which panes have finished, not how many events arrived. A plain counter double-counts a
  // pane that is sent to twice and keeps counting one that has since been removed, so the
  // "2/3 completed" line drifts away from what is on screen.
  const [completedPaneIds, setCompletedPaneIds] = useState<Set<string>>(() => new Set())
  const [paneMessages, setPaneMessages] = useState<Record<string, Message[]>>({})

  const handleMessagesChange = useCallback((paneId: string, messages: Message[]) => {
    setPaneMessages((prev) => ({ ...prev, [paneId]: messages }))
  }, [])

  const handleAddPane = useCallback(
    (instanceId: string) => {
      const instance = instances?.find((i) => i.id === instanceId)
      if (!instance) return

      if (panes.length >= 4) {
        toast.error('Maximum 4 panes allowed')
        return
      }

      setPanes((prev) => [
        ...prev,
        {
          id: `pane-${Date.now()}`,
          instanceId: instance.id,
          instanceName: instance.name,
        },
      ])
    },
    [instances, panes]
  )

  const handleRemovePane = useCallback((paneId: string) => {
    setPanes((prev) => prev.filter((p) => p.id !== paneId))
    // Drop its numbers too, or a removed pane keeps competing in the comparison.
    setPaneMessages((prev) => {
      const { [paneId]: _removed, ...rest } = prev
      return rest
    })
    setCompletedPaneIds((prev) => {
      if (!prev.has(paneId)) return prev
      const next = new Set(prev)
      next.delete(paneId)
      return next
    })
  }, [])

  const handleSendAll = useCallback(() => {
    if (!inputText.trim()) return
    if (panes.length === 0) {
      toast.error('Add at least one instance pane')
      return
    }
    setCompletedPaneIds(new Set())
    setSharedInput(inputText.trim())
  }, [inputText, panes])

  const handleStreamDone = useCallback((paneId: string) => {
    setCompletedPaneIds((prev) => (prev.has(paneId) ? prev : new Set(prev).add(paneId)))
  }, [])

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSendAll()
    }
  }

  // Counted against the panes actually on screen, so the total and the tally can never
  // disagree about how many there are.
  const completedCount = panes.filter((pane) => completedPaneIds.has(pane.id)).length

  return (
    <div className="flex flex-col h-[calc(100vh-3.5rem)]">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-zinc-800 px-4 py-2">
        <div className="flex items-center gap-3">
          <Button variant="ghost" size="sm" onClick={() => navigate('/playground')}>
            <ArrowLeft className="h-4 w-4" />
          </Button>
          <h1 className="text-lg font-semibold text-zinc-100">Multi-Pane Comparison</h1>
        </div>

        <div className="flex items-center gap-2">
          <select
            onChange={(e) => {
              if (e.target.value) handleAddPane(e.target.value)
              e.target.value = ''
            }}
            className="bg-zinc-900 border border-border rounded px-2 py-1.5 text-sm text-zinc-300"
            defaultValue=""
          >
            <option value="" disabled>
              Add instance...
            </option>
            {instances?.map((inst) => (
              <option key={inst.id} value={inst.id}>
                {inst.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* System Prompt (shared) */}
      <SystemPromptEditor />

      {/* Panes Grid */}
      <div className="flex-1 min-h-0 overflow-auto p-4">
        {panes.length === 0 ? (
          <div className="flex flex-col items-center justify-center h-full text-zinc-500">
            <Plus className="h-12 w-12 mb-4 text-zinc-600" />
            <p className="text-lg font-medium text-zinc-400">No panes added</p>
            <p className="text-sm mt-1">
              Select instances from the dropdown to compare responses side-by-side.
            </p>
          </div>
        ) : (
          <div
            className="grid gap-4 h-full"
            style={{
              gridTemplateColumns: `repeat(${Math.min(panes.length, panes.length <= 2 ? panes.length : 2)}, 1fr)`,
              gridTemplateRows: panes.length > 2 ? 'repeat(2, 1fr)' : '1fr',
            }}
          >
            {panes.map((pane) => (
              <PlaygroundPane
                key={pane.id}
                paneId={pane.id}
                instanceId={pane.instanceId}
                instanceName={pane.instanceName}
                sharedInput={sharedInput}
                onRemove={() => handleRemovePane(pane.id)}
                onStreamDone={handleStreamDone}
                onMessagesChange={handleMessagesChange}
              />
            ))}
          </div>
        )}
      </div>

      {/* Head-to-head performance. Renders nothing until two panes have real numbers. */}
      <PaneComparisonSummary
        panes={panes.map((pane) => ({
          paneId: pane.id,
          label: pane.instanceName,
          messages: paneMessages[pane.id] ?? [],
        }))}
      />

      {/* Shared Input */}
      <div className="border-t border-zinc-800 px-4 py-3">
        <div className="flex gap-2">
          <Textarea
            value={inputText}
            onChange={(e) => setInputText(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="Type a message to send to all instances..."
            rows={2}
            className="resize-none"
          />
          <Button
            onClick={handleSendAll}
            disabled={!inputText.trim() || panes.length === 0}
            className="self-end gap-2"
          >
            <Send className="h-4 w-4" />
            Send All
          </Button>
        </div>
        {completedCount > 0 && completedCount < panes.length && (
          <p className="text-xs text-zinc-500 mt-1">
            {completedCount}/{panes.length} completed
          </p>
        )}
      </div>
    </div>
  )
}
