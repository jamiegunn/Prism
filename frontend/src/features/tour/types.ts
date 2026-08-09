/**
 * Shapes for the guided tour and the guided situations.
 *
 * A "situation" is the same machinery as the welcome tour, differing only in intent: the
 * welcome tour orients someone in the shell, a situation walks them through one real task
 * end to end. Keeping them the same type means one overlay, one keyboard model, one set of
 * tests — and a situation can be resumed exactly like a tour.
 */

/** Which side of the spotlight the callout sits on. */
export type Side = 'top' | 'bottom' | 'left' | 'right'

/** What the tool must already have before a walkthrough is worth starting. */
export type Requirement = 'provider' | 'logprobs' | 'history'

/**
 * Orientation, a concrete task, or a single area of the app.
 *
 * A `page` tour belongs to one route and offers itself the first time you go there, which is
 * the moment it is worth having and the only moment it is not an interruption.
 */
export type TourKind = 'welcome' | 'situation' | 'page'

/** One stop in a walkthrough. */
export interface TourStep {
  /** Stable identity, so a step can be pinned by a test and resumed by the store. */
  id: string
  title: string
  /** The explanation. Prose, not bullet fragments. */
  body: string
  /** Route this step happens on. The host navigates there before showing the step. */
  route?: string
  /**
   * The `data-tour` value to spotlight. When absent — or when nothing on the page carries
   * it — the step degrades to a centred card rather than pointing at nothing.
   */
  anchor?: string
  /** Preferred callout side; ignored when it will not fit. */
  side?: Side
  /** An invitation to actually do something, shown under the body. */
  action?: string
}

/** A complete walkthrough. */
export interface Tour {
  id: string
  kind: TourKind
  /**
   * For a `page` tour, the route it belongs to. Arriving there for the first time offers it,
   * and the guide pins it to the top while you are on that route.
   */
  area?: string
  title: string
  /** What the reader can do afterwards that they could not do before. */
  outcome: string
  /** Rough length, so nobody starts one without knowing the cost. */
  minutes: number
  /** Conditions that must hold for this to be worth starting. */
  requires: Requirement[]
  steps: TourStep[]
}

/** What the app currently has, used to decide which situations are worth offering. */
export interface TourContext {
  /** At least one inference instance is registered. */
  hasProvider: boolean
  /** At least one registered instance reports per-token probabilities. */
  hasLogprobs: boolean
  /** Something has been run, so the history and analytics pages are not empty. */
  hasHistory: boolean
}
