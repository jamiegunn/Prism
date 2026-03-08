import { useState, useRef, useCallback, useEffect } from 'react'
import { Send, Square } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'

interface ChatInputProps {
  onSend: (message: string) => void
  onStop: () => void
  isStreaming: boolean
  disabled: boolean
}

export function ChatInput({ onSend, onStop, isStreaming, disabled }: ChatInputProps) {
  const [input, setInput] = useState('')
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const adjustHeight = useCallback(() => {
    const textarea = textareaRef.current
    if (textarea) {
      textarea.style.height = 'auto'
      const newHeight = Math.min(textarea.scrollHeight, 200)
      textarea.style.height = `${newHeight}px`
    }
  }, [])

  useEffect(() => {
    adjustHeight()
  }, [input, adjustHeight])

  function handleSubmit() {
    const trimmed = input.trim()
    if (!trimmed || disabled) return
    onSend(trimmed)
    setInput('')
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      if (isStreaming) return
      handleSubmit()
    }
  }

  const charCount = input.length
  const estimatedTokens = Math.ceil(charCount / 4)

  return (
    <div className="border-t border-zinc-800 bg-zinc-950 p-4">
      <div className="flex items-end gap-2">
        <div className="relative flex-1">
          <textarea
            ref={textareaRef}
            value={input}
            onChange={(e) => setInput(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder={
              disabled ? 'Select an instance to start chatting...' : 'Type a message... (Shift+Enter for newline)'
            }
            disabled={disabled || isStreaming}
            rows={1}
            className={cn(
              'w-full resize-none rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2.5 pr-12 text-sm text-zinc-50',
              'placeholder:text-zinc-500 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500/50',
              'disabled:cursor-not-allowed disabled:opacity-50'
            )}
          />
          {input.length > 0 && (
            <div className="absolute bottom-1 right-12 text-[10px] text-zinc-600">
              ~{estimatedTokens} tokens
            </div>
          )}
        </div>

        {isStreaming ? (
          <Button
            variant="destructive"
            size="icon"
            onClick={onStop}
            className="h-10 w-10 shrink-0"
          >
            <Square className="h-4 w-4" />
          </Button>
        ) : (
          <Button
            size="icon"
            onClick={handleSubmit}
            disabled={!input.trim() || disabled}
            className="h-10 w-10 shrink-0"
          >
            <Send className="h-4 w-4" />
          </Button>
        )}
      </div>
    </div>
  )
}
