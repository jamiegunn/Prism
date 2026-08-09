import { describe, it, expect } from 'vitest'
import { describeOffer, describeOffers, shouldAutoStart } from './selection'
import { allTours, findTour, WELCOME_TOUR_ID } from './tours'
import type { Tour, TourContext } from './types'

/*
 * What gets offered, and when the tour opens by itself.
 *
 * Two failures are worth guarding against. Offering a walkthrough into a page that has no
 * data — History before anything has run, token views on a server that returns no
 * probabilities — teaches a newcomer that the tool is broken, which is the opposite of what
 * onboarding is for. And a tour that comes back after being dismissed is the single behaviour
 * that makes people resent onboarding, so leaving one has to count as having seen it.
 */

const emptyInstall: TourContext = { hasProvider: false, hasLogprobs: false, hasHistory: false }
const workingInstall: TourContext = { hasProvider: true, hasLogprobs: true, hasHistory: true }

const needsLogprobs: Tour = {
  id: 'test-logprobs',
  kind: 'situation',
  title: 'Test',
  outcome: 'Test',
  minutes: 1,
  requires: ['logprobs'],
  steps: [{ id: 'only', title: 'Only', body: 'Body' }],
}

describe('describeOffer', () => {
  it('offers a walkthrough with no requirements on a bare install', () => {
    const welcome = findTour(WELCOME_TOUR_ID)!

    expect(describeOffer(welcome, emptyInstall).available).toBe(true)
  })

  it('blocks one whose requirement is unmet, and says which', () => {
    const offer = describeOffer(needsLogprobs, emptyInstall)

    expect(offer.available).toBe(false)
    expect(offer.blockedReason).toMatch(/token probabilities/i)
  })

  it('reports the first unmet requirement rather than the last', () => {
    const needsBoth: Tour = { ...needsLogprobs, requires: ['provider', 'logprobs'] }

    expect(describeOffer(needsBoth, emptyInstall).blockedReason).toMatch(/connect a model/i)
  })

  it('unblocks once the requirement is satisfied', () => {
    const offer = describeOffer(needsLogprobs, workingInstall)

    expect(offer.available).toBe(true)
    expect(offer.blockedReason).toBeNull()
  })
})

describe('describeOffers', () => {
  it('keeps blocked walkthroughs in the list rather than hiding them', () => {
    // Hiding them would make the guide emptiest on a fresh install, which is exactly when
    // someone needs to see what the tool can do.
    const offers = describeOffers(allTours, emptyInstall)

    expect(offers).toHaveLength(allTours.length)
    expect(offers.some((offer) => !offer.available)).toBe(true)
  })

  it('offers everything once the app is fully set up', () => {
    const offers = describeOffers(allTours, workingInstall)

    expect(offers.every((offer) => offer.available)).toBe(true)
  })
})

describe('shouldAutoStart', () => {
  it('starts on a fresh install', () => {
    expect(
      shouldAutoStart({ completedTourIds: [], autoStartEnabled: true }, WELCOME_TOUR_ID)
    ).toBe(true)
  })

  it('never starts again once the tour has been seen', () => {
    expect(
      shouldAutoStart(
        { completedTourIds: [WELCOME_TOUR_ID], autoStartEnabled: true },
        WELCOME_TOUR_ID
      )
    ).toBe(false)
  })

  it('respects the switch even on a fresh install', () => {
    expect(
      shouldAutoStart({ completedTourIds: [], autoStartEnabled: false }, WELCOME_TOUR_ID)
    ).toBe(false)
  })

  it('is not confused by other walkthroughs having been completed', () => {
    expect(
      shouldAutoStart(
        { completedTourIds: ['compare-servers', 'look-back'], autoStartEnabled: true },
        WELCOME_TOUR_ID
      )
    ).toBe(true)
  })
})

describe('the shipped walkthroughs', () => {
  it('all have steps, and every step has something to say', () => {
    for (const tour of allTours) {
      expect(tour.steps.length, `${tour.id} has no steps`).toBeGreaterThan(0)

      for (const step of tour.steps) {
        expect(step.title.length, `${tour.id}/${step.id} has no title`).toBeGreaterThan(0)
        expect(step.body.length, `${tour.id}/${step.id} has no body`).toBeGreaterThan(0)
      }
    }
  })

  it('use unique ids, since the store persists them', () => {
    const ids = allTours.map((tour) => tour.id)

    expect(new Set(ids).size).toBe(ids.length)
  })

  it('declare a requirement for every step that needs data to exist', () => {
    // A walkthrough that visits History or the token views without declaring it needs them
    // will land a newcomer on an empty page. This is the check that catches a new step
    // being added to an old tour without revisiting its requirements.
    const dataRoutes: Record<string, string> = {
      '/history': 'history',
      '/token-explorer': 'logprobs',
    }

    for (const tour of allTours) {
      for (const step of tour.steps) {
        const required = step.route ? dataRoutes[step.route] : undefined
        if (!required) continue

        expect(
          tour.requires,
          `${tour.id} visits ${step.route} without requiring '${required}'`
        ).toContain(required)
      }
    }
  })
})
