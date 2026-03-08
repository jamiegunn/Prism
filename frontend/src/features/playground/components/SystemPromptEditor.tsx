import { useState } from 'react'
import { ChevronDown, ChevronRight, MessageSquareText } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Textarea } from '@/components/ui/textarea'
import { usePlaygroundStore } from '../store'
import { SystemPromptLibrary } from './SystemPromptLibrary'

interface SystemPromptEditorProps {
  className?: string
}

export function SystemPromptEditor({ className }: SystemPromptEditorProps) {
  const [isOpen, setIsOpen] = useState(false)
  const systemPrompt = usePlaygroundStore((s) => s.systemPrompt)
  const setSystemPrompt = usePlaygroundStore((s) => s.setSystemPrompt)

  return (
    <div className={cn('border-b border-zinc-800', className)}>
      <div className="flex items-center">
        <button
          onClick={() => setIsOpen(!isOpen)}
          className="flex flex-1 items-center gap-2 px-4 py-2 text-sm text-zinc-400 hover:text-zinc-200 transition-colors"
        >
          {isOpen ? (
            <ChevronDown className="h-3.5 w-3.5" />
          ) : (
            <ChevronRight className="h-3.5 w-3.5" />
          )}
          <MessageSquareText className="h-3.5 w-3.5" />
          <span>System Prompt</span>
          {systemPrompt && !isOpen && (
            <span className="ml-auto text-xs text-zinc-600 truncate max-w-48">
              {systemPrompt.slice(0, 60)}
              {systemPrompt.length > 60 ? '...' : ''}
            </span>
          )}
          {systemPrompt && (
            <span className="text-xs text-zinc-600">{systemPrompt.length} chars</span>
          )}
        </button>
        <div className="pr-3">
          <SystemPromptLibrary />
        </div>
      </div>

      {isOpen && (
        <div className="px-4 pb-3">
          <Textarea
            value={systemPrompt}
            onChange={(e) => setSystemPrompt(e.target.value)}
            placeholder="You are a helpful assistant..."
            className="min-h-[60px] max-h-32 resize-y text-xs"
            rows={3}
          />
        </div>
      )}
    </div>
  )
}
