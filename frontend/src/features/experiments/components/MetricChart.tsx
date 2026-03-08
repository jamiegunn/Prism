import {
  BarChart,
  Bar,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  Cell,
} from 'recharts'

const COLORS = [
  '#8b5cf6', // violet
  '#06b6d4', // cyan
  '#f59e0b', // amber
  '#10b981', // emerald
  '#f43f5e', // rose
  '#6366f1', // indigo
]

interface MetricChartProps {
  metric: string
  labels: string[]
  values: (number | null)[]
}

export function MetricChart({ metric, labels, values }: MetricChartProps) {
  const data = labels.map((label, i) => ({
    name: label,
    value: values[i] ?? 0,
    hasValue: values[i] != null,
  }))

  const formatLabel = (key: string) =>
    key.replace(/([A-Z])/g, ' $1').replace(/^./, (s) => s.toUpperCase())

  return (
    <div className="rounded-lg border border-border bg-zinc-900/50 p-4">
      <h5 className="text-xs font-medium text-zinc-400 mb-3">{formatLabel(metric)}</h5>
      <ResponsiveContainer width="100%" height={160}>
        <BarChart data={data} margin={{ top: 4, right: 4, bottom: 4, left: 4 }}>
          <XAxis
            dataKey="name"
            tick={{ fill: '#71717a', fontSize: 10 }}
            axisLine={false}
            tickLine={false}
          />
          <YAxis
            tick={{ fill: '#71717a', fontSize: 10 }}
            axisLine={false}
            tickLine={false}
            width={50}
          />
          <Tooltip
            contentStyle={{
              backgroundColor: '#18181b',
              border: '1px solid #27272a',
              borderRadius: '6px',
              fontSize: '12px',
            }}
          />
          <Bar dataKey="value" radius={[4, 4, 0, 0]}>
            {data.map((_, i) => (
              <Cell key={i} fill={COLORS[i % COLORS.length]} opacity={data[i].hasValue ? 1 : 0.2} />
            ))}
          </Bar>
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
