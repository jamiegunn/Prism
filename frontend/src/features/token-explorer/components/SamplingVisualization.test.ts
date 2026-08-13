import { describe, it, expect } from 'vitest'
import { computeStats } from './SamplingVisualization'
import type { TokenPredictionEntry } from '../types'

/*
 * What the Sampling Analysis panel is allowed to claim.
 *
 * A server returns the top N alternatives for a position, never the whole vocabulary. Every
 * statistic here is therefore measured over a truncated distribution, and the panel used to
 * report them as though it were not: the probability mass of the 20 tokens it had was labelled
 * "in top-50", which is a statement about 30 tokens nobody looked at, and entropy over 97% of
 * the mass was printed as the entropy rather than the floor it is.
 */

function entry(token: string, probability: number): TokenPredictionEntry {
  return {
    token,
    probability,
    logprob: Math.log(probability),
    cumulativeProbability: 0,
  } as TokenPredictionEntry
}

describe('computeStats', () => {
  /*
   * A hand-worked distribution. Four tokens at 0.5, 0.25, 0.125, 0.125 sum to exactly 1, and
   * its entropy is -(0.5·log2 0.5 + 0.25·log2 0.25 + 2 · 0.125·log2 0.125)
   *          = -(0.5·-1 + 0.25·-2 + 0.25·-3) = 0.5 + 0.5 + 0.75 = 1.75 bits.
   */
  const complete = [entry('a', 0.5), entry('b', 0.25), entry('c', 0.125), entry('d', 0.125)]

  it('computes entropy of a known distribution', () => {
    const stats = computeStats(complete, 0.9, 4)

    expect(stats.entropy).toBeCloseTo(1.75, 6)
    expect(stats.isTruncated).toBe(false)
    expect(stats.coveredMass).toBeCloseTo(1, 6)
  })

  it('reports a complete distribution without hedging', () => {
    const stats = computeStats(complete, 0.9, 4)

    expect(stats.topKMeasurable).toBe(true)
    expect(stats.topKMass).toBeCloseTo(1, 6)
    expect(stats.topPReached).toBe(true)
    // 0.5 + 0.25 + 0.125 = 0.875, still short of 0.9, so the nucleus is all four.
    expect(stats.topPTokenCount).toBe(4)
  })

  it('knows when the returned alternatives do not cover the whole distribution', () => {
    // The tail is missing: these are the three the server chose to return.
    const truncated = [entry('a', 0.5), entry('b', 0.2), entry('c', 0.1)]

    const stats = computeStats(truncated, 0.9, 50)

    expect(stats.isTruncated).toBe(true)
    expect(stats.coveredMass).toBeCloseTo(0.8, 6)
  })

  it('will not claim a top-k it was not given the tokens to measure', () => {
    const truncated = [entry('a', 0.5), entry('b', 0.2), entry('c', 0.1)]

    const stats = computeStats(truncated, 0.9, 50)

    // The honest k is the one there is data for, and the mass is a floor for the k asked about.
    expect(stats.topKMeasurable).toBe(false)
    expect(stats.topKActual).toBe(3)
    expect(stats.topKMass).toBeCloseTo(0.8, 6)
  })

  it('says when the nucleus is not reached within the returned alternatives', () => {
    const flat = Array.from({ length: 5 }, (_, i) => entry(`t${i}`, 0.1))

    const stats = computeStats(flat, 0.9, 5)

    // Five tokens at 0.1 reach 0.5, so the p=0.9 nucleus is somewhere past what was returned.
    expect(stats.topPReached).toBe(false)
    expect(stats.topPMass).toBeCloseTo(0.5, 6)
  })

  it('counts tokens over 1% exactly only when the smallest returned is under 1%', () => {
    const exact = computeStats([entry('a', 0.9), entry('b', 0.005)], 0.9, 2)
    expect(exact.effectiveVocab).toBe(1)
    expect(exact.effectiveVocabIsExact).toBe(true)

    // Every alternative returned is above 1%, so tokens beyond them may be too.
    const floor = computeStats([entry('a', 0.5), entry('b', 0.3), entry('c', 0.2)], 0.9, 3)
    expect(floor.effectiveVocab).toBe(3)
    expect(floor.effectiveVocabIsExact).toBe(false)
  })

  it('does not hedge a top-k when the returned alternatives already hold all the mass', () => {
    // Two tokens summing to 1 answer "what is in the top 50" completely: there is nothing else.
    const stats = computeStats([entry('a', 0.99), entry('b', 0.01)], 0.9, 50)

    expect(stats.topKMeasurable).toBe(true)
    expect(stats.topKMass).toBeCloseTo(1, 6)
  })

  it('does not call rounding a truncation', () => {
    // Probabilities that sum to just under 1 through rounding alone must not be reported as a
    // truncated distribution, or every complete answer would carry a caveat it has not earned.
    const rounded = [entry('a', 0.6667), entry('b', 0.3333)]

    expect(computeStats(rounded, 0.9, 2).isTruncated).toBe(false)
  })
})
