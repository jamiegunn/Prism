import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, XCircle } from 'lucide-react'
import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, Legend } from 'recharts'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useEvaluation, useCancelEvaluation, useEvaluationResults } from './api'

const COLORS = ['hsl(var(--primary))', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4']

export function EvaluationDetailPage() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { data: evaluation } = useEvaluation(id ?? null)
  const { data: summary } = useEvaluationResults(id ?? null)
  const cancel = useCancelEvaluation()

  if (!evaluation) return <div className="p-6 text-muted-foreground">Loading...</div>

  // Build chart data: one group per scoring method, one bar per model
  const chartData = summary?.modelSummaries
    ? (() => {
        const allMethods = new Set<string>()
        summary.modelSummaries.forEach((m) => Object.keys(m.averageScores).forEach((k) => allMethods.add(k)))
        return Array.from(allMethods).map((method) => {
          const entry: Record<string, unknown> = { method }
          summary.modelSummaries.forEach((m) => {
            entry[m.model] = m.averageScores[method] ?? 0
          })
          return entry
        })
      })()
    : []

  const models = summary?.modelSummaries.map((m) => m.model) ?? []

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex items-center gap-4">
        <Button variant="ghost" size="sm" onClick={() => navigate('/evaluation')}>
          <ArrowLeft className="h-4 w-4 mr-1" />
          Back
        </Button>
      </div>

      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold">{evaluation.name}</h1>
          <div className="flex items-center gap-3 mt-2">
            <Badge>{evaluation.status}</Badge>
            <span className="text-sm text-muted-foreground">
              {evaluation.completedRecords}/{evaluation.totalRecords} records
              {evaluation.failedRecords > 0 && ` · ${evaluation.failedRecords} failed`}
            </span>
          </div>
          {(evaluation.status === 'Running' || evaluation.status === 'Pending') && (
            <div className="mt-2 w-64">
              <div className="h-2 rounded-full bg-muted overflow-hidden">
                <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${evaluation.progress * 100}%` }} />
              </div>
            </div>
          )}
        </div>
        {(evaluation.status === 'Running' || evaluation.status === 'Pending') && (
          <Button variant="destructive" size="sm" onClick={() => cancel.mutate(evaluation.id)}>
            <XCircle className="h-4 w-4 mr-1" />
            Cancel
          </Button>
        )}
      </div>

      <Tabs defaultValue="summary">
        <TabsList>
          <TabsTrigger value="summary">Summary</TabsTrigger>
          <TabsTrigger value="details">Model Details</TabsTrigger>
        </TabsList>

        <TabsContent value="summary" className="mt-4">
          {chartData.length > 0 && (
            <div className="h-72 mb-6">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={chartData}>
                  <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                  <XAxis dataKey="method" tick={{ fontSize: 12 }} />
                  <YAxis domain={[0, 1]} tick={{ fontSize: 12 }} />
                  <Tooltip />
                  <Legend />
                  {models.map((model, i) => (
                    <Bar key={model} dataKey={model} fill={COLORS[i % COLORS.length]} radius={[4, 4, 0, 0]} />
                  ))}
                </BarChart>
              </ResponsiveContainer>
            </div>
          )}

          {summary?.modelSummaries && (
            <div className="rounded-lg border overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-2 text-left font-medium">Model</th>
                    <th className="px-4 py-2 text-left font-medium">Records</th>
                    <th className="px-4 py-2 text-left font-medium">Avg Latency</th>
                    <th className="px-4 py-2 text-left font-medium">Tokens</th>
                    <th className="px-4 py-2 text-left font-medium">Errors</th>
                    <th className="px-4 py-2 text-left font-medium">Scores</th>
                  </tr>
                </thead>
                <tbody>
                  {summary.modelSummaries.map((m) => (
                    <tr key={m.model} className="border-b">
                      <td className="px-4 py-2 font-medium">{m.model}</td>
                      <td className="px-4 py-2">{m.recordCount}</td>
                      <td className="px-4 py-2">{m.averageLatencyMs.toFixed(0)}ms</td>
                      <td className="px-4 py-2 text-xs">
                        {m.totalPromptTokens.toLocaleString()} / {m.totalCompletionTokens.toLocaleString()}
                      </td>
                      <td className="px-4 py-2">
                        {m.errorCount > 0 ? <span className="text-destructive">{m.errorCount}</span> : '0'}
                      </td>
                      <td className="px-4 py-2">
                        <div className="flex gap-1 flex-wrap">
                          {Object.entries(m.averageScores).map(([k, v]) => (
                            <Badge key={k} variant="secondary" className="text-xs">
                              {k}: {v.toFixed(3)}
                            </Badge>
                          ))}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </TabsContent>

        <TabsContent value="details" className="mt-4">
          <div className="grid gap-4">
            <div className="rounded-lg border p-4">
              <h3 className="font-medium mb-2">Configuration</h3>
              <div className="grid grid-cols-2 gap-2 text-sm">
                <div><span className="text-muted-foreground">Models:</span> {evaluation.models.join(', ')}</div>
                <div><span className="text-muted-foreground">Scoring:</span> {evaluation.scoringMethods.join(', ')}</div>
                <div><span className="text-muted-foreground">Split:</span> {evaluation.splitLabel ?? 'All'}</div>
                {evaluation.startedAt && <div><span className="text-muted-foreground">Started:</span> {new Date(evaluation.startedAt).toLocaleString()}</div>}
                {evaluation.finishedAt && <div><span className="text-muted-foreground">Finished:</span> {new Date(evaluation.finishedAt).toLocaleString()}</div>}
              </div>
            </div>
            {evaluation.errorMessage && (
              <div className="rounded-lg border border-destructive/50 p-4">
                <h3 className="font-medium text-destructive mb-1">Error</h3>
                <p className="text-sm">{evaluation.errorMessage}</p>
              </div>
            )}
          </div>
        </TabsContent>
      </Tabs>
    </div>
  )
}
