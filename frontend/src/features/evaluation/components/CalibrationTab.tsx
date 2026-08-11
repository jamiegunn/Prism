import { useState } from 'react'
import { Select } from '@/components/ui/select'
import { describeMutationError } from '@/services/mutationErrors'
import { useCalibration } from '../api'
import { CalibrationPlot } from './CalibrationPlot'

interface CalibrationTabProps {
  evaluationId: string
  models: string[]
}

/**
 * The Calibration tab: the reliability diagram with ECE and Brier beneath it, and — when the
 * data the computation needs is missing — a statement of which prerequisite is unmet, instead
 * of an empty chart that looks like a perfect score of nothing.
 */
export function CalibrationTab({ evaluationId, models }: CalibrationTabProps) {
  const [model, setModel] = useState<string>(models[0] ?? '')
  const { data, isLoading, isError, error } = useCalibration(evaluationId, model || undefined)

  if (isLoading) {
    return <div className="text-sm text-muted-foreground p-4">Computing calibration…</div>
  }

  if (isError) {
    return (
      <div className="rounded border border-red-900/60 bg-red-950/40 p-4">
        <p className="font-medium text-red-300">Calibration could not be loaded.</p>
        <p className="mt-1 text-sm text-red-200/80">{describeMutationError(error)}</p>
      </div>
    )
  }

  if (!data) {
    return null
  }

  const prerequisite =
    data.totalResults === 0
      ? 'This evaluation has no successful results yet, so there is nothing to calibrate.'
      : data.withLogprobs === 0
        ? 'No logprobs were recorded for these answers. Calibration needs a provider that supports logprobs; this evaluation ran without them.'
        : data.withLabel === 0
          ? 'No correctness labels: calibration labels an answer correct when its exact_match score is 1.0, and this evaluation did not run the exact_match scorer.'
          : null

  return (
    <div className="grid gap-4 max-w-3xl">
      {models.length > 1 && (
        <div className="w-64">
          <label className="text-xs text-muted-foreground mb-1 block">Model</label>
          <Select value={model} onChange={(e) => setModel(e.target.value)} className="h-9 text-sm">
            {models.map((m) => (
              <option key={m} value={m}>
                {m}
              </option>
            ))}
          </Select>
        </div>
      )}

      {prerequisite ? (
        <div className="rounded-lg border p-4 text-sm text-muted-foreground">
          <p className="font-medium text-foreground mb-1">Calibration not available</p>
          <p>{prerequisite}</p>
          <p className="mt-2 text-xs">
            {data.totalResults} successful result{data.totalResults === 1 ? '' : 's'} ·{' '}
            {data.withLogprobs} with logprobs · {data.withLabel} with a label
          </p>
        </div>
      ) : (
        <>
          <CalibrationPlot results={data.predictions} ece={data.ece} />

          <div className="grid grid-cols-3 gap-3">
            <Metric label={`ECE (${data.binCount} bins)`} value={data.ece} />
            <Metric label="Brier score" value={data.brier} />
            <Metric label="Predictions" value={data.withLabel} format={(v) => String(v)} />
          </div>

          <p className="text-xs text-muted-foreground leading-relaxed">{data.definition}</p>
        </>
      )}
    </div>
  )
}

function Metric({
  label,
  value,
  format = (v: number) => v.toFixed(4),
}: {
  label: string
  value: number | null
  format?: (v: number) => string
}) {
  return (
    <div className="rounded-lg border p-3">
      <div className="text-xs text-muted-foreground">{label}</div>
      <div className="text-lg font-mono mt-1">{value === null ? '—' : format(value)}</div>
    </div>
  )
}
