import { describe, it, expect } from 'vitest'
import { applyTemperature } from './temperature'
import type { TokenPredictionEntry } from './types'

/*
 * The temperature control on the Token Explorer used to do nothing.
 *
 * It was sent to the server, and the server returned the same distribution whatever it was:
 * the same prompt at 0, 1 and 2 came back identical to four significant figures, because
 * temperature decides which token gets sampled, not what the model computed. The page then
 * ignored the server's sampled token and stepped by taking the top entry itself. So the slider
 * moved, the numbers did not, and the label under it said "0 = greedy (deterministic)".
 */

function entry(token: string, probability: number): TokenPredictionEntry {
  return {
    token,
    probability,
    logprob: Math.log(probability),
    cumulativeProbability: 0,
  } as TokenPredictionEntry
}

/** Two tokens at 0.75 / 0.25 — a ratio of 3, which is easy to follow through the maths. */
const pair = [entry('a', 0.75), entry('b', 0.25)]

describe('applyTemperature', () => {
  it('returns the distribution unchanged at temperature 1', () => {
    expect(applyTemperature(pair, 1)).toBe(pair)
  })

  it('sharpens below 1', () => {
    // At T = 0.5 the odds square: 3:1 becomes 9:1, so 0.9 and 0.1.
    const [a, b] = applyTemperature(pair, 0.5)

    expect(a.probability).toBeCloseTo(0.9, 6)
    expect(b.probability).toBeCloseTo(0.1, 6)
  })

  it('flattens above 1', () => {
    // At T = 2 the odds take a square root: 3:1 becomes √3:1.
    const [a, b] = applyTemperature(pair, 2)
    const expectedTop = Math.sqrt(3) / (Math.sqrt(3) + 1)

    expect(a.probability).toBeCloseTo(expectedTop, 6)
    expect(b.probability).toBeCloseTo(1 - expectedTop, 6)
  })

  it('collapses onto the most likely token at zero', () => {
    const [a, b] = applyTemperature(pair, 0)

    expect(a.probability).toBeCloseTo(1, 6)
    expect(b.probability).toBe(0)
  })

  it('never reorders the alternatives, so stepping greedily is unaffected', () => {
    const many = [entry('a', 0.4), entry('b', 0.35), entry('c', 0.15), entry('d', 0.1)]

    for (const t of [0.1, 0.5, 1, 1.5, 2]) {
      const adjusted = applyTemperature(many, t)
      const order = [...adjusted].sort((x, y) => y.probability - x.probability).map((p) => p.token)

      expect(order).toEqual(['a', 'b', 'c', 'd'])
    }
  })

  it('keeps the mass that was returned, so a truncated distribution stays truncated', () => {
    // These three hold 80% of the distribution; reshaping must not invent the missing 20%,
    // because the panel's caveats about what was not returned are built on that number.
    const truncated = [entry('a', 0.5), entry('b', 0.2), entry('c', 0.1)]

    for (const t of [0.25, 0.5, 2]) {
      const mass = applyTemperature(truncated, t).reduce((sum, p) => sum + p.probability, 0)

      expect(mass).toBeCloseTo(0.8, 6)
    }
  })

  it('survives a very low temperature without overflowing', () => {
    const [a, b] = applyTemperature(pair, 0.01)

    expect(Number.isFinite(a.probability)).toBe(true)
    expect(a.probability).toBeCloseTo(1, 6)
    expect(b.probability).toBeCloseTo(0, 6)
  })

  it('handles an empty distribution', () => {
    expect(applyTemperature([], 0.5)).toEqual([])
  })
})
