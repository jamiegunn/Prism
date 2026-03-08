import { useState, useCallback } from 'react'
import { Input } from '@/components/ui/input'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Select } from '@/components/ui/select'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import {
  Clock,
  Search,
  CheckCircle,
  XCircle,
  ChevronLeft,
  ChevronRight,
  Filter,
  RotateCcw,
} from 'lucide-react'
import { useHistoryRecords } from './api'
import { RecordDetailPanel } from './components/RecordDetailPanel'
import type { HistoryFilterParams } from './types'
import {
  formatRelativeTime,
  formatTimestamp,
  getModuleBadgeColor,
  SOURCE_MODULES,
} from './utils'
import { cn } from '@/lib/utils'

export function HistoryPage() {
  const [filters, setFilters] = useState<HistoryFilterParams>({
    page: 1,
    pageSize: 20,
  })
  const [searchInput, setSearchInput] = useState('')
  const [selectedRecordId, setSelectedRecordId] = useState<string | null>(null)
  const [detailOpen, setDetailOpen] = useState(false)

  const { data, isLoading, isError } = useHistoryRecords(filters)

  const updateFilter = useCallback(
    (patch: Partial<HistoryFilterParams>) => {
      setFilters((prev) => ({ ...prev, ...patch, page: 1 }))
    },
    []
  )

  const handleSearch = useCallback(() => {
    updateFilter({ search: searchInput || undefined })
  }, [searchInput, updateFilter])

  const handleSearchKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Enter') handleSearch()
  }

  const handleRowClick = (id: string) => {
    setSelectedRecordId(id)
    setDetailOpen(true)
  }

  const handleTagFilter = (tag: string) => {
    setFilters((prev) => ({ ...prev, tags: tag, page: 1 }))
  }

  const resetFilters = () => {
    setFilters({ page: 1, pageSize: 20 })
    setSearchInput('')
  }

  const goToPage = (page: number) => {
    setFilters((prev) => ({ ...prev, page }))
  }

  const records = data?.items ?? []
  const totalPages = data?.totalPages ?? 0
  const currentPage = data?.page ?? 1
  const totalCount = data?.totalCount ?? 0

  return (
    <div className="space-y-4">
      {/* Header */}
      <div>
        <h1 className="text-3xl font-bold tracking-tight">Inference History</h1>
        <p className="text-muted-foreground mt-1">
          Browse, search, and replay past inference calls across all modules.
        </p>
      </div>

      {/* Filter Bar */}
      <div className="flex flex-wrap items-end gap-3 rounded-lg border border-zinc-800 bg-zinc-900/40 p-4">
        {/* Search */}
        <div className="flex-1 min-w-[200px]">
          <label className="text-xs text-zinc-500 mb-1 block">Search</label>
          <div className="relative">
            <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-zinc-500" />
            <Input
              value={searchInput}
              onChange={(e) => setSearchInput(e.target.value)}
              onKeyDown={handleSearchKeyDown}
              placeholder="Search prompts, responses..."
              className="pl-8 h-9 text-sm"
            />
          </div>
        </div>

        {/* Source Module */}
        <div className="min-w-[150px]">
          <label className="text-xs text-zinc-500 mb-1 block">Source</label>
          <Select
            value={filters.sourceModule ?? ''}
            onChange={(e) =>
              updateFilter({ sourceModule: e.target.value || undefined })
            }
            className="h-9 text-sm"
          >
            {SOURCE_MODULES.map((m) => (
              <option key={m.value} value={m.value}>
                {m.label}
              </option>
            ))}
          </Select>
        </div>

        {/* Model */}
        <div className="min-w-[140px]">
          <label className="text-xs text-zinc-500 mb-1 block">Model</label>
          <Input
            value={filters.model ?? ''}
            onChange={(e) => updateFilter({ model: e.target.value || undefined })}
            placeholder="Filter model..."
            className="h-9 text-sm"
          />
        </div>

        {/* Date From */}
        <div className="min-w-[140px]">
          <label className="text-xs text-zinc-500 mb-1 block">From</label>
          <Input
            type="date"
            value={filters.from ?? ''}
            onChange={(e) => updateFilter({ from: e.target.value || undefined })}
            className="h-9 text-sm"
          />
        </div>

        {/* Date To */}
        <div className="min-w-[140px]">
          <label className="text-xs text-zinc-500 mb-1 block">To</label>
          <Input
            type="date"
            value={filters.to ?? ''}
            onChange={(e) => updateFilter({ to: e.target.value || undefined })}
            className="h-9 text-sm"
          />
        </div>

        {/* Success Toggle */}
        <div className="min-w-[120px]">
          <label className="text-xs text-zinc-500 mb-1 block">Status</label>
          <Select
            value={filters.isSuccess === undefined ? '' : String(filters.isSuccess)}
            onChange={(e) => {
              const val = e.target.value
              updateFilter({
                isSuccess: val === '' ? undefined : val === 'true',
              })
            }}
            className="h-9 text-sm"
          >
            <option value="">All</option>
            <option value="true">Success</option>
            <option value="false">Failed</option>
          </Select>
        </div>

        {/* Tag Filter */}
        <div className="min-w-[120px]">
          <label className="text-xs text-zinc-500 mb-1 block">Tag</label>
          <Input
            value={filters.tags ?? ''}
            onChange={(e) => updateFilter({ tags: e.target.value || undefined })}
            placeholder="Filter tag..."
            className="h-9 text-sm"
          />
        </div>

        {/* Action Buttons */}
        <div className="flex gap-2">
          <Button size="sm" className="h-9" onClick={handleSearch}>
            <Filter className="h-3.5 w-3.5 mr-1.5" />
            Apply
          </Button>
          <Button variant="outline" size="sm" className="h-9" onClick={resetFilters}>
            <RotateCcw className="h-3.5 w-3.5 mr-1.5" />
            Reset
          </Button>
        </div>
      </div>

      {/* Results Table */}
      <div className="rounded-lg border border-zinc-800 overflow-hidden">
        {/* Header Row */}
        <div className="grid grid-cols-[140px_110px_1fr_180px_80px_80px_60px_1fr] gap-2 px-4 py-2.5 bg-zinc-800/60 text-xs font-medium text-zinc-400 uppercase tracking-wider">
          <div>Time</div>
          <div>Source</div>
          <div>Model</div>
          <div>Prompt</div>
          <div>Tokens</div>
          <div>Latency</div>
          <div>Status</div>
          <div>Tags</div>
        </div>

        {/* Loading state */}
        {isLoading && (
          <div className="flex items-center justify-center py-16 text-zinc-500 text-sm">
            <Clock className="h-4 w-4 mr-2 animate-spin" />
            Loading history...
          </div>
        )}

        {/* Error state */}
        {isError && (
          <div className="flex items-center justify-center py-16 text-red-400 text-sm">
            Failed to load history records. Check that the API is running.
          </div>
        )}

        {/* Empty state */}
        {!isLoading && !isError && records.length === 0 && (
          <div className="flex flex-col items-center justify-center py-16 text-zinc-500 text-sm">
            <Clock className="h-8 w-8 mb-3 text-zinc-600" />
            <p>No inference records found.</p>
            <p className="text-xs mt-1">Try adjusting your filters or make some inference calls.</p>
          </div>
        )}

        {/* Data rows */}
        {records.map((record, idx) => (
          <div
            key={record.id}
            onClick={() => handleRowClick(record.id)}
            className={cn(
              'grid grid-cols-[140px_110px_1fr_180px_80px_80px_60px_1fr] gap-2 px-4 py-2.5 text-sm cursor-pointer transition-colors hover:bg-zinc-800/40',
              idx % 2 === 0 ? 'bg-zinc-900/20' : 'bg-zinc-900/40'
            )}
          >
            {/* Time */}
            <Tooltip>
              <TooltipTrigger>
                <div className="text-zinc-400 text-xs truncate">
                  {formatRelativeTime(record.startedAt)}
                </div>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <span className="text-xs">{formatTimestamp(record.startedAt)}</span>
              </TooltipContent>
            </Tooltip>

            {/* Source */}
            <div>
              <Badge className={cn('text-[10px] px-1.5 py-0', getModuleBadgeColor(record.sourceModule))}>
                {record.sourceModule}
              </Badge>
            </div>

            {/* Model */}
            <div className="text-zinc-300 text-xs font-mono truncate" title={record.model}>
              {record.model}
            </div>

            {/* Prompt */}
            <div className="text-zinc-400 text-xs truncate" title={record.promptPreview}>
              {record.promptPreview.length > 60
                ? record.promptPreview.slice(0, 60) + '...'
                : record.promptPreview}
            </div>

            {/* Tokens */}
            <div className="text-zinc-300 text-xs font-mono">
              {record.promptTokens}/{record.completionTokens}
            </div>

            {/* Latency */}
            <div className="text-zinc-300 text-xs font-mono">
              {record.latencyMs}ms
            </div>

            {/* Status */}
            <div className="flex items-center">
              {record.isSuccess ? (
                <CheckCircle className="h-4 w-4 text-emerald-500" />
              ) : (
                <XCircle className="h-4 w-4 text-red-500" />
              )}
            </div>

            {/* Tags */}
            <div className="flex flex-wrap gap-1 items-center overflow-hidden">
              {record.tags.slice(0, 3).map((tag) => (
                <Badge
                  key={tag}
                  className="bg-zinc-700 text-zinc-300 text-[10px] px-1.5 py-0 cursor-pointer hover:bg-zinc-600"
                  onClick={(e) => {
                    e.stopPropagation()
                    handleTagFilter(tag)
                  }}
                >
                  {tag}
                </Badge>
              ))}
              {record.tags.length > 3 && (
                <span className="text-[10px] text-zinc-500">+{record.tags.length - 3}</span>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Pagination */}
      {totalPages > 0 && (
        <div className="flex items-center justify-between px-1">
          <span className="text-xs text-zinc-500">
            {totalCount} record{totalCount !== 1 ? 's' : ''} total
          </span>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              className="h-8 w-8 p-0"
              disabled={currentPage <= 1}
              onClick={() => goToPage(currentPage - 1)}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <span className="text-xs text-zinc-400">
              Page {currentPage} of {totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              className="h-8 w-8 p-0"
              disabled={currentPage >= totalPages}
              onClick={() => goToPage(currentPage + 1)}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

      {/* Detail Panel */}
      <RecordDetailPanel
        recordId={selectedRecordId}
        open={detailOpen}
        onClose={() => {
          setDetailOpen(false)
          setSelectedRecordId(null)
        }}
      />
    </div>
  )
}
