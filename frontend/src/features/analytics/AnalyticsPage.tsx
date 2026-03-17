import { BarChart3, Zap, Clock } from 'lucide-react'
import {
  BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer,
  LineChart, Line,
} from 'recharts'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useUsage, usePerformance } from './api'

export function AnalyticsPage() {
  const { data: usage } = useUsage()
  const { data: performance } = usePerformance()

  return (
    <div className="flex flex-col gap-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Analytics</h1>
        <p className="text-sm text-muted-foreground">
          Usage tracking, cost breakdown, and performance metrics
        </p>
      </div>

      {/* Summary Cards */}
      <div className="grid gap-4 md:grid-cols-4">
        <SummaryCard
          title="Total Requests"
          value={usage?.totalRequests.toLocaleString() ?? '0'}
          icon={<BarChart3 className="h-4 w-4" />}
        />
        <SummaryCard
          title="Total Tokens"
          value={usage?.totalTokens.toLocaleString() ?? '0'}
          icon={<Zap className="h-4 w-4" />}
        />
        <SummaryCard
          title="Avg Latency"
          value={performance ? `${performance.averageLatencyMs.toFixed(0)}ms` : '—'}
          icon={<Clock className="h-4 w-4" />}
        />
        <SummaryCard
          title="P95 Latency"
          value={performance ? `${performance.p95LatencyMs.toFixed(0)}ms` : '—'}
          icon={<Clock className="h-4 w-4" />}
        />
      </div>

      <Tabs defaultValue="usage">
        <TabsList>
          <TabsTrigger value="usage">Usage</TabsTrigger>
          <TabsTrigger value="performance">Performance</TabsTrigger>
        </TabsList>

        <TabsContent value="usage" className="mt-4 space-y-6">
          {/* Time Series */}
          {usage && usage.timeSeries.length > 0 && (
            <div>
              <h3 className="text-sm font-medium mb-2">Requests Over Time</h3>
              <div className="h-64">
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={usage.timeSeries.map(t => ({ ...t, date: new Date(t.date).toLocaleDateString() }))}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                    <XAxis dataKey="date" tick={{ fontSize: 11 }} />
                    <YAxis tick={{ fontSize: 11 }} />
                    <Tooltip />
                    <Line type="monotone" dataKey="requestCount" stroke="hsl(var(--primary))" strokeWidth={2} dot={false} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </div>
          )}

          {/* By Model */}
          {usage && usage.byModel.length > 0 && (
            <div>
              <h3 className="text-sm font-medium mb-2">Usage by Model</h3>
              <div className="h-48">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={usage.byModel}>
                    <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                    <XAxis dataKey="model" tick={{ fontSize: 11 }} />
                    <YAxis tick={{ fontSize: 11 }} />
                    <Tooltip />
                    <Bar dataKey="requestCount" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          )}

          {/* By Module */}
          {usage && usage.byModule.length > 0 && (
            <div>
              <h3 className="text-sm font-medium mb-2">Usage by Module</h3>
              <div className="flex flex-wrap gap-3">
                {usage.byModule.map((m) => (
                  <div key={m.module} className="rounded-lg border p-3 min-w-[140px]">
                    <p className="text-xs text-muted-foreground capitalize">{m.module}</p>
                    <p className="text-lg font-bold">{m.requestCount}</p>
                    <p className="text-xs text-muted-foreground">{m.totalTokens.toLocaleString()} tokens</p>
                  </div>
                ))}
              </div>
            </div>
          )}
        </TabsContent>

        <TabsContent value="performance" className="mt-4 space-y-6">
          {/* Latency Percentiles */}
          {performance && (
            <div className="grid gap-4 md:grid-cols-4">
              <PercentileCard label="P50" value={performance.p50LatencyMs} />
              <PercentileCard label="P95" value={performance.p95LatencyMs} />
              <PercentileCard label="P99" value={performance.p99LatencyMs} />
              <div className="rounded-lg border p-4">
                <p className="text-sm text-muted-foreground">Avg Tokens/sec</p>
                <p className="text-2xl font-bold">
                  {performance.averageTokensPerSecond?.toFixed(1) ?? '—'}
                </p>
              </div>
            </div>
          )}

          {/* By Model */}
          {performance && performance.byModel.length > 0 && (
            <div>
              <h3 className="text-sm font-medium mb-2">Performance by Model</h3>
              <div className="rounded-lg border overflow-hidden">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b bg-muted/50">
                      <th className="px-4 py-2 text-left font-medium">Model</th>
                      <th className="px-4 py-2 text-left font-medium">Requests</th>
                      <th className="px-4 py-2 text-left font-medium">Avg Latency</th>
                      <th className="px-4 py-2 text-left font-medium">P50</th>
                      <th className="px-4 py-2 text-left font-medium">P95</th>
                      <th className="px-4 py-2 text-left font-medium">TTFT</th>
                      <th className="px-4 py-2 text-left font-medium">Tok/s</th>
                    </tr>
                  </thead>
                  <tbody>
                    {performance.byModel.map((m) => (
                      <tr key={m.model} className="border-b">
                        <td className="px-4 py-2 font-medium">{m.model}</td>
                        <td className="px-4 py-2">{m.requestCount}</td>
                        <td className="px-4 py-2">{m.averageLatencyMs.toFixed(0)}ms</td>
                        <td className="px-4 py-2">{m.p50LatencyMs.toFixed(0)}ms</td>
                        <td className="px-4 py-2">{m.p95LatencyMs.toFixed(0)}ms</td>
                        <td className="px-4 py-2">{m.averageTtftMs?.toFixed(0) ?? '—'}ms</td>
                        <td className="px-4 py-2">{m.averageTokensPerSecond?.toFixed(1) ?? '—'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  )
}

function SummaryCard({ title, value, icon }: { title: string; value: string; icon: React.ReactNode }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
        <CardTitle className="text-sm font-medium">{title}</CardTitle>
        <span className="text-muted-foreground">{icon}</span>
      </CardHeader>
      <CardContent>
        <p className="text-2xl font-bold">{value}</p>
      </CardContent>
    </Card>
  )
}

function PercentileCard({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border p-4">
      <p className="text-sm text-muted-foreground">{label} Latency</p>
      <p className="text-2xl font-bold">{value.toFixed(0)}ms</p>
    </div>
  )
}
