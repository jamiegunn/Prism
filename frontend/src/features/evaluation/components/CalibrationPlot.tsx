import {
  ScatterChart, Scatter, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, ReferenceLine,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'

interface CalibrationPoint {
  predicted: number
  actual: number
  count: number
}

interface CalibrationPlotProps {
  /** Array of results with predicted confidence and actual correctness */
  results: { confidence: number; isCorrect: boolean }[]
  className?: string
}

/**
 * Calibration plot showing predicted confidence vs actual accuracy.
 * A perfectly calibrated model falls on the diagonal line.
 * Points above the line = underconfident, below = overconfident.
 */
export function CalibrationPlot({ results, className }: CalibrationPlotProps) {
  if (results.length === 0) {
    return (
      <Card className={className}>
        <CardHeader className="pb-2">
          <CardTitle className="text-sm">Calibration</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-xs text-zinc-500">No results with confidence scores to plot.</p>
        </CardContent>
      </Card>
    )
  }

  // Bin results into 10 buckets by predicted confidence
  const bucketCount = 10
  const buckets: { sum: number; correct: number; count: number }[] = Array.from(
    { length: bucketCount },
    () => ({ sum: 0, correct: 0, count: 0 })
  )

  for (const r of results) {
    const bucketIdx = Math.min(Math.floor(r.confidence * bucketCount), bucketCount - 1)
    buckets[bucketIdx].sum += r.confidence
    buckets[bucketIdx].correct += r.isCorrect ? 1 : 0
    buckets[bucketIdx].count += 1
  }

  const points: CalibrationPoint[] = buckets
    .map((b, i) => ({
      predicted: b.count > 0 ? b.sum / b.count : (i + 0.5) / bucketCount,
      actual: b.count > 0 ? b.correct / b.count : 0,
      count: b.count,
    }))
    .filter((p) => p.count > 0)

  // Compute Expected Calibration Error (ECE)
  const totalCount = results.length
  const ece = points.reduce(
    (acc, p) => acc + (p.count / totalCount) * Math.abs(p.predicted - p.actual),
    0
  )

  return (
    <Card className={className}>
      <CardHeader className="pb-2">
        <CardTitle className="text-sm flex items-center justify-between">
          Calibration Plot
          <span className="text-xs font-mono text-zinc-500">
            ECE: {(ece * 100).toFixed(1)}%
          </span>
        </CardTitle>
      </CardHeader>
      <CardContent>
        <ResponsiveContainer width="100%" height={250}>
          <ScatterChart margin={{ top: 10, right: 10, bottom: 20, left: 10 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#27272a" />
            <XAxis
              dataKey="predicted"
              type="number"
              domain={[0, 1]}
              tickFormatter={(v: number) => `${(v * 100).toFixed(0)}%`}
              label={{ value: 'Predicted Confidence', position: 'bottom', offset: 0, style: { fill: '#71717a', fontSize: 10 } }}
              tick={{ fill: '#71717a', fontSize: 10 }}
            />
            <YAxis
              dataKey="actual"
              type="number"
              domain={[0, 1]}
              tickFormatter={(v: number) => `${(v * 100).toFixed(0)}%`}
              label={{ value: 'Actual Accuracy', angle: -90, position: 'insideLeft', style: { fill: '#71717a', fontSize: 10 } }}
              tick={{ fill: '#71717a', fontSize: 10 }}
            />
            <Tooltip
              content={({ payload }) => {
                if (!payload || payload.length === 0) return null
                const d = payload[0].payload as CalibrationPoint
                return (
                  <div className="bg-zinc-800 border border-zinc-700 rounded px-3 py-2 text-xs">
                    <div>Predicted: {(d.predicted * 100).toFixed(1)}%</div>
                    <div>Actual: {(d.actual * 100).toFixed(1)}%</div>
                    <div className="text-zinc-500">{d.count} samples</div>
                  </div>
                )
              }}
            />
            <ReferenceLine
              segment={[{ x: 0, y: 0 }, { x: 1, y: 1 }]}
              stroke="#525252"
              strokeDasharray="4 4"
              label={{ value: 'Perfect', position: 'insideTopLeft', style: { fill: '#525252', fontSize: 9 } }}
            />
            <Scatter data={points} fill="#8b5cf6" />
          </ScatterChart>
        </ResponsiveContainer>
        <div className="flex items-center gap-4 mt-2 text-[10px] text-zinc-600">
          <span>Above line = underconfident</span>
          <span>Below line = overconfident</span>
          <span>{results.length} total predictions</span>
        </div>
      </CardContent>
    </Card>
  )
}
