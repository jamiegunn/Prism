import { X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { MetricChart } from './MetricChart'
import type { RunComparison } from '../types'

interface RunComparisonViewProps {
  comparison: RunComparison
  onClose: () => void
}

export function RunComparisonView({ comparison, onClose }: RunComparisonViewProps) {
  const { runs, parameterDiffs, metricComparison } = comparison

  const runLabels = runs.map(
    (r) => r.name || `${r.model} (${r.id.slice(0, 6)})`
  )

  // Filter parameter diffs to only show differences
  const diffedParams = Object.entries(parameterDiffs).filter(([, values]) => {
    const unique = new Set(values)
    return unique.size > 1
  })

  return (
    <div className="rounded-lg border border-border bg-card">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <h3 className="font-medium text-zinc-100">
          Run Comparison ({runs.length} runs)
        </h3>
        <Button size="sm" variant="ghost" onClick={onClose} className="h-7 w-7 p-0">
          <X className="h-4 w-4" />
        </Button>
      </div>

      <ScrollArea className="h-[calc(100vh-16rem)]">
        <div className="p-4 space-y-6">
          {/* Parameter Diffs */}
          {diffedParams.length > 0 && (
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-3">Parameter Differences</h4>
              <div className="overflow-x-auto rounded border border-border">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-border bg-zinc-900/50">
                      <th className="px-3 py-2 text-left text-xs font-medium text-zinc-400">
                        Parameter
                      </th>
                      {runLabels.map((label, i) => (
                        <th
                          key={i}
                          className="px-3 py-2 text-left text-xs font-medium text-zinc-400 truncate max-w-[120px]"
                        >
                          {label}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {diffedParams.map(([param, values]) => (
                      <tr key={param} className="border-b border-border last:border-0">
                        <td className="px-3 py-2 text-zinc-400">{param}</td>
                        {values.map((val, i) => (
                          <td key={i} className="px-3 py-2">
                            <Badge variant="secondary" className="font-mono text-xs">
                              {val ?? 'null'}
                            </Badge>
                          </td>
                        ))}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

          {/* Metric Charts */}
          <div>
            <h4 className="text-xs font-medium text-zinc-400 mb-3">Metric Comparison</h4>
            <div className="grid grid-cols-1 gap-4">
              {Object.entries(metricComparison).map(([metric, values]) => {
                const hasData = values.some((v) => v != null)
                if (!hasData) return null
                return (
                  <MetricChart
                    key={metric}
                    metric={metric}
                    labels={runLabels}
                    values={values}
                  />
                )
              })}
            </div>
          </div>
        </div>
      </ScrollArea>
    </div>
  )
}
