import { describe, it, expect } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MessageStatsPanel } from './MessageStatsPanel'
import type { Message } from '../types'

/*
 * What the statistics panel claims about token counts.
 *
 * It summed tokenCount over the user's messages to get "prompt tokens". Nothing ever sets a
 * token count on a user message, so the answer was always 0 — printed as a measurement next to
 * a completion count that was real, and folded into a "total" that was therefore the completion
 * count wearing a different label. The conversation's own total is the only record of the prompt
 * side, so the prompt count is derived from it, and is absent when there is nothing to derive
 * it from.
 */

function message(overrides: Partial<Message>): Message {
  return {
    id: crypto.randomUUID(),
    conversationId: 'c1',
    role: 'Assistant',
    content: 'hello',
    tokenCount: null,
    logprobsData: null,
    perplexity: null,
    latencyMs: null,
    ttftMs: null,
    tokensPerSecond: null,
    finishReason: null,
    sortOrder: 0,
    createdAt: new Date().toISOString(),
    ...overrides,
  } as Message
}

function statValue(label: string): string {
  const cell = screen.getByText(label).nextElementSibling
  return cell?.textContent ?? ''
}

describe('MessageStatsPanel', () => {
  const conversation = [
    message({ role: 'User', content: 'is c# the best', sortOrder: 0 }),
    message({ role: 'Assistant', tokenCount: 768, sortOrder: 1 }),
  ]

  it('derives the prompt count from the conversation total', () => {
    render(<MessageStatsPanel messages={conversation} conversationTotalTokens={912} />)

    expect(statValue('Prompt tokens')).toBe('144')
    expect(statValue('Completion tokens')).toBe('768')
    expect(statValue('Total tokens')).toBe('912')
  })

  it('reports an unknown prompt count as absent rather than zero', () => {
    render(<MessageStatsPanel messages={conversation} />)

    expect(statValue('Prompt tokens')).toBe('--')
  })

  it('does not report a negative prompt count when the total lags behind', () => {
    // The conversation total is written when a message completes, so a total that has not
    // caught up must not turn into a negative number presented as a measurement.
    render(<MessageStatsPanel messages={conversation} conversationTotalTokens={100} />)

    expect(statValue('Prompt tokens')).toBe('--')
  })
})
