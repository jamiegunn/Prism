import type { Requirement, Tour, TourContext } from './types'

/**
 * Decides what to offer and when to offer it.
 *
 * Separated from the components because these are the rules worth pinning: a walkthrough
 * offered into an empty page teaches the reader that the tool is broken, and a tour that
 * reappears after being dismissed is worse than no tour at all.
 */

/** A walkthrough plus whether it is worth starting right now. */
export interface TourOffer {
  tour: Tour
  available: boolean
  /** Why not, phrased for the reader. Null when available. */
  blockedReason: string | null
}

/** What the reader must do first, in their words rather than ours. */
const requirementText: Record<Requirement, string> = {
  provider: 'Connect a model first',
  logprobs: 'Needs a server that reports token probabilities',
  history: 'Run something first — there is nothing to look back at yet',
}

/**
 * Whether the app currently satisfies a requirement.
 *
 * @param requirement The condition to check.
 * @param context What the app has.
 * @returns True when satisfied.
 */
function isMet(requirement: Requirement, context: TourContext): boolean {
  switch (requirement) {
    case 'provider':
      return context.hasProvider
    case 'logprobs':
      return context.hasLogprobs
    case 'history':
      return context.hasHistory
  }
}

/**
 * Reports whether a walkthrough can be started, and why not when it cannot.
 *
 * @param tour The walkthrough.
 * @param context What the app has.
 * @returns The offer, with the first unmet requirement named.
 */
export function describeOffer(tour: Tour, context: TourContext): TourOffer {
  const unmet = tour.requires.find((requirement) => !isMet(requirement, context))

  return {
    tour,
    available: unmet === undefined,
    blockedReason: unmet === undefined ? null : requirementText[unmet],
  }
}

/**
 * Builds the list shown in the guide panel.
 *
 * Blocked walkthroughs are kept rather than hidden, so the panel shows what the tool can do
 * and what it is waiting for. Hiding them would make the guide look emptier the less set up
 * you are, which is exactly backwards.
 *
 * @param tours Every walkthrough.
 * @param context What the app has.
 * @returns Offers in the given order, available ones first within each kind.
 */
export function describeOffers(tours: Tour[], context: TourContext): TourOffer[] {
  return tours.map((tour) => describeOffer(tour, context))
}

/** The persisted slice that decides whether the welcome tour appears unprompted. */
export interface AutoStartState {
  /** Walkthroughs the reader has finished or explicitly left. */
  completedTourIds: string[]
  /** The reader's answer to "show this on startup". */
  autoStartEnabled: boolean
}

/**
 * Finds the page tour belonging to a route, if there is one.
 *
 * @param tours Every walkthrough.
 * @param pathname The current route.
 * @returns The page tour for that exact route, or undefined.
 */
export function pageTourFor(tours: Tour[], pathname: string): Tour | undefined {
  return tours.find((tour) => tour.kind === 'page' && tour.area === pathname)
}

/**
 * Whether arriving on a route should offer its tour unprompted.
 *
 * Deliberately narrow. It fires on the first visit to that area and never again, only when
 * nothing else is running, and only while the reader has left the switch on. Fifteen areas
 * each offering once is a manageable amount of help; any one of them offering twice is
 * nagging, and one that interrupts a walkthrough already in progress is worse.
 *
 * @param tour The page tour for the route just arrived at, if any.
 * @param state The persisted flags.
 * @param context What the app has, so an area with nothing in it stays quiet.
 * @param tourInProgress Whether a walkthrough is already running.
 * @returns True when it should open on its own.
 */
export function shouldOfferPageTour(
  tour: Tour | undefined,
  state: AutoStartState,
  context: TourContext,
  tourInProgress: boolean
): boolean {
  if (!tour || tourInProgress) return false
  if (!state.autoStartEnabled) return false
  if (state.completedTourIds.includes(tour.id)) return false

  // An area whose requirements are unmet has nothing to show, and touring an empty page
  // teaches the reader the tool is broken.
  return describeOffer(tour, context).available
}

/**
 * Whether the welcome tour should open on its own.
 *
 * Only ever true once. Reappearing after a dismissal is the behaviour that makes people
 * resent onboarding, so leaving the tour counts as completing it — the guide button is how
 * you get it back, deliberately, rather than by having it pushed at you again.
 *
 * @param state The persisted flags.
 * @param welcomeTourId Identity of the welcome tour.
 * @returns True when it should start unprompted.
 */
export function shouldAutoStart(state: AutoStartState, welcomeTourId: string): boolean {
  if (!state.autoStartEnabled) return false
  return !state.completedTourIds.includes(welcomeTourId)
}
