import { describe, it, expect } from 'vitest'
import { diffWords } from './diff'

/** The words a side reports as differing, in order. */
function changedWords(segments: { text: string; changed: boolean }[]): string[] {
  return segments
    .filter((s) => s.changed)
    .flatMap((s) => s.text.split(/\s+/))
    .filter((w) => w.length > 0)
}

/** Reassembles a side, which must always give back exactly the text that went in. */
function rejoin(segments: { text: string }[]): string {
  return segments.map((s) => s.text).join('')
}

describe('diffWords', () => {
  it('reports nothing changed when the texts match', () => {
    const diff = diffWords('the capital is Paris', 'the capital is Paris')

    expect(changedWords(diff.original)).toEqual([])
    expect(changedWords(diff.replay)).toEqual([])
  })

  it('marks only the word that differs', () => {
    const diff = diffWords('the capital is Paris', 'the capital is Berlin')

    expect(changedWords(diff.original)).toEqual(['Paris'])
    expect(changedWords(diff.replay)).toEqual(['Berlin'])
  })

  it('marks only the inserted word, not everything after it', () => {
    // The positional comparison this replaced marked every word after an insertion as
    // changed, so one extra word painted both responses red end to end.
    const diff = diffWords('the capital is Paris', 'the capital city is Paris')

    expect(changedWords(diff.original)).toEqual([])
    expect(changedWords(diff.replay)).toEqual(['city'])
  })

  it('marks a deletion on the side it was deleted from', () => {
    const diff = diffWords('the capital city is Paris', 'the capital is Paris')

    expect(changedWords(diff.original)).toEqual(['city'])
    expect(changedWords(diff.replay)).toEqual([])
  })

  it('handles an empty original', () => {
    const diff = diffWords('', 'a reply')

    expect(diff.original).toEqual([])
    expect(changedWords(diff.replay)).toEqual(['a', 'reply'])
  })

  it('preserves the exact text of both sides', () => {
    const original = '  The  capital\nis Paris. '
    const replay = 'The capital\n\nis Berlin.'

    const diff = diffWords(original, replay)

    expect(rejoin(diff.original)).toBe(original)
    expect(rejoin(diff.replay)).toBe(replay)
  })

  it('keeps the common middle when both ends differ', () => {
    // Neither end matches, so nothing can be trimmed and the subsequence match itself has to
    // find the shared run. Without it every word is reported as changed.
    const diff = diffWords('alpha one two three omega', 'beta one two three gamma')

    expect(changedWords(diff.original)).toEqual(['alpha', 'omega'])
    expect(changedWords(diff.replay)).toEqual(['beta', 'gamma'])
  })

  it('reports an insertion between two differing ends as just the insertion', () => {
    const diff = diffWords('alpha one two omega', 'beta one extra two gamma')

    expect(changedWords(diff.original)).toEqual(['alpha', 'omega'])
    expect(changedWords(diff.replay)).toEqual(['beta', 'extra', 'gamma'])
  })

  it('keeps emoji, accents and RTL text intact', () => {
    // Splitting on whitespace must not split inside a grapheme, and a bidi mark must survive
    // the round trip — a diff that mangles the text it is describing is worse than no diff.
    const original = 'الطقس اليوم 🌤️ جميل café'
    const replay = 'الطقس اليوم 🌧️ جميل café'

    const diff = diffWords(original, replay)

    expect(rejoin(diff.original)).toBe(original)
    expect(rejoin(diff.replay)).toBe(replay)
    expect(changedWords(diff.original)).toEqual(['🌤️'])
    expect(changedWords(diff.replay)).toEqual(['🌧️'])
  })

  it('treats one long unbroken string as a single differing word', () => {
    const original = `a${'x'.repeat(5000)}`
    const replay = `b${'x'.repeat(5000)}`

    const diff = diffWords(original, replay)

    expect(diff.original).toEqual([{ text: original, changed: true }])
    expect(diff.replay).toEqual([{ text: replay, changed: true }])
  })

  it('degrades to a whole-block difference rather than failing on two enormous responses', () => {
    // Past the table cap the differing region is reported as one block. What must not change is
    // that both sides still read back exactly, so the panes never show text nobody generated.
    const original = Array.from({ length: 2200 }, (_, i) => `a${i}`).join(' ')
    const replay = Array.from({ length: 2200 }, (_, i) => `b${i}`).join(' ')

    const diff = diffWords(original, replay)

    expect(rejoin(diff.original)).toBe(original)
    expect(rejoin(diff.replay)).toBe(replay)
  })

  it('finds the difference in the middle of long, mostly identical responses', () => {
    const words = Array.from({ length: 400 }, (_, i) => `w${i}`)
    const changed = [...words]
    changed[200] = 'DIFFERENT'

    const diff = diffWords(words.join(' '), changed.join(' '))

    expect(changedWords(diff.original)).toEqual(['w200'])
    expect(changedWords(diff.replay)).toEqual(['DIFFERENT'])
  })
})
