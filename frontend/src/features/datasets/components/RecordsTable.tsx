import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { useDatasetRecords } from '../api'
import { useDatasetsStore } from '../store'
import type { ColumnSchema } from '../types'

interface RecordsTableProps {
  datasetId: string
  schema: ColumnSchema[]
}

export function RecordsTable({ datasetId, schema }: RecordsTableProps) {
  const { recordsPage, recordsPageSize, splitFilter, setRecordsPage } = useDatasetsStore()
  const { data, isLoading } = useDatasetRecords(datasetId, splitFilter ?? undefined, recordsPage, recordsPageSize)

  const columnNames = schema.map((c) => c.name)

  if (isLoading) {
    return <div className="p-4 text-sm text-muted-foreground">Loading records...</div>
  }

  if (!data || data.items.length === 0) {
    return <div className="p-4 text-sm text-muted-foreground">No records found.</div>
  }

  return (
    <div>
      <div className="overflow-x-auto rounded-lg border">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b bg-muted/50">
              <th className="px-3 py-2 text-left font-medium text-muted-foreground">#</th>
              {splitFilter === null && (
                <th className="px-3 py-2 text-left font-medium text-muted-foreground">Split</th>
              )}
              {columnNames.map((col) => (
                <th key={col} className="px-3 py-2 text-left font-medium text-muted-foreground">
                  {col}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.items.map((record, _i) => (
              <tr key={record.id} className="border-b hover:bg-muted/30">
                <td className="px-3 py-2 text-muted-foreground">{record.orderIndex + 1}</td>
                {splitFilter === null && (
                  <td className="px-3 py-2">
                    {record.splitLabel ? (
                      <Badge variant="secondary" className="text-xs">{record.splitLabel}</Badge>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </td>
                )}
                {columnNames.map((col) => (
                  <td key={col} className="px-3 py-2 max-w-xs truncate">
                    {formatValue(record.data[col])}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="flex items-center justify-between mt-3 text-sm text-muted-foreground">
        <span>
          Showing {(recordsPage - 1) * recordsPageSize + 1}–
          {Math.min(recordsPage * recordsPageSize, data.totalCount)} of {data.totalCount}
        </span>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={recordsPage <= 1}
            onClick={() => setRecordsPage(recordsPage - 1)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span>Page {recordsPage} of {data.totalPages}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={recordsPage >= data.totalPages}
            onClick={() => setRecordsPage(recordsPage + 1)}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  )
}

function formatValue(value: unknown): string {
  if (value === null || value === undefined) return '—'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}
