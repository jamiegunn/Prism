import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ChatPane } from './ChatPane'
import type { Message } from '../types'

/**
 * The Playground shows newest first, which is the opposite of a normal chat window.
 *
 * That is deliberate. A chat log is read forwards because you are following a conversation;
 * here you are usually re-reading one long response and comparing it against the last, and
 * having the thing you just generated scroll away below the fold is the wrong default.
 *
 * It is also the kind of decision that gets quietly reverted by anyone who assumes the
 * conventional order was intended, hence a test rather than a comment.
 */

function message(overrides: Partial<Message> = {}): Message {
  return {
    id: 'm1',
    conversationId: 'c1',
    role: 'Assistant',
    content: 'content',
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

/** Where a string first appears in the rendered output. */
function positionOf(container: HTMLElement, text: string): number {
  const index = (container.textContent ?? '').indexOf(text)
  expect(index, `"${text}" was not rendered`).toBeGreaterThanOrEqual(0)
  return index
}

describe('ChatPane ordering', () => {
  it('renders the newest message above the oldest', () => {
    const { container } = render(
      <ChatPane
        messages={[
          message({ id: '1', content: 'OLDEST' }),
          message({ id: '2', content: 'MIDDLE' }),
          message({ id: '3', content: 'NEWEST' }),
        ]}
        streamingContent=""
        streamingTokens={[]}
        isStreaming={false}
      />,
    )

    const newest = positionOf(container, 'NEWEST')
    const middle = positionOf(container, 'MIDDLE')
    const oldest = positionOf(container, 'OLDEST')

    expect(newest).toBeLessThan(middle)
    expect(middle).toBeLessThan(oldest)
  })

  it('puts the in-flight response above everything, where you are already looking', () => {
    const { container } = render(
      <ChatPane
        messages={[message({ id: '1', content: 'PREVIOUS' })]}
        streamingContent="ARRIVING"
        streamingTokens={[]}
        isStreaming
      />,
    )

    expect(positionOf(container, 'ARRIVING')).toBeLessThan(positionOf(container, 'PREVIOUS'))
  })

  it('does not mutate the array it was given', () => {
    // reverse() sorts in place. Reversing the prop directly would reorder the caller's state
    // on every render, which shows up as messages shuffling rather than as an obvious error.
    const messages = [
      message({ id: '1', content: 'FIRST' }),
      message({ id: '2', content: 'SECOND' }),
    ]

    render(
      <ChatPane messages={messages} streamingContent="" streamingTokens={[]} isStreaming={false} />,
    )

    expect(messages.map((m) => m.id)).toEqual(['1', '2'])
  })

  it('still shows the empty state when there is nothing to order', () => {
    render(
      <ChatPane messages={[]} streamingContent="" streamingTokens={[]} isStreaming={false} />,
    )

    expect(screen.getByText('Start a conversation')).toBeInTheDocument()
  })
})
