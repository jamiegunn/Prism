import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PaneComparisonSummary } from './PaneComparisonSummary'
import { compareMetric, formatRatio, comparePanes } from '../paneComparison'
import type { Message } from '../types'

/**
 * The point of this panel is that TTFT and tok/s are different questions: TTFT is
 * compute-bound and shows GPU acceleration, tok/s is memory-bandwidth-bound and often
 * ties. Everything below guards a way that distinction, or the honesty of the numbers
 * underneath it, could be quietly lost.
 */

let nextId = 0

function assistant(overrides: Partial<Message> = {}): Message {
  return {
    id: `m${nextId++}`,
    conversationId: 'c1',
    role: 'Assistant',
    content: 'response',
    tokenCount: null,
    logprobsData: null,
    perplexity: null,
    latencyMs: null,
    ttftMs: null,
    tokensPerSecond: null,
    finishReason: null,
    sortOrder: 0,
    createdAt: '2026-01-01T00:00:00Z',
    ...overrides,
  }
}

function user(): Message {
  return assistant({ role: 'User', content: 'prompt', ttftMs: 9999, tokensPerSecond: 9999 })
}

describe('averaging and sample size', () => {
  it('averages every response that carried the metric, not just the last one', () => {
    const result = compareMetric(
      [
        { paneId: 'a', label: 'A', messages: [assistant({ ttftMs: 100 }), assistant({ ttftMs: 300 })] },
        { paneId: 'b', label: 'B', messages: [assistant({ ttftMs: 400 })] },
      ],
      'ttftMs'
    )

    expect(result.panes[0].average).toBe(200)
    expect(result.panes[0].sampleCount).toBe(2)
    expect(result.panes[1].sampleCount).toBe(1)
  })

  it('averages over the measured responses only, so a missing sample cannot drag the mean down', () => {
    const result = compareMetric(
      [
        {
          paneId: 'a',
          label: 'A',
          messages: [assistant({ ttftMs: 100 }), assistant({ ttftMs: null }), assistant({ ttftMs: 200 })],
        },
        { paneId: 'b', label: 'B', messages: [assistant({ ttftMs: 100 })] },
      ],
      'ttftMs'
    )

    expect(result.panes[0].average).toBe(150)
    expect(result.panes[0].sampleCount).toBe(2)
  })

  it('ignores user messages', () => {
    const result = compareMetric(
      [{ paneId: 'a', label: 'A', messages: [user(), assistant({ ttftMs: 50 })] }],
      'ttftMs'
    )

    expect(result.panes[0].average).toBe(50)
    expect(result.panes[0].sampleCount).toBe(1)
  })

  it('shows the sample size in the rendered panel', () => {
    render(
      <PaneComparisonSummary
        panes={[
          { paneId: 'a', label: 'Native', messages: [assistant({ ttftMs: 100 }), assistant({ ttftMs: 300 })] },
          { paneId: 'b', label: 'Docker', messages: [assistant({ ttftMs: 400 })] },
        ]}
      />
    )

    expect(screen.getByText('n=2')).toBeInTheDocument()
    expect(screen.getByText('n=1')).toBeInTheDocument()
    expect(screen.getByText('200 ms')).toBeInTheDocument()
  })
})

describe('missing measurements', () => {
  it('reports no average and no samples when the metric was never populated', () => {
    const result = compareMetric(
      [{ paneId: 'a', label: 'A', messages: [assistant({ ttftMs: null })] }],
      'ttftMs'
    )

    expect(result.panes[0].average).toBeNull()
    expect(result.panes[0].sampleCount).toBe(0)
    expect(result.panes[0].ratioToBest).toBeNull()
    expect(result.panes[0].isBest).toBe(false)
    expect(result.measuredCount).toBe(0)
  })

  it('treats a zero as unset rather than as an unbeatable measurement', () => {
    const result = compareMetric(
      [
        { paneId: 'a', label: 'A', messages: [assistant({ ttftMs: 0 })] },
        { paneId: 'b', label: 'B', messages: [assistant({ ttftMs: 500 })] },
      ],
      'ttftMs'
    )

    expect(result.panes[0].average).toBeNull()
    expect(result.panes[1].isBest).toBe(false) // nothing left to beat
    expect(result.measuredCount).toBe(1)
  })

  it('discards NaN samples', () => {
    const result = compareMetric(
      [{ paneId: 'a', label: 'A', messages: [assistant({ tokensPerSecond: Number.NaN })] }],
      'tokensPerSecond'
    )

    expect(result.panes[0].average).toBeNull()
  })

  it('renders an em-dash and "not measured", never a zero or NaN', () => {
    const { container } = render(
      <PaneComparisonSummary
        panes={[
          { paneId: 'a', label: 'Native', messages: [assistant({ ttftMs: 100, tokensPerSecond: 40 })] },
          { paneId: 'b', label: 'Docker', messages: [assistant({ ttftMs: null, tokensPerSecond: 38 })] },
        ]}
      />
    )

    const text = container.textContent ?? ''
    expect(screen.getAllByText('not measured').length).toBeGreaterThan(0)
    expect(text).toContain('—')
    expect(text).not.toContain('NaN')
    expect(text).not.toContain('null')
    // The absent TTFT must not have been backfilled with a zero and rendered as a time.
    expect(text).not.toMatch(/(^|\D)0 ms/)
  })

  it('says so when only one pane reported a metric, instead of crowning a winner', () => {
    render(
      <PaneComparisonSummary
        panes={[
          { paneId: 'a', label: 'Native', messages: [assistant({ ttftMs: 100, tokensPerSecond: 40 })] },
          { paneId: 'b', label: 'Docker', messages: [assistant({ ttftMs: null, tokensPerSecond: 38 })] },
        ]}
      />
    )

    expect(
      screen.getByText(/Only one pane reported this metric/)
    ).toBeInTheDocument()
    // tok/s has two measured panes, so exactly one "best" marker is shown overall.
    expect(screen.getAllByText('best')).toHaveLength(1)
  })
})

describe('relative difference', () => {
  it('is withheld until two panes have real data for that metric', () => {
    const single = compareMetric(
      [
        { paneId: 'a', label: 'A', messages: [assistant({ ttftMs: 100 })] },
        { paneId: 'b', label: 'B', messages: [] },
      ],
      'ttftMs'
    )

    expect(single.panes[0].ratioToBest).toBeNull()
    expect(single.panes[0].isBest).toBe(false)
  })

  it('measures TTFT against the lowest pane', () => {
    const result = compareMetric(
      [
        { paneId: 'a', label: 'A', messages: [assistant({ ttftMs: 1200 })] },
        { paneId: 'b', label: 'B', messages: [assistant({ ttftMs: 400 })] },
      ],
      'ttftMs'
    )

    expect(result.panes[1].isBest).toBe(true)
    expect(result.panes[1].ratioToBest).toBe(1)
    expect(result.panes[0].ratioToBest).toBe(3)
  })

  it('measures throughput against the highest pane, inverting the direction', () => {
    const result = compareMetric(
      [
        { paneId: 'a', label: 'A', messages: [assistant({ tokensPerSecond: 20 })] },
        { paneId: 'b', label: 'B', messages: [assistant({ tokensPerSecond: 40 })] },
      ],
      'tokensPerSecond'
    )

    expect(result.panes[1].isBest).toBe(true)
    expect(result.panes[0].ratioToBest).toBe(2)
  })

  it('formats near-ties as a percentage and real gaps as a multiplier', () => {
    // The Apple Silicon case: shared memory bandwidth makes decode rates nearly equal,
    // and "1.0x slower" would hide that while "5% slower" states it.
    expect(formatRatio(1.05)).toBe('5% slower')
    expect(formatRatio(3)).toBe('3.0x slower')
    expect(formatRatio(1)).toBe('matched')
    expect(formatRatio(null)).toBeNull()
  })

  it('puts the percentage and the winner in the rendered panel', () => {
    render(
      <PaneComparisonSummary
        panes={[
          { paneId: 'a', label: 'Native', messages: [assistant({ ttftMs: 400, tokensPerSecond: 40 })] },
          { paneId: 'b', label: 'Docker', messages: [assistant({ ttftMs: 1200, tokensPerSecond: 38 })] },
        ]}
      />
    )

    expect(screen.getByText('3.0x slower')).toBeInTheDocument() // TTFT: GPU wins outright
    expect(screen.getByText('5% slower')).toBeInTheDocument() // tok/s: near-tie
  })
})

describe('when there is nothing worth showing', () => {
  it('renders nothing for a single pane', () => {
    const { container } = render(
      <PaneComparisonSummary
        panes={[{ paneId: 'a', label: 'Native', messages: [assistant({ ttftMs: 100 })] }]}
      />
    )

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing before any pane has responded', () => {
    const { container } = render(
      <PaneComparisonSummary
        panes={[
          { paneId: 'a', label: 'Native', messages: [] },
          { paneId: 'b', label: 'Docker', messages: [] },
        ]}
      />
    )

    expect(container).toBeEmptyDOMElement()
    expect(comparePanes([]).isEmpty).toBe(true)
  })
})
