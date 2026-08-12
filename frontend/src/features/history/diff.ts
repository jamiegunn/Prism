/**
 * Word-level diff between two responses.
 *
 * The comparison exists so a researcher can see where a replay departed from the original.
 * Comparing the two texts word-by-position — which is what this replaced — reports every word
 * after an inserted one as changed, so a single extra "the" paints both responses red and the
 * screen claims a difference that is not there. Matching a longest common subsequence instead
 * marks only the words that genuinely differ.
 */

/** One run of text on one side of the comparison, and whether it is part of a difference. */
export interface DiffSegment {
  /** The text of this run, including any trailing whitespace. */
  text: string
  /** True when this run has no counterpart on the other side. */
  changed: boolean
}

/** Both sides of a comparison, aligned. */
export interface WordDiff {
  /** Segments making up the original text, in order. */
  original: DiffSegment[]
  /** Segments making up the replay text, in order. */
  replay: DiffSegment[]
}

/**
 * The largest subsequence table this will build. Two ~2,000-word responses fit comfortably;
 * beyond that the quadratic table costs more than the detail is worth, so the differing middle
 * is reported as one block rather than word by word. Stated here rather than hidden, because a
 * diff that quietly degrades is a diff that quietly misleads.
 */
const MAX_TABLE_CELLS = 4_000_000

/** Splits text into words and the whitespace between them, keeping both. */
function tokenize(text: string): string[] {
  return text.length === 0 ? [] : text.split(/(\s+)/).filter((t) => t.length > 0)
}

/** Joins adjacent tokens that share a changed flag, so the DOM gets one span per run. */
function coalesce(tokens: string[], changed: boolean[]): DiffSegment[] {
  const segments: DiffSegment[] = []
  for (let i = 0; i < tokens.length; i++) {
    const last = segments[segments.length - 1]
    if (last && last.changed === changed[i]) {
      last.text += tokens[i]
    } else {
      segments.push({ text: tokens[i], changed: changed[i] })
    }
  }
  return segments
}

/**
 * Computes a word-level diff of two texts.
 *
 * @param original The text the original call returned.
 * @param replay The text the replay returned.
 * @returns Aligned segments for each side; identical inputs yield one unchanged segment each.
 */
export function diffWords(original: string, replay: string): WordDiff {
  const a = tokenize(original)
  const b = tokenize(replay)

  const changedA = new Array<boolean>(a.length).fill(true)
  const changedB = new Array<boolean>(b.length).fill(true)

  // Identical head and tail are the common case for a low-temperature replay; trimming them
  // first keeps the table small enough to build for everything but the wildest divergence.
  let head = 0
  while (head < a.length && head < b.length && a[head] === b[head]) {
    changedA[head] = false
    changedB[head] = false
    head++
  }

  let tail = 0
  while (
    tail < a.length - head &&
    tail < b.length - head &&
    a[a.length - 1 - tail] === b[b.length - 1 - tail]
  ) {
    changedA[a.length - 1 - tail] = false
    changedB[b.length - 1 - tail] = false
    tail++
  }

  const midA = a.slice(head, a.length - tail)
  const midB = b.slice(head, b.length - tail)

  if (midA.length > 0 && midB.length > 0) {
    if ((midA.length + 1) * (midB.length + 1) <= MAX_TABLE_CELLS) {
      // Longest common subsequence lengths, then walk back through the table marking matches.
      const width = midB.length + 1
      const lcs = new Uint32Array((midA.length + 1) * width)

      for (let i = midA.length - 1; i >= 0; i--) {
        for (let j = midB.length - 1; j >= 0; j--) {
          lcs[i * width + j] =
            midA[i] === midB[j]
              ? lcs[(i + 1) * width + (j + 1)] + 1
              : Math.max(lcs[(i + 1) * width + j], lcs[i * width + (j + 1)])
        }
      }

      let i = 0
      let j = 0
      while (i < midA.length && j < midB.length) {
        if (midA[i] === midB[j]) {
          changedA[head + i] = false
          changedB[head + j] = false
          i++
          j++
        } else if (lcs[(i + 1) * width + j] >= lcs[i * width + (j + 1)]) {
          i++
        } else {
          j++
        }
      }
    }
    // Over the cap, the trimmed middle stays marked as changed on both sides.
  }

  return {
    original: coalesce(a, changedA),
    replay: coalesce(b, changedB),
  }
}
