import { useState, useRef, useEffect } from 'react'
import {
  Library,
  Save,
  Trash2,
  Check,
  X,
  Pencil,
  ChevronDown,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { usePlaygroundStore } from '../store'
import { useSystemPromptLibrary } from '../systemPromptLibraryStore'

interface SystemPromptLibraryProps {
  className?: string
}

export function SystemPromptLibrary({ className }: SystemPromptLibraryProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [saveName, setSaveName] = useState('')
  const [isSaving, setIsSaving] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [editName, setEditName] = useState('')
  const dropdownRef = useRef<HTMLDivElement>(null)

  const systemPrompt = usePlaygroundStore((s) => s.systemPrompt)
  const setSystemPrompt = usePlaygroundStore((s) => s.setSystemPrompt)

  const library = useSystemPromptLibrary()

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false)
        setIsSaving(false)
        setEditingId(null)
      }
    }
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside)
      return () => document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [isOpen])

  function handleSave() {
    const name = saveName.trim()
    if (!name || !systemPrompt) return
    library.add(name, systemPrompt)
    setSaveName('')
    setIsSaving(false)
  }

  function handleSelect(content: string) {
    setSystemPrompt(content)
    setIsOpen(false)
  }

  function handleRename(id: string) {
    const name = editName.trim()
    if (!name) return
    library.rename(id, name)
    setEditingId(null)
  }

  function startEditing(id: string, currentName: string) {
    setEditingId(id)
    setEditName(currentName)
  }

  return (
    <div className={cn('relative', className)} ref={dropdownRef}>
      <Button
        variant="ghost"
        size="sm"
        onClick={() => {
          setIsOpen(!isOpen)
          setIsSaving(false)
          setEditingId(null)
        }}
        className="h-7 gap-1.5 text-xs text-zinc-500 hover:text-zinc-300"
      >
        <Library className="h-3 w-3" />
        Library
        <ChevronDown className={cn('h-3 w-3 transition-transform', isOpen && 'rotate-180')} />
      </Button>

      {isOpen && (
        <div className="absolute left-0 top-full z-50 mt-1 w-72 rounded-md border border-zinc-700 bg-zinc-900 shadow-lg">
          {/* Save current prompt */}
          {systemPrompt && !isSaving && (
            <button
              onClick={() => setIsSaving(true)}
              className="flex w-full items-center gap-2 border-b border-zinc-800 px-3 py-2 text-xs text-zinc-400 hover:bg-zinc-800 hover:text-zinc-200"
            >
              <Save className="h-3 w-3" />
              Save current prompt to library
            </button>
          )}

          {isSaving && (
            <div className="border-b border-zinc-800 p-2">
              <div className="flex items-center gap-1">
                <Input
                  value={saveName}
                  onChange={(e) => setSaveName(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter') handleSave()
                    if (e.key === 'Escape') setIsSaving(false)
                  }}
                  placeholder="Prompt name..."
                  className="h-7 text-xs"
                  autoFocus
                />
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleSave}
                  disabled={!saveName.trim()}
                  className="h-7 w-7 shrink-0 text-emerald-400 hover:text-emerald-300"
                >
                  <Check className="h-3 w-3" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => setIsSaving(false)}
                  className="h-7 w-7 shrink-0 text-zinc-500 hover:text-zinc-300"
                >
                  <X className="h-3 w-3" />
                </Button>
              </div>
            </div>
          )}

          {/* Prompt list */}
          <div className="max-h-64 overflow-y-auto">
            {library.prompts.length === 0 && (
              <div className="px-3 py-4 text-center text-xs text-zinc-600">
                No saved prompts yet.
                {systemPrompt ? ' Save the current one above.' : ' Set a system prompt first.'}
              </div>
            )}

            {library.prompts.map((entry) => (
              <div
                key={entry.id}
                className="group flex items-center gap-1 border-b border-zinc-800/50 px-2 py-1.5 last:border-b-0 hover:bg-zinc-800/50"
              >
                {editingId === entry.id ? (
                  <div className="flex flex-1 items-center gap-1">
                    <Input
                      value={editName}
                      onChange={(e) => setEditName(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter') handleRename(entry.id)
                        if (e.key === 'Escape') setEditingId(null)
                      }}
                      className="h-6 text-xs"
                      autoFocus
                    />
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => handleRename(entry.id)}
                      className="h-6 w-6 shrink-0 text-emerald-400"
                    >
                      <Check className="h-3 w-3" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      onClick={() => setEditingId(null)}
                      className="h-6 w-6 shrink-0 text-zinc-500"
                    >
                      <X className="h-3 w-3" />
                    </Button>
                  </div>
                ) : (
                  <>
                    <button
                      onClick={() => handleSelect(entry.content)}
                      className="flex-1 text-left"
                    >
                      <div className="text-xs font-medium text-zinc-300">
                        {entry.name}
                      </div>
                      <div className="truncate text-[10px] text-zinc-600">
                        {entry.content.slice(0, 80)}
                        {entry.content.length > 80 ? '...' : ''}
                      </div>
                    </button>
                    <div className="flex shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => startEditing(entry.id, entry.name)}
                        className="h-6 w-6 text-zinc-500 hover:text-zinc-300"
                      >
                        <Pencil className="h-3 w-3" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        onClick={() => library.remove(entry.id)}
                        className="h-6 w-6 text-zinc-500 hover:text-red-400"
                      >
                        <Trash2 className="h-3 w-3" />
                      </Button>
                    </div>
                  </>
                )}
              </div>
            ))}
          </div>

          {/* Clear prompt */}
          {systemPrompt && (
            <button
              onClick={() => {
                setSystemPrompt('')
                setIsOpen(false)
              }}
              className="flex w-full items-center gap-2 border-t border-zinc-800 px-3 py-2 text-xs text-zinc-500 hover:bg-zinc-800 hover:text-zinc-300"
            >
              <X className="h-3 w-3" />
              Clear system prompt
            </button>
          )}
        </div>
      )}
    </div>
  )
}
