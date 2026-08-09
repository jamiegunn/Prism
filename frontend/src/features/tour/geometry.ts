import type { Side } from './types'

/**
 * Where the spotlight hole and its callout go.
 *
 * Kept pure and free of the DOM because jsdom has no layout engine: every
 * `getBoundingClientRect()` in a test returns zeros, so geometry decided inside a component
 * cannot be tested at all. Passing rectangles in means the interesting cases — a target
 * against the bottom edge, a target wider than the viewport, no target at all — are ordinary
 * unit tests.
 */

/** A rectangle in viewport coordinates. */
export interface Rect {
  top: number
  left: number
  width: number
  height: number
}

/** The visible area the callout has to stay inside. */
export interface Viewport {
  width: number
  height: number
}

/** Where a callout ends up, and which side it settled on. */
export interface Placement {
  top: number
  left: number
  side: Side
  /** True when there was no target and the callout is centred. */
  centred: boolean
}

/** Gap between the spotlight and the callout. */
const GAP = 12

/** Smallest distance the callout keeps from the viewport edge. */
const MARGIN = 16

/**
 * Grows the target rectangle slightly so the spotlight does not clip its subject.
 *
 * @param target The element's rectangle, or null when there is nothing to point at.
 * @param padding Extra space around the element.
 * @returns The padded rectangle, or null.
 */
export function spotlightRect(target: Rect | null, padding = 6): Rect | null {
  if (!target) return null

  return {
    top: target.top - padding,
    left: target.left - padding,
    width: target.width + padding * 2,
    height: target.height + padding * 2,
  }
}

/**
 * How much room a side has, given a target and a viewport.
 *
 * @param side The candidate side.
 * @param target The spotlight rectangle.
 * @param viewport The visible area.
 * @returns Available pixels along that side's axis.
 */
function roomOn(side: Side, target: Rect, viewport: Viewport): number {
  switch (side) {
    case 'top':
      return target.top
    case 'bottom':
      return viewport.height - (target.top + target.height)
    case 'left':
      return target.left
    case 'right':
      return viewport.width - (target.left + target.width)
  }
}

/**
 * Whether a callout fits on a side without being clamped away from the target.
 *
 * @param side The candidate side.
 * @param target The spotlight rectangle.
 * @param callout The callout's measured size.
 * @param viewport The visible area.
 * @returns True when it fits.
 */
function fitsOn(side: Side, target: Rect, callout: Rect, viewport: Viewport): boolean {
  const needed = side === 'top' || side === 'bottom' ? callout.height : callout.width
  return roomOn(side, target, viewport) >= needed + GAP + MARGIN
}

/**
 * Keeps a value inside a range, preferring the low end when the range is inverted.
 *
 * @param value The proposed coordinate.
 * @param min Lowest allowed.
 * @param max Highest allowed.
 * @returns The clamped coordinate.
 */
function clamp(value: number, min: number, max: number): number {
  if (max < min) return min
  return Math.min(Math.max(value, min), max)
}

/**
 * Chooses a position for the callout beside the spotlight.
 *
 * Falls back through the sides that actually have room rather than trusting the preference,
 * because a step that prefers "bottom" on a target near the bottom edge would otherwise put
 * its own instructions off-screen.
 *
 * @param target The spotlight rectangle, or null for a step with no anchor.
 * @param callout The callout's measured size.
 * @param viewport The visible area.
 * @param preferred The side the step asked for.
 * @returns Coordinates, the side used, and whether it had to centre.
 */
export function placeCallout(
  target: Rect | null,
  callout: Rect,
  viewport: Viewport,
  preferred: Side = 'bottom'
): Placement {
  const centred: Placement = {
    top: Math.max(MARGIN, (viewport.height - callout.height) / 2),
    left: Math.max(MARGIN, (viewport.width - callout.width) / 2),
    side: preferred,
    centred: true,
  }

  if (!target) return centred

  const candidates: Side[] = [...new Set<Side>([preferred, 'bottom', 'top', 'right', 'left'])]

  const side =
    candidates.find((candidate) => fitsOn(candidate, target, callout, viewport)) ??
    // Nothing fits cleanly, so take whichever side has the most room and let the clamping
    // below keep the callout on screen. Overlapping the target beats vanishing off it.
    candidates.reduce((best, candidate) =>
      roomOn(candidate, target, viewport) > roomOn(best, target, viewport) ? candidate : best
    )

  const maxLeft = viewport.width - callout.width - MARGIN
  const maxTop = viewport.height - callout.height - MARGIN

  if (side === 'top' || side === 'bottom') {
    const top =
      side === 'top'
        ? target.top - callout.height - GAP
        : target.top + target.height + GAP

    return {
      top: clamp(top, MARGIN, maxTop),
      left: clamp(target.left + target.width / 2 - callout.width / 2, MARGIN, maxLeft),
      side,
      centred: false,
    }
  }

  const left =
    side === 'left' ? target.left - callout.width - GAP : target.left + target.width + GAP

  return {
    top: clamp(target.top + target.height / 2 - callout.height / 2, MARGIN, maxTop),
    left: clamp(left, MARGIN, maxLeft),
    side,
    centred: false,
  }
}
