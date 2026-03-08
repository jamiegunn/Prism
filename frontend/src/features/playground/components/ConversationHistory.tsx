import { useState } from 'react'
import { Plus, Search, Pin, Trash2, MessageSquare } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { ScrollArea } from '@/components/ui/scroll-area'
import { truncate } from '@/lib/utils'
import { useConversations, useDeleteConversation } from '../api'
import type { ConversationSummary } from '../types'

interface ConversationHistoryProps {
  selectedId: string | null
  onSelect: (id: string) => void
  onNewConversation: () => void
  className?: string
}

export function ConversationHistory({
  selectedId,
  onSelect,
  onNewConversation,
  className,
}: ConversationHistoryProps) {
  const [search, setSearch] = useState('')
  const [deleteConfirmId, setDeleteConfirmId] = useState<string | null>(null)
  const conversations = useConversations({ search: search || undefined })
  const deleteConversation = useDeleteConversation()

  const items = conversations.data?.items ?? []

  const sortedItems = [...items].sort((a, b) => {
    if (a.isPinned && !b.isPinned) return -1
    if (!a.isPinned && b.isPinned) return 1
    const dateA = a.lastMessageAt ?? a.createdAt
    const dateB = b.lastMessageAt ?? b.createdAt
    return new Date(dateB).getTime() - new Date(dateA).getTime()
  })

  function handleDelete(id: string) {
    deleteConversation.mutate(id, {
      onSuccess: () => {
        setDeleteConfirmId(null)
        if (selectedId === id) {
          onNewConversation()
        }
      },
    })
  }

  function formatDate(dateStr: string): string {
    const date = new Date(dateStr)
    const now = new Date()
    const diffMs = now.getTime() - date.getTime()
    const diffDays = Math.floor(diffMs / 86400000)
    if (diffDays === 0) return 'Today'
    if (diffDays === 1) return 'Yesterday'
    if (diffDays < 7) return `${diffDays}d ago`
    return date.toLocaleDateString()
  }

  return (
    <div className={cn('flex h-full flex-col', className)}>
      <div className="space-y-2 p-3 border-b border-zinc-800">
        <Button
          variant="outline"
          size="sm"
          onClick={onNewConversation}
          className="w-full gap-2 text-xs"
        >
          <Plus className="h-3.5 w-3.5" />
          New Conversation
        </Button>
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-zinc-500" />
          <Input
            placeholder="Search..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="h-8 pl-8 text-xs"
          />
        </div>
      </div>

      <ScrollArea className="flex-1">
        <div className="space-y-0.5 p-2">
          {sortedItems.length === 0 && (
            <div className="flex flex-col items-center py-8 text-zinc-600">
              <MessageSquare className="h-8 w-8 mb-2" />
              <p className="text-xs">No conversations yet</p>
            </div>
          )}

          {sortedItems.map((item) => (
            <ConversationItem
              key={item.id}
              item={item}
              isSelected={item.id === selectedId}
              isDeleting={deleteConfirmId === item.id}
              onSelect={() => onSelect(item.id)}
              onDeleteClick={() =>
                setDeleteConfirmId(deleteConfirmId === item.id ? null : item.id)
              }
              onDeleteConfirm={() => handleDelete(item.id)}
              formatDate={formatDate}
            />
          ))}
        </div>
      </ScrollArea>
    </div>
  )
}

interface ConversationItemProps {
  item: ConversationSummary
  isSelected: boolean
  isDeleting: boolean
  onSelect: () => void
  onDeleteClick: () => void
  onDeleteConfirm: () => void
  formatDate: (dateStr: string) => string
}

function ConversationItem({
  item,
  isSelected,
  isDeleting,
  onSelect,
  onDeleteClick,
  onDeleteConfirm,
  formatDate,
}: ConversationItemProps) {
  return (
    <div
      className={cn(
        'group relative rounded-md px-3 py-2 cursor-pointer transition-colors',
        isSelected ? 'bg-zinc-800 text-zinc-100' : 'text-zinc-400 hover:bg-zinc-800/50 hover:text-zinc-200'
      )}
      onClick={onSelect}
    >
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-1.5">
            {item.isPinned && <Pin className="h-3 w-3 text-amber-500 shrink-0" />}
            <span className="text-xs font-medium truncate">{truncate(item.title, 32)}</span>
          </div>
          <div className="mt-0.5 flex items-center gap-2 text-[10px] text-zinc-600">
            <span>{item.messageCount} msgs</span>
            <span>&middot;</span>
            <span>{formatDate(item.lastMessageAt ?? item.createdAt)}</span>
          </div>
        </div>

        <button
          onClick={(e) => {
            e.stopPropagation()
            onDeleteClick()
          }}
          className="opacity-0 group-hover:opacity-100 shrink-0 p-0.5 text-zinc-600 hover:text-red-400 transition-all"
        >
          <Trash2 className="h-3.5 w-3.5" />
        </button>
      </div>

      {isDeleting && (
        <div className="mt-1.5 flex items-center gap-2" onClick={(e) => e.stopPropagation()}>
          <span className="text-[10px] text-red-400">Delete?</span>
          <button
            onClick={onDeleteConfirm}
            className="text-[10px] text-red-400 hover:text-red-300 font-medium"
          >
            Yes
          </button>
          <button
            onClick={onDeleteClick}
            className="text-[10px] text-zinc-500 hover:text-zinc-300"
          >
            No
          </button>
        </div>
      )}
    </div>
  )
}
