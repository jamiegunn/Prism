import { BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from 'recharts'
import { useDatasetStats } from '../api'

interface DatasetStatsPanelProps {
  datasetId: string
}

export function DatasetStatsPanel({ datasetId }: DatasetStatsPanelProps) {
  const { data: stats, isLoading } = useDatasetStats(datasetId)

  if (isLoading) {
    return <div className="p-4 text-sm text-muted-foreground">Loading stats...</div>
  }

  if (!stats) return null

  const splitData = Object.entries(stats.splitDistribution).map(([name, count]) => ({
    name,
    count,
  }))

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-4">
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Total Records</p>
          <p className="text-2xl font-bold">{stats.recordCount.toLocaleString()}</p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Splits</p>
          <p className="text-2xl font-bold">{Object.keys(stats.splitDistribution).length}</p>
        </div>
      </div>

      {splitData.length > 0 && (
        <div>
          <h4 className="text-sm font-medium mb-2">Split Distribution</h4>
          <div className="h-48">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={splitData}>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip />
                <Bar dataKey="count" fill="hsl(var(--primary))" radius={[4, 4, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>
      )}

      {stats.columnStats.length > 0 && (
        <div>
          <h4 className="text-sm font-medium mb-2">Column Statistics</h4>
          <div className="space-y-2">
            {stats.columnStats.map((col) => (
              <div key={col.column} className="rounded border p-3 text-sm">
                <div className="flex items-center justify-between mb-1">
                  <span className="font-medium">{col.column}</span>
                  <span className="text-xs text-muted-foreground">
                    {col.uniqueCount} unique · {col.nullCount} null
                  </span>
                </div>
                {col.topValues.length > 0 && (
                  <div className="flex flex-wrap gap-1 mt-1">
                    {col.topValues.slice(0, 5).map((tv, i) => (
                      <span key={i} className="text-xs bg-muted px-1.5 py-0.5 rounded">
                        {tv.value} ({tv.count})
                      </span>
                    ))}
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
