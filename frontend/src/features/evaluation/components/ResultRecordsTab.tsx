import { useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { describeMutationError } from '@/services/mutationErrors'
import { useEvaluationResultRecords } from '../api'

interface ResultRecordsTabProps {
  evaluationId: string
  models: string[]
}

/**
 * Per-record results: which answers a model got wrong, with the scores that judged them.
 * The endpoint always existed; this tab is its first caller — an evaluation you cannot drill
 * into is a leaderboard, not an evaluation.
 */
export function ResultRecordsTab({ evaluationId, models }: ResultRecordsTabProps) {
  const [model, setModel] = useState<string>('')
  const [page, setPage] = useState(1)
  const pageSize = 20
  const { data, isLoading, isError, error } = useEvaluationResultRecords(
    evaluationId,
    model || undefined,
    page,
    pageSize
  )

  if (isLoading) {
    return <div className="text-sm text-muted-foreground p-4">Loading records…</div>
  }

  if (isError) {
    return (
      <div className="rounded border border-red-900/60 bg-red-950/40 p-4">
        <p className="font-medium text-red-300">Records could not be loaded.</p>
        <p className="mt-1 text-sm text-red-200/80">{describeMutationError(error)}</p>
      </div>
    )
  }

  const records = data?.items ?? []
  const totalPages = data?.totalPages ?? 0

  return (
    <div className="space-y-3">
      {models.length > 1 && (
        <div className="w-64">
          <label className="text-xs text-muted-foreground mb-1 block">Model</label>
          <Select
            value={model}
            onChange={(e) => {
              setModel(e.target.value)
              setPage(1)
            }}
            className="h-9 text-sm"
          >
            <option value="">All models</option>
            {models.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </div>
      )}

      {records.length === 0 && (
        <p className="text-sm text-muted-foreground p-4">
          No result records yet — the evaluation may still be running.
        </p>
      )}

      {records.map((r) => (
        <div key={r.id} className="rounded-lg border p-3 text-sm space-y-2">
          <div className="flex items-center justify-between gap-2">
            <span className="font-mono text-xs text-muted-foreground">{r.model}</span>
            <div className="flex gap-1 flex-wrap">
              {Object.entries(r.scores).map(([k, v]) => (
                <Badge key={k} variant="secondary" className="text-xs">
                  {k}: {v.toFixed(3)}
                </Badge>
              ))}
              {r.perplexity !== null && (
                <Badge variant="outline" className="text-xs">
                  ppl: {r.perplexity.toFixed(2)}
                </Badge>
              )}
            </div>
          </div>

          <Field label="Input" value={r.input} />
          <Field label="Expected" value={r.expectedOutput ?? '(none)'} />
          {r.error === null ? (
            <Field label="Answer" value={r.actualOutput ?? '(empty)'} />
          ) : (
            <div className="rounded border border-red-900/60 bg-red-950/40 p-2 text-xs">
              <span className="font-medium text-red-300">Failed: </span>
              <span className="text-red-200/80">{r.error}</span>
            </div>
          )}
        </div>
      ))}

      {totalPages > 1 && (
        <div className="flex items-center justify-end gap-2">
          <Button
            variant="outline"
            size="sm"
            className="h-8 w-8 p-0"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <span className="text-xs text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            className="h-8 w-8 p-0"
            disabled={page >= totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            <ChevronRight className="h-4 w-4" />
          </Button>
        </div>
      )}
    </div>
  )
}

function Field({ label, value }: { label: string; value: string }) {
  const truncated = value.length > 400 ? value.slice(0, 400) + '…' : value
  return (
    <div className="text-xs">
      <span className="text-muted-foreground">{label}: </span>
      <span className="whitespace-pre-wrap">{truncated}</span>
    </div>
  )
}
