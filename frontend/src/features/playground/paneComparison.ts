import type { Message } from './types'

/**
 * Aggregation behind the multi-pane performance comparison.
 *
 * Two servers running the same model are compared on two axes that answer different
 * questions, and collapsing them into a single "speed" number destroys the distinction:
 *
 *  - TTFT (time to first token) is prompt processing. It is compute-bound, so GPU
 *    acceleration (Metal, CUDA) reliably wins here.
 *  - tok/s (decode throughput) is memory-bandwidth-bound. On Apple Silicon the CPU and
 *    GPU share the same memory bandwidth, so a native and a containerised server can
 *    decode at nearly the same rate while their TTFT differs several-fold.
 *
 * Kept separate from the component so the arithmetic — averaging, sample counts, the
 * ratio against the best pane — is testable without rendering.
 */

/** The metrics we compare panes on. */
export type ComparisonMetric = 'ttftMs' | 'tokensPerSecond'

/** One pane's conversation, as fed into the comparison. */
export interface PaneMetricsInput {
  /** Stable pane identity, used as the React key. */
  paneId: string
  /** Human label — the instance name shown in the pane header. */
  label: string
  /** The pane's conversation so far. Only assistant messages are considered. */
  messages: Message[]
}

/** One pane's standing on one metric. */
export interface PaneMetricSummary {
  paneId: string
  label: string
  /** Mean of the measured samples, or null when the pane has none. */
  average: number | null
  /** How many responses the average is over. Zero when unmeasured. */
  sampleCount: number
  /** True for the single best pane, and only when another pane also has data. */
  isBest: boolean
  /**
   * How much worse this pane is than the best one, as a factor >= 1
   * (1.0 = tied with the best). Null when it cannot be computed honestly:
   * no data, fewer than two measured panes, or a non-positive best value.
   */
  ratioToBest: number | null
}

/** Every pane's standing on one metric, plus how many of them actually have data. */
export interface MetricComparison {
  metric: ComparisonMetric
  /** In the caller's pane order, so rows line up with the panes on screen. */
  panes: PaneMetricSummary[]
  /** Panes with at least one sample. Relative differences need at least two. */
  measuredCount: number
}

/** Both metric comparisons for the current set of panes. */
export interface PaneComparison {
  ttft: MetricComparison
  throughput: MetricComparison
  /** True when no pane has a sample for either metric — nothing worth showing. */
  isEmpty: boolean
}

/**
 * A sample counts only if it is a finite positive number.
 *
 * Zero is rejected on purpose: neither a 0ms time to first token nor a 0 tok/s decode
 * rate is a real measurement, and both are what an unset field tends to serialise as.
 * Treating them as data would put a fake winner at the top of the table.
 */
function isMeasured(value: number | null | undefined): value is number {
  return typeof value === 'number' && Number.isFinite(value) && value > 0
}

/** Lower is better for TTFT; higher is better for throughput. */
function isLowerBetter(metric: ComparisonMetric): boolean {
  return metric === 'ttftMs'
}

/** Mean of the assistant messages that carried this metric, ignoring those that did not. */
function averageOf(
  messages: Message[],
  metric: ComparisonMetric
): { average: number | null; sampleCount: number } {
  const samples = messages
    .filter((m) => m.role === 'Assistant')
    .map((m) => m[metric])
    .filter(isMeasured)

  if (samples.length === 0) return { average: null, sampleCount: 0 }

  const total = samples.reduce((sum, value) => sum + value, 0)
  return { average: total / samples.length, sampleCount: samples.length }
}

/** Compare panes on a single metric. */
export function compareMetric(
  panes: PaneMetricsInput[],
  metric: ComparisonMetric
): MetricComparison {
  const averages = panes.map((pane) => ({
    pane,
    ...averageOf(pane.messages, metric),
  }))

  const measured = averages.filter((entry) => entry.average != null)
  const lowerBetter = isLowerBetter(metric)

  const bestValue =
    measured.length > 0
      ? measured.reduce(
          (best, entry) =>
            lowerBetter
              ? Math.min(best, entry.average as number)
              : Math.max(best, entry.average as number),
          measured[0].average as number
        )
      : null

  // A "winner" against a field of one is not a finding, so both the best marker and the
  // relative difference stay off until two panes have real numbers to put side by side.
  const comparable = measured.length >= 2 && bestValue != null && bestValue > 0

  let bestClaimed = false

  return {
    metric,
    measuredCount: measured.length,
    panes: averages.map(({ pane, average, sampleCount }) => {
      const isBest = comparable && average === bestValue && !bestClaimed
      if (isBest) bestClaimed = true

      return {
        paneId: pane.paneId,
        label: pane.label,
        average,
        sampleCount,
        isBest,
        ratioToBest:
          comparable && average != null && average > 0
            ? lowerBetter
              ? average / (bestValue as number)
              : (bestValue as number) / average
            : null,
      }
    }),
  }
}

/** Compare panes on both metrics at once. */
export function comparePanes(panes: PaneMetricsInput[]): PaneComparison {
  const ttft = compareMetric(panes, 'ttftMs')
  const throughput = compareMetric(panes, 'tokensPerSecond')

  return {
    ttft,
    throughput,
    isEmpty: ttft.measuredCount === 0 && throughput.measuredCount === 0,
  }
}

/**
 * Render a ratio the way it reads best.
 *
 * Near-ties are the interesting case here — two Ollama servers on the same Apple Silicon
 * box often decode within a few percent of each other — and "1.0x slower" hides exactly
 * that, so small gaps are reported as a percentage and large ones as a multiplier.
 */
export function formatRatio(ratio: number | null): string | null {
  if (ratio == null || !Number.isFinite(ratio)) return null

  const percent = Math.round((ratio - 1) * 100)
  if (percent <= 0) return 'matched'
  if (ratio >= 1.5) return `${ratio.toFixed(1)}x slower`
  return `${percent}% slower`
}

/**
 * Bar length as a fraction of the best pane, where a full bar always means fastest,
 * whichever direction the underlying metric runs in.
 */
export function goodnessFraction(summary: PaneMetricSummary): number {
  if (summary.average == null) return 0
  // Measured, but nothing to be relative to: it is its own reference.
  if (summary.ratioToBest == null) return 1
  return Math.min(1, Math.max(0, 1 / summary.ratioToBest))
}
