import { TrendingUp, TrendingDown, Minus, Timer, Hash, Gauge, DollarSign } from 'lucide-react'
import { cn } from '@/lib/utils'
import type { Run } from '../types'

interface RunStatsSummaryProps {
  runs: Run[]
  className?: string
}

interface StatCardProps {
  label: string
  value: string
  subtext?: string
  icon: typeof Timer
  trend?: 'up' | 'down' | 'neutral'
}

function computeStats(values: number[]) {
  if (values.length === 0) return null
  const sorted = [...values].sort((a, b) => a - b)
  const sum = sorted.reduce((a, b) => a + b, 0)
  const mean = sum / sorted.length
  const median = sorted.length % 2 === 0
    ? (sorted[sorted.length / 2 - 1] + sorted[sorted.length / 2]) / 2
    : sorted[Math.floor(sorted.length / 2)]
  const variance = sorted.reduce((acc, v) => acc + (v - mean) ** 2, 0) / sorted.length
  const stdDev = Math.sqrt(variance)
  const min = sorted[0]
  const max = sorted[sorted.length - 1]
  return { mean, median, stdDev, min, max, count: sorted.length }
}

export function RunStatsSummary({ runs, className }: RunStatsSummaryProps) {
  const completedRuns = runs.filter((r) => r.status === 'Completed')

  if (completedRuns.length === 0) {
    return null
  }

  const latencies = completedRuns.map((r) => r.latencyMs)
  const tokens = completedRuns.map((r) => r.totalTokens)
  const throughputs = completedRuns.filter((r) => r.tokensPerSecond != null).map((r) => r.tokensPerSecond!)
  const perplexities = completedRuns.filter((r) => r.perplexity != null).map((r) => r.perplexity!)
  const costs = completedRuns.filter((r) => r.cost != null).map((r) => r.cost!)

  const latencyStats = computeStats(latencies)
  const tokenStats = computeStats(tokens)
  const throughputStats = computeStats(throughputs)
  const perplexityStats = computeStats(perplexities)

  // Compute custom metric stats
  const metricKeys = new Set<string>()
  completedRuns.forEach((r) => Object.keys(r.metrics).forEach((k) => metricKeys.add(k)))

  const totalCost = costs.reduce((a, b) => a + b, 0)
  const failedCount = runs.filter((r) => r.status === 'Failed').length

  return (
    <div className={cn('space-y-3', className)}>
      {/* Summary strip */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-2">
        <StatCard
          label="Runs"
          value={`${completedRuns.length}/${runs.length}`}
          subtext={failedCount > 0 ? `${failedCount} failed` : undefined}
          icon={Hash}
          trend={failedCount > 0 ? 'down' : 'neutral'}
        />
        {latencyStats && (
          <StatCard
            label="Avg Latency"
            value={`${latencyStats.mean.toFixed(0)}ms`}
            subtext={`${latencyStats.min.toFixed(0)}–${latencyStats.max.toFixed(0)}ms`}
            icon={Timer}
          />
        )}
        {throughputStats && (
          <StatCard
            label="Avg Throughput"
            value={`${throughputStats.mean.toFixed(1)} t/s`}
            subtext={`${throughputStats.min.toFixed(1)}–${throughputStats.max.toFixed(1)} t/s`}
            icon={Gauge}
          />
        )}
        {totalCost > 0 && (
          <StatCard
            label="Total Cost"
            value={`$${totalCost.toFixed(4)}`}
            subtext={`$${(totalCost / completedRuns.length).toFixed(4)}/run`}
            icon={DollarSign}
          />
        )}
      </div>

      {/* Metric stats table */}
      {metricKeys.size > 0 && (
        <div className="rounded border border-zinc-800 overflow-hidden">
          <div className="grid grid-cols-6 text-[10px] bg-zinc-800/50 text-zinc-500 font-medium">
            <div className="px-3 py-1.5">Metric</div>
            <div className="px-3 py-1.5 text-right">Mean</div>
            <div className="px-3 py-1.5 text-right">Median</div>
            <div className="px-3 py-1.5 text-right">Std Dev</div>
            <div className="px-3 py-1.5 text-right">Min</div>
            <div className="px-3 py-1.5 text-right">Max</div>
          </div>
          {[...metricKeys].sort().map((key) => {
            const values = completedRuns.map((r) => r.metrics[key]).filter((v) => v != null)
            const stats = computeStats(values)
            if (!stats) return null
            return (
              <div key={key} className="grid grid-cols-6 text-[10px] border-t border-zinc-800/50">
                <div className="px-3 py-1.5 text-zinc-300 font-medium">{key}</div>
                <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{stats.mean.toFixed(3)}</div>
                <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{stats.median.toFixed(3)}</div>
                <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{stats.stdDev.toFixed(3)}</div>
                <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{stats.min.toFixed(3)}</div>
                <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{stats.max.toFixed(3)}</div>
              </div>
            )
          })}

          {/* Built-in metrics */}
          {perplexityStats && (
            <div className="grid grid-cols-6 text-[10px] border-t border-zinc-800/50">
              <div className="px-3 py-1.5 text-zinc-300 font-medium">perplexity</div>
              <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{perplexityStats.mean.toFixed(3)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{perplexityStats.median.toFixed(3)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{perplexityStats.stdDev.toFixed(3)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{perplexityStats.min.toFixed(3)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{perplexityStats.max.toFixed(3)}</div>
            </div>
          )}

          {tokenStats && (
            <div className="grid grid-cols-6 text-[10px] border-t border-zinc-800/50">
              <div className="px-3 py-1.5 text-zinc-300 font-medium">total_tokens</div>
              <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{tokenStats.mean.toFixed(0)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-400 tabular-nums">{tokenStats.median.toFixed(0)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{tokenStats.stdDev.toFixed(0)}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{tokenStats.min}</div>
              <div className="px-3 py-1.5 text-right text-zinc-500 tabular-nums">{tokenStats.max}</div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}

function StatCard({ label, value, subtext, icon: Icon, trend }: StatCardProps) {
  return (
    <div className="rounded border border-zinc-800 bg-zinc-900/50 px-3 py-2">
      <div className="flex items-center gap-1.5 mb-1">
        <Icon className="h-3 w-3 text-zinc-500" />
        <span className="text-[10px] text-zinc-500 uppercase tracking-wider">{label}</span>
        {trend === 'up' && <TrendingUp className="h-3 w-3 text-emerald-500 ml-auto" />}
        {trend === 'down' && <TrendingDown className="h-3 w-3 text-red-500 ml-auto" />}
        {trend === 'neutral' && <Minus className="h-3 w-3 text-zinc-600 ml-auto" />}
      </div>
      <div className="text-sm font-mono font-medium text-zinc-200">{value}</div>
      {subtext && <div className="text-[10px] text-zinc-600 mt-0.5">{subtext}</div>}
    </div>
  )
}
