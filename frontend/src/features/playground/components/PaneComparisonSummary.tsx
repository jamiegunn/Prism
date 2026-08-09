import { ArrowDown, ArrowUp, Gauge, Zap } from 'lucide-react'
import { cn } from '@/lib/utils'
import {
  comparePanes,
  formatRatio,
  goodnessFraction,
  type MetricComparison,
  type PaneMetricsInput,
  type PaneMetricSummary,
} from '../paneComparison'

interface PaneComparisonSummaryProps {
  panes: PaneMetricsInput[]
  className?: string
}

/**
 * Answers "which pane is actually faster, and in which respect?" without making the
 * reader run the same prompt twice and hold two Statistics panels in their head.
 *
 * The two metrics are deliberately never combined into one score: TTFT is compute-bound
 * and tok/s is memory-bandwidth-bound, so a GPU-accelerated server can win the first
 * decisively while tying the second. That split is the whole point of the panel.
 */
export function PaneComparisonSummary({ panes, className }: PaneComparisonSummaryProps) {
  // Nothing to compare against: stay out of the way rather than showing a table of one.
  if (panes.length < 2) return null

  const comparison = comparePanes(panes)
  if (comparison.isEmpty) return null

  return (
    <div className={cn('border-t border-border bg-zinc-900/40 px-4 py-3', className)}>
      <h2 className="text-[11px] font-semibold uppercase tracking-wider text-zinc-500 mb-3">
        Performance comparison
      </h2>
      <div className="grid gap-x-8 gap-y-4 md:grid-cols-2">
        <MetricSection
          comparison={comparison.ttft}
          icon={Zap}
          title="Time to first token"
          direction="lower"
          subtitle="prompt processing, compute-bound"
          format={(value) => `${Math.round(value).toLocaleString()} ms`}
        />
        <MetricSection
          comparison={comparison.throughput}
          icon={Gauge}
          title="Decode throughput"
          direction="higher"
          subtitle="memory-bandwidth-bound"
          format={(value) => `${value.toFixed(1)} tok/s`}
        />
      </div>
    </div>
  )
}

interface MetricSectionProps {
  comparison: MetricComparison
  icon: typeof Zap
  title: string
  subtitle: string
  direction: 'lower' | 'higher'
  format: (value: number) => string
}

function MetricSection({
  comparison,
  icon: Icon,
  title,
  subtitle,
  direction,
  format,
}: MetricSectionProps) {
  const DirectionIcon = direction === 'lower' ? ArrowDown : ArrowUp

  return (
    <section>
      <header className="flex items-baseline gap-2 mb-2">
        <h3 className="flex items-center gap-1.5 text-xs font-medium text-zinc-300">
          <Icon className="h-3 w-3 text-zinc-500" />
          {title}
        </h3>
        <span className="flex items-center gap-0.5 text-[10px] text-emerald-400">
          <DirectionIcon className="h-3 w-3" />
          {direction} is better
        </span>
        <span className="text-[10px] text-zinc-600 truncate">{subtitle}</span>
      </header>

      <table className="w-full text-xs">
        <tbody className="divide-y divide-zinc-800">
          {comparison.panes.map((pane) => (
            <MetricRow key={pane.paneId} pane={pane} format={format} />
          ))}
        </tbody>
      </table>

      {comparison.measuredCount === 1 && (
        <p className="mt-1.5 text-[10px] text-zinc-600">
          Only one pane reported this metric, so there is nothing to compare it against.
        </p>
      )}
    </section>
  )
}

interface MetricRowProps {
  pane: PaneMetricSummary
  format: (value: number) => string
}

function MetricRow({ pane, format }: MetricRowProps) {
  const measured = pane.average != null
  const relative = formatRatio(pane.ratioToBest)

  return (
    <tr>
      <td className="py-1.5 pr-3 text-zinc-400 truncate max-w-[10rem]" title={pane.label}>
        {pane.label}
      </td>
      <td className="py-1.5 pr-3 w-[30%]">
        {measured && (
          <div
            className="h-1.5 rounded-full bg-zinc-800 overflow-hidden"
            aria-hidden="true"
          >
            <div
              className={cn(
                'h-full rounded-full',
                pane.isBest ? 'bg-emerald-500' : 'bg-violet-500'
              )}
              style={{ width: `${goodnessFraction(pane) * 100}%` }}
            />
          </div>
        )}
      </td>
      <td
        className={cn(
          'py-1.5 pr-3 text-right font-mono tabular-nums whitespace-nowrap',
          measured ? 'text-zinc-200' : 'text-zinc-600'
        )}
      >
        {/* Never dress an absent measurement up as a number — an em-dash says so plainly. */}
        {measured ? format(pane.average as number) : <span title="not measured">&mdash;</span>}
      </td>
      <td className="py-1.5 pr-3 text-right text-[10px] whitespace-nowrap">
        {pane.isBest ? (
          <span className="text-emerald-400">best</span>
        ) : relative ? (
          <span className="text-zinc-500 font-mono tabular-nums">{relative}</span>
        ) : (
          <span className="text-zinc-600">{measured ? '' : 'not measured'}</span>
        )}
      </td>
      <td className="py-1.5 text-right text-[10px] font-mono tabular-nums text-zinc-600 whitespace-nowrap">
        {/* A single sample is noise, so the sample size travels with the average. */}
        {pane.sampleCount > 0 ? `n=${pane.sampleCount}` : ''}
      </td>
    </tr>
  )
}
