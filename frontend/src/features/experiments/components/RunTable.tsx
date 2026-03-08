import { useState } from 'react'
import {
  ArrowUpDown,
  ChevronLeft,
  ChevronRight,
  Trash2,
  GitCompareArrows,
} from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { toast } from 'sonner'
import { cn } from '@/lib/utils'
import { useRuns, useDeleteRun, useCompareRuns } from '../api'
import { useExperimentsStore } from '../store'
import type { Run, RunStatus } from '../types'

const statusColors: Record<RunStatus, string> = {
  Pending: 'bg-yellow-500/10 text-yellow-400',
  Running: 'bg-blue-500/10 text-blue-400',
  Completed: 'bg-green-500/10 text-green-400',
  Failed: 'bg-red-500/10 text-red-400',
}

interface RunTableProps {
  experimentId: string
  onSelectRun: (run: Run) => void
  onCompareResult: (comparison: import('../types').RunComparison) => void
}

export function RunTable({ experimentId, onSelectRun, onCompareResult }: RunTableProps) {
  const [page, setPage] = useState(1)
  const { runsSortBy, runsSortOrder, setRunsSortBy, setRunsSortOrder, runsPageSize } =
    useExperimentsStore()
  const { selectedRunIds, toggleRunSelection, clearRunSelection } =
    useExperimentsStore()

  const { data, isLoading } = useRuns(experimentId, {
    sortBy: runsSortBy,
    order: runsSortOrder,
    page,
    pageSize: runsPageSize,
  })

  const deleteMutation = useDeleteRun(experimentId)
  const compareMutation = useCompareRuns(experimentId)

  function handleSort(field: string) {
    if (runsSortBy === field) {
      setRunsSortOrder(runsSortOrder === 'asc' ? 'desc' : 'asc')
    } else {
      setRunsSortBy(field)
      setRunsSortOrder('desc')
    }
  }

  function handleDelete(runId: string) {
    deleteMutation.mutate(runId, {
      onSuccess: () => toast.success('Run deleted'),
      onError: (error) => toast.error(`Delete failed: ${error.message}`),
    })
  }

  function handleCompare() {
    if (selectedRunIds.length < 2) {
      toast.error('Select at least 2 runs to compare')
      return
    }
    compareMutation.mutate(selectedRunIds, {
      onSuccess: (result) => {
        onCompareResult(result)
        clearRunSelection()
      },
      onError: (error) => toast.error(`Compare failed: ${error.message}`),
    })
  }

  const columns: { key: string; label: string; sortable: boolean }[] = [
    { key: 'select', label: '', sortable: false },
    { key: 'name', label: 'Name', sortable: false },
    { key: 'model', label: 'Model', sortable: true },
    { key: 'status', label: 'Status', sortable: false },
    { key: 'latencyMs', label: 'Latency', sortable: true },
    { key: 'totalTokens', label: 'Tokens', sortable: true },
    { key: 'cost', label: 'Cost', sortable: true },
    { key: 'createdAt', label: 'Created', sortable: true },
    { key: 'actions', label: '', sortable: false },
  ]

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-12 w-full" />
        ))}
      </div>
    )
  }

  const runs = data?.items ?? []

  return (
    <div className="space-y-3">
      {selectedRunIds.length > 0 && (
        <div className="flex items-center gap-3 rounded-md bg-violet-500/10 border border-violet-500/20 px-4 py-2">
          <span className="text-sm text-violet-300">
            {selectedRunIds.length} run{selectedRunIds.length !== 1 ? 's' : ''} selected
          </span>
          <Button
            size="sm"
            variant="outline"
            className="gap-1"
            onClick={handleCompare}
            disabled={compareMutation.isPending}
          >
            <GitCompareArrows className="h-3 w-3" />
            Compare
          </Button>
          <Button size="sm" variant="ghost" onClick={clearRunSelection}>
            Clear
          </Button>
        </div>
      )}

      <div className="overflow-x-auto rounded-lg border border-border">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-border bg-zinc-900/50">
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={cn(
                    'px-4 py-3 text-left text-xs font-medium text-zinc-400',
                    col.sortable && 'cursor-pointer hover:text-zinc-200'
                  )}
                  onClick={() => col.sortable && handleSort(col.key)}
                >
                  <span className="flex items-center gap-1">
                    {col.label}
                    {col.sortable && runsSortBy === col.key && (
                      <ArrowUpDown className="h-3 w-3" />
                    )}
                  </span>
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {runs.length === 0 ? (
              <tr>
                <td colSpan={columns.length} className="px-4 py-8 text-center text-zinc-500">
                  No runs yet. Create runs from the Prompt Lab or API.
                </td>
              </tr>
            ) : (
              runs.map((run) => (
                <tr
                  key={run.id}
                  className="border-b border-border last:border-0 hover:bg-zinc-800/30 cursor-pointer"
                  onClick={() => onSelectRun(run)}
                >
                  <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                    <input
                      type="checkbox"
                      className="h-4 w-4 rounded border-zinc-700 bg-zinc-900"
                      checked={selectedRunIds.includes(run.id)}
                      onChange={() => toggleRunSelection(run.id)}
                    />
                  </td>
                  <td className="px-4 py-3 text-zinc-200 truncate max-w-[150px]">
                    {run.name || run.id.slice(0, 8)}
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant="secondary" className="font-mono text-xs">
                      {run.model}
                    </Badge>
                  </td>
                  <td className="px-4 py-3">
                    <Badge variant="outline" className={statusColors[run.status]}>
                      {run.status}
                    </Badge>
                  </td>
                  <td className="px-4 py-3 text-zinc-400 tabular-nums">
                    {run.latencyMs.toLocaleString()}ms
                  </td>
                  <td className="px-4 py-3 text-zinc-400 tabular-nums">
                    {run.totalTokens.toLocaleString()}
                  </td>
                  <td className="px-4 py-3 text-zinc-400 tabular-nums">
                    {run.cost != null ? `$${run.cost.toFixed(4)}` : '-'}
                  </td>
                  <td className="px-4 py-3 text-zinc-500 text-xs">
                    {new Date(run.createdAt).toLocaleString()}
                  </td>
                  <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 w-7 p-0 text-zinc-500 hover:text-red-400"
                      onClick={() => handleDelete(run.id)}
                    >
                      <Trash2 className="h-3 w-3" />
                    </Button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-between">
          <span className="text-xs text-zinc-500">
            Page {data.page} of {data.totalPages} ({data.totalCount} runs)
          </span>
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="outline"
              disabled={page <= 1}
              onClick={() => setPage(page - 1)}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              size="sm"
              variant="outline"
              disabled={page >= data.totalPages}
              onClick={() => setPage(page + 1)}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
