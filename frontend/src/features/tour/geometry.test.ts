import { describe, it, expect } from 'vitest'
import { placeCallout, spotlightRect, type Rect, type Viewport } from './geometry'

/*
 * Tour placement, tested as arithmetic.
 *
 * jsdom has no layout engine — every getBoundingClientRect() returns zeros — so geometry
 * decided inside the overlay component would be untestable, and the cases that actually
 * break a tour are all positional: an anchor hard against the bottom edge, an anchor wider
 * than the viewport, a step with no anchor at all. Keeping the maths in a pure function is
 * what makes those ordinary assertions instead of a manual check in a browser.
 */

const viewport: Viewport = { width: 1280, height: 800 }
const callout: Rect = { top: 0, left: 0, width: 360, height: 216 }

/** A target somewhere near the middle, with room on every side. */
const roomy: Rect = { top: 300, left: 500, width: 200, height: 100 }

const MARGIN = 16

describe('spotlightRect', () => {
  it('is null when there is nothing to point at', () => {
    expect(spotlightRect(null)).toBeNull()
  })

  it('grows the target so the ring does not clip its subject', () => {
    expect(spotlightRect({ top: 100, left: 200, width: 50, height: 20 }, 6)).toEqual({
      top: 94,
      left: 194,
      width: 62,
      height: 32,
    })
  })
})

describe('placeCallout', () => {
  it('centres when the step has no anchor', () => {
    const placement = placeCallout(null, callout, viewport)

    expect(placement.centred).toBe(true)
    expect(placement.left).toBe((1280 - 360) / 2)
    expect(placement.top).toBe((800 - 216) / 2)
  })

  it('honours the preferred side when it fits', () => {
    expect(placeCallout(roomy, callout, viewport, 'right').side).toBe('right')
    expect(placeCallout(roomy, callout, viewport, 'top').side).toBe('top')
  })

  it('centres the callout on the target along the shared axis', () => {
    const placement = placeCallout(roomy, callout, viewport, 'bottom')

    // Target centre is 600; the callout is 360 wide, so it starts at 420.
    expect(placement.left).toBe(420)
    expect(placement.top).toBe(roomy.top + roomy.height + 12)
  })

  it('moves to another side rather than going off the bottom edge', () => {
    const nearBottom: Rect = { top: 740, left: 500, width: 200, height: 40 }

    const placement = placeCallout(nearBottom, callout, viewport, 'bottom')

    expect(placement.side).not.toBe('bottom')
    expect(placement.top + callout.height).toBeLessThanOrEqual(viewport.height)
  })

  it('keeps a callout on screen for a target hard against the left edge', () => {
    const nearLeft: Rect = { top: 300, left: 0, width: 64, height: 64 }

    const placement = placeCallout(nearLeft, callout, viewport, 'left')

    expect(placement.left).toBeGreaterThanOrEqual(MARGIN)
    expect(placement.side).not.toBe('left')
  })

  it('stays inside the viewport even when no side has room', () => {
    // A target filling the screen: every side is too tight, so it must clamp rather than
    // place the instructions somewhere nobody can read them.
    const huge: Rect = { top: 0, left: 0, width: 1280, height: 800 }

    const placement = placeCallout(huge, callout, viewport, 'bottom')

    expect(placement.left).toBeGreaterThanOrEqual(MARGIN)
    expect(placement.top).toBeGreaterThanOrEqual(MARGIN)
    expect(placement.left + callout.width).toBeLessThanOrEqual(viewport.width)
    expect(placement.top + callout.height).toBeLessThanOrEqual(viewport.height)
  })

  it('does not produce negative coordinates on a viewport smaller than the callout', () => {
    const tiny: Viewport = { width: 320, height: 200 }

    const placement = placeCallout(null, callout, tiny)

    expect(placement.top).toBeGreaterThanOrEqual(0)
    expect(placement.left).toBeGreaterThanOrEqual(0)
  })
})
