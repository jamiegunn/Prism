import { describe, it, expect } from 'vitest'
import { findFirstDivergence } from './BranchDiffView'
import type { BranchToken } from '../types'

/*
 * Where two branch continuations part company.
 *
 * The search started at index 1, on the reasoning that position 0 is the forced token and always
 * differs. What is compared here are the tokens generated *after* the forced one, so index 0 is
 * an ordinary position — two branches that parted immediately were reported as diverging one row
 * below the row that visibly differed. Finding no divergence at all also left the answer at 0,
 * which reads as "parted at once".
 */

function tokens(...values: string[]): BranchToken[] {
  return values.map((token) => ({
    token,
    logprob: -0.5,
    probability: 0.6,
    topAlternatives: [],
  })) as BranchToken[]
}

describe('findFirstDivergence', () => {
  it('finds a divergence at the very first token', () => {
    expect(findFirstDivergence(tokens('the', 'French'), tokens('The', 'capital'))).toBe(0)
  })

  it('finds a divergence after a shared opening', () => {
    expect(findFirstDivergence(tokens('a', 'b', 'c'), tokens('a', 'b', 'z'))).toBe(2)
  })

  it('reports no divergence when the continuations match', () => {
    expect(findFirstDivergence(tokens('a', 'b'), tokens('a', 'b'))).toBeNull()
  })

  it('treats the end of a shorter branch as the divergence', () => {
    // One branch stopped and the other kept going: they part where the first ran out.
    expect(findFirstDivergence(tokens('a', 'b'), tokens('a', 'b', 'c'))).toBe(2)
  })

  it('handles empty branches', () => {
    expect(findFirstDivergence([], [])).toBeNull()
    expect(findFirstDivergence([], tokens('a'))).toBe(0)
  })
})
