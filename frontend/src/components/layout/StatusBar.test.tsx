import { describe, expect, it } from 'vitest'
import { describeConnection, describeLogprobs } from './StatusBar'

/**
 * The status bar previously hardcoded every value: a permanently green dot labelled
 * "Connected", the literal text "No model loaded", and an em dash for GPU. It reported a
 * healthy connection with the backend switched off.
 *
 * These tests pin the property that makes it a status bar at all — that what it says is
 * derived from state, and in particular that it can say something other than "Connected".
 */
describe('describeConnection', () => {
  it('reports the backend being unreachable rather than claiming a connection', () => {
    const result = describeConnection({ isLoading: false, isError: true, hasInstance: false })

    expect(result.label).toMatch(/unreachable/i)
    expect(result.dotClass).toContain('red')
  })

  it('distinguishes "still loading" from "connected"', () => {
    const result = describeConnection({ isLoading: true, isError: false, hasInstance: false })

    expect(result.label).not.toMatch(/^Connected$/)
    expect(result.dotClass).not.toContain('emerald')
  })

  it('does not claim a connection when no instance is selected', () => {
    const result = describeConnection({ isLoading: false, isError: false, hasInstance: false })

    expect(result.label).toMatch(/no instance/i)
    expect(result.dotClass).not.toContain('emerald')
  })

  it('reports green only for a healthy instance', () => {
    const result = describeConnection({
      isLoading: false,
      isError: false,
      hasInstance: true,
      instance: { status: 'Online' },
    })

    expect(result.label).toBe('Connected')
    expect(result.dotClass).toContain('emerald')
  })

  it('reports an offline instance as unreachable', () => {
    const result = describeConnection({
      isLoading: false,
      isError: false,
      hasInstance: true,
      instance: { status: 'Offline' },
    })

    expect(result.label).toMatch(/unreachable/i)
    expect(result.dotClass).toContain('red')
  })

  it('says unknown rather than guessing when the status has not been probed', () => {
    const result = describeConnection({
      isLoading: false,
      isError: false,
      hasInstance: true,
      instance: { status: 'Unknown' },
    })

    expect(result.label).toMatch(/unknown/i)
    expect(result.dotClass).not.toContain('emerald')
  })
})

describe('describeLogprobs', () => {
  it('distinguishes unprobed from unavailable', () => {
    // These are different facts: one means we have not asked, the other means we asked and the
    // answer was no. Collapsing them would tell a researcher a capability is missing when it
    // may simply be unknown.
    expect(describeLogprobs(undefined, true)).toMatch(/unprobed/i)
    expect(describeLogprobs(false, true)).toMatch(/unavailable/i)
    expect(describeLogprobs(true, true)).toMatch(/available/i)
  })

  it('shows nothing meaningful when no instance is selected', () => {
    expect(describeLogprobs(true, false)).toBe('—')
  })
})
