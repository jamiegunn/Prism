import { useState, useCallback } from 'react'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { X, Plus } from 'lucide-react'
import { useTagRecord } from '../api'

interface TagEditorProps {
  recordId: string
  tags: string[]
  onTagsChange?: (tags: string[]) => void
}

export function TagEditor({ recordId, tags, onTagsChange }: TagEditorProps) {
  const [inputValue, setInputValue] = useState('')
  const [localTags, setLocalTags] = useState<string[]>(tags)
  const tagMutation = useTagRecord()

  const saveTags = useCallback(
    (newTags: string[]) => {
      setLocalTags(newTags)
      onTagsChange?.(newTags)
      tagMutation.mutate({ id: recordId, tags: newTags })
    },
    [recordId, tagMutation, onTagsChange]
  )

  const addTag = useCallback(() => {
    const trimmed = inputValue.trim().toLowerCase()
    if (!trimmed || localTags.includes(trimmed)) {
      setInputValue('')
      return
    }
    saveTags([...localTags, trimmed])
    setInputValue('')
  }, [inputValue, localTags, saveTags])

  const removeTag = useCallback(
    (tagToRemove: string) => {
      saveTags(localTags.filter((t) => t !== tagToRemove))
    },
    [localTags, saveTags]
  )

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') {
      e.preventDefault()
      addTag()
    }
  }

  return (
    <div className="space-y-2">
      <div className="flex flex-wrap gap-1.5">
        {localTags.map((tag) => (
          <Badge
            key={tag}
            className="bg-zinc-700 text-zinc-200 hover:bg-zinc-600 gap-1 pr-1 cursor-default"
          >
            {tag}
            <button
              onClick={() => removeTag(tag)}
              className="ml-0.5 rounded-full hover:bg-zinc-500 p-0.5"
            >
              <X className="h-3 w-3" />
            </button>
          </Badge>
        ))}
      </div>
      <div className="flex gap-2 items-center">
        <Input
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Add tag..."
          className="h-7 text-xs max-w-[160px]"
        />
        <button
          onClick={addTag}
          disabled={!inputValue.trim()}
          className="text-zinc-400 hover:text-zinc-200 disabled:opacity-30"
        >
          <Plus className="h-4 w-4" />
        </button>
      </div>
    </div>
  )
}
