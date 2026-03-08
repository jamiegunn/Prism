import { FlaskConical, Search, Tag } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'
import { useTemplates } from '../api'
import { usePromptLabStore } from '../store'
import type { PromptTemplate } from '../types'

interface TemplateListProps {
  search: string
  onSearchChange: (value: string) => void
}

export function TemplateList({ search, onSearchChange }: TemplateListProps) {
  const { selectedCategory, selectedTemplateId, setSelectedTemplateId } =
    usePromptLabStore()
  const { data: templates, isLoading } = useTemplates(
    selectedCategory ?? undefined,
    search || undefined
  )

  // Extract unique categories
  const categories = templates
    ? [...new Set(templates.map((t) => t.category).filter(Boolean))]
    : []

  return (
    <div className="flex flex-col h-full">
      <div className="p-3 border-b border-border">
        <div className="relative">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-zinc-500" />
          <Input
            placeholder="Search templates..."
            value={search}
            onChange={(e) => onSearchChange(e.target.value)}
            className="pl-9"
          />
        </div>

        {categories.length > 0 && (
          <div className="flex flex-wrap gap-1 mt-2">
            <CategoryBadge
              label="All"
              active={!selectedCategory}
              onClick={() => usePromptLabStore.getState().setSelectedCategory(null)}
            />
            {categories.map((cat) => (
              <CategoryBadge
                key={cat}
                label={cat!}
                active={selectedCategory === cat}
                onClick={() =>
                  usePromptLabStore
                    .getState()
                    .setSelectedCategory(selectedCategory === cat ? null : cat!)
                }
              />
            ))}
          </div>
        )}
      </div>

      <ScrollArea className="flex-1">
        <div className="p-2 space-y-1">
          {isLoading ? (
            Array.from({ length: 6 }).map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))
          ) : !templates || templates.length === 0 ? (
            <div className="flex flex-col items-center py-8 text-zinc-500">
              <FlaskConical className="h-8 w-8 mb-2" />
              <p className="text-sm">No templates found</p>
            </div>
          ) : (
            templates.map((template) => (
              <TemplateListItem
                key={template.id}
                template={template}
                isSelected={template.id === selectedTemplateId}
                onClick={() => setSelectedTemplateId(template.id)}
              />
            ))
          )}
        </div>
      </ScrollArea>
    </div>
  )
}

function TemplateListItem({
  template,
  isSelected,
  onClick,
}: {
  template: PromptTemplate
  isSelected: boolean
  onClick: () => void
}) {
  return (
    <button
      onClick={onClick}
      className={cn(
        'w-full text-left rounded-md px-3 py-2.5 transition-colors',
        isSelected
          ? 'bg-violet-500/10 border border-violet-500/30'
          : 'hover:bg-zinc-800/50'
      )}
    >
      <div className="flex items-center justify-between">
        <span className="text-sm font-medium text-zinc-200 truncate">
          {template.name}
        </span>
        <span className="text-xs text-zinc-500">v{template.latestVersion}</span>
      </div>
      {template.description && (
        <p className="text-xs text-zinc-500 mt-0.5 truncate">{template.description}</p>
      )}
      {template.tags.length > 0 && (
        <div className="flex items-center gap-1 mt-1">
          <Tag className="h-3 w-3 text-zinc-600" />
          {template.tags.slice(0, 3).map((tag) => (
            <span key={tag} className="text-[10px] text-zinc-500">
              {tag}
            </span>
          ))}
        </div>
      )}
    </button>
  )
}

function CategoryBadge({
  label,
  active,
  onClick,
}: {
  label: string
  active: boolean
  onClick: () => void
}) {
  return (
    <button onClick={onClick}>
      <Badge
        variant={active ? 'default' : 'secondary'}
        className={cn('text-xs cursor-pointer', !active && 'opacity-60')}
      >
        {label}
      </Badge>
    </button>
  )
}
