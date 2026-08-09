import { describe, expect, it } from 'vitest'
import {
  calculateEntropy,
  calculatePerplexity,
  getTokenColor,
  isSurpriseToken,
  logprobToProb,
} from './logprobs'

/**
 * These functions are the product's thesis — every heatmap, entropy chart and surprise
 * highlight is derived from them — and they had no tests at all. A silent regression here
 * would not crash anything; it would quietly produce wrong numbers that a researcher then
 * reports as findings, which is the worst failure mode this codebase has.
 *
 * Asserted against hand-computable values rather than against the implementation's own output.
 */
describe('calculatePerplexity', () => {
  it('is exp(mean negative log-likelihood)', () => {
    // Two tokens each at logprob ln(0.5): mean NLL = ln 2, so perplexity = 2.
    const halfLog = Math.log(0.5)

    expect(calculatePerplexity([halfLog, halfLog])).toBeCloseTo(2, 10)
  })

  it('is 1 for a perfectly confident sequence', () => {
    // logprob 0 means probability 1: no surprise at all, so perplexity bottoms out at 1.
    expect(calculatePerplexity([0, 0, 0])).toBeCloseTo(1, 10)
  })

  it('rises as the model becomes less certain', () => {
    const confident = calculatePerplexity([Math.log(0.9), Math.log(0.9)])
    const unsure = calculatePerplexity([Math.log(0.2), Math.log(0.2)])

    expect(unsure).toBeGreaterThan(confident)
  })

  it('returns 0 for an empty sequence rather than NaN', () => {
    // exp(-0/0) is NaN, which would render as "NaN" in the UI.
    expect(calculatePerplexity([])).toBe(0)
  })
})

describe('calculateEntropy', () => {
  it('is 0 for a certain distribution', () => {
    expect(calculateEntropy([1])).toBeCloseTo(0, 10)
  })

  it('is 1 bit for a fair coin', () => {
    expect(calculateEntropy([0.5, 0.5])).toBeCloseTo(1, 10)
  })

  it('is 2 bits for four equally likely outcomes', () => {
    expect(calculateEntropy([0.25, 0.25, 0.25, 0.25])).toBeCloseTo(2, 10)
  })

  it('is measured in bits, not nats', () => {
    // The distinction matters when comparing against any other tool's numbers: the same
    // distribution is 1 bit or 0.693 nats. log2 is the reported unit here.
    expect(calculateEntropy([0.5, 0.5])).not.toBeCloseTo(Math.LN2, 3)
  })

  it('ignores zero-probability entries instead of producing NaN', () => {
    // 0 * log2(0) is defined as 0 in information theory, but computes as NaN.
    expect(calculateEntropy([0.5, 0.5, 0])).toBeCloseTo(1, 10)
  })

  it('falls as the distribution concentrates', () => {
    const flat = calculateEntropy([0.25, 0.25, 0.25, 0.25])
    const peaked = calculateEntropy([0.97, 0.01, 0.01, 0.01])

    expect(peaked).toBeLessThan(flat)
  })
})

describe('logprobToProb', () => {
  it('inverts the log', () => {
    expect(logprobToProb(Math.log(0.37))).toBeCloseTo(0.37, 10)
    expect(logprobToProb(0)).toBeCloseTo(1, 10)
  })
})

describe('isSurpriseToken', () => {
  it('flags tokens below the threshold and not those at or above it', () => {
    expect(isSurpriseToken(0.05)).toBe(true)
    expect(isSurpriseToken(0.5)).toBe(false)

    // Boundary: the threshold itself is not a surprise.
    expect(isSurpriseToken(0.1)).toBe(false)
  })

  it('honours a custom threshold', () => {
    expect(isSurpriseToken(0.3, 0.5)).toBe(true)
    expect(isSurpriseToken(0.3, 0.2)).toBe(false)
  })
})

describe('getTokenColor', () => {
  it('is monotonic — less likely tokens never look more confident', () => {
    // The heatmap's whole job is that colour tracks confidence. A non-monotonic mapping would
    // show a low-probability token in a reassuring colour.
    const rank = [
      'text-emerald-400',
      'text-emerald-300',
      'text-yellow-400',
      'text-orange-400',
      'text-red-400',
      'text-red-600',
    ]

    const logprobs = [0, -0.3, -0.75, -1.5, -2.5, -5]
    const indices = logprobs.map((lp) => rank.indexOf(getTokenColor(lp)))

    expect(indices).not.toContain(-1)

    for (let i = 1; i < indices.length; i++) {
      expect(indices[i]).toBeGreaterThanOrEqual(indices[i - 1])
    }
  })
})
