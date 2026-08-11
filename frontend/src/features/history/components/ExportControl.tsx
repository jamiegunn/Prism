import { useState } from 'react'
import { toast } from 'sonner'
import { Download, Loader2 } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { useExportHistory, type HistoryExportFormat } from '../api'
import type { HistoryFilterParams } from '../types'

interface ExportControlProps {
  /** The applied filters — the export selects exactly what these select. */
  filters: HistoryFilterParams
  /** How many records the applied filters currently match. */
  totalCount: number
  /** True while the list itself is loading, when the count cannot be trusted yet. */
  isLoading: boolean
}

/**
 * The Export control beside the History filter bar. States the row count before writing —
 * the button says how many records will leave — and is disabled when there is nothing to
 * export rather than being clickable and doing nothing.
 */
export function ExportControl({ filters, totalCount, isLoading }: ExportControlProps) {
  const [format, setFormat] = useState<HistoryExportFormat>('jsonl')
  const exportHistory = useExportHistory()

  const disabled = isLoading || totalCount === 0 || exportHistory.isPending
  const disabledReason = isLoading
    ? 'Waiting for the record count…'
    : totalCount === 0
      ? 'No records match the current filters — nothing to export.'
      : undefined

  const handleExport = () => {
    exportHistory.mutate(
      { filters, format },
      {
        onSuccess: ({ fileName, rowCount }) => {
          toast.success(
            rowCount !== null
              ? `Exported ${rowCount} record${rowCount === 1 ? '' : 's'} to ${fileName}`
              : `Exported to ${fileName}`
          )
        },
        onError: (error) => {
          toast.error(error instanceof Error ? error.message : 'Export failed.')
        },
      }
    )
  }

  return (
    <div className="min-w-[220px]" data-tour="history-export">
      <label className="text-xs text-zinc-500 mb-1 block">
        Export what the filters select
      </label>
      <div className="flex gap-2">
        <Select
          value={format}
          onChange={(e) => setFormat(e.target.value as HistoryExportFormat)}
          className="h-9 text-sm w-[110px]"
          aria-label="Export format"
        >
          <option value="jsonl">JSONL</option>
          <option value="csv">CSV</option>
          <option value="parquet">Parquet</option>
        </Select>
        <Button
          variant="outline"
          size="sm"
          className="h-9 whitespace-nowrap"
          disabled={disabled}
          title={disabledReason}
          onClick={handleExport}
        >
          {exportHistory.isPending ? (
            <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
          ) : (
            <Download className="h-3.5 w-3.5 mr-1.5" />
          )}
          Export {isLoading ? '…' : totalCount.toLocaleString()}
        </Button>
      </div>
    </div>
  )
}
