import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { findTour } from './tours'

/**
 * Tour state, split between what survives a reload and what must not.
 *
 * Which walkthroughs you have finished is worth keeping; which step you were on is not. A
 * half-finished tour restored on next launch would reopen an overlay over a page the reader
 * did not ask for, which is the behaviour `partialize` exists to prevent.
 */
interface TourStore {
  // Persisted.
  /** Walkthroughs finished or explicitly left. */
  completedTourIds: string[]
  /** Whether the welcome tour may open unprompted. */
  autoStartEnabled: boolean

  // Transient.
  /** The running walkthrough, or null. */
  activeTourId: string | null
  /** Position within the running walkthrough. */
  stepIndex: number
  /** Whether the guide panel is open. */
  panelOpen: boolean

  startTour: (tourId: string) => void
  nextStep: () => void
  previousStep: () => void
  /** Leaves the tour, recording it as seen so it does not reappear on its own. */
  endTour: () => void
  setPanelOpen: (open: boolean) => void
  setAutoStartEnabled: (enabled: boolean) => void
  /** Forgets every completion, so the welcome tour behaves as it does on a fresh install. */
  resetProgress: () => void
}

const transientDefaults = {
  activeTourId: null as string | null,
  stepIndex: 0,
  panelOpen: false,
}

export const useTourStore = create<TourStore>()(
  persist(
    (set, get) => ({
      completedTourIds: [],
      autoStartEnabled: true,
      ...transientDefaults,

      startTour: (tourId) => {
        // Opening the panel and the overlay together would put a sheet over the spotlight.
        set({ activeTourId: tourId, stepIndex: 0, panelOpen: false })
      },

      nextStep: () => {
        const { activeTourId, stepIndex } = get()
        if (!activeTourId) return

        const tour = findTour(activeTourId)
        if (!tour) {
          set({ ...transientDefaults })
          return
        }

        if (stepIndex >= tour.steps.length - 1) {
          get().endTour()
          return
        }

        set({ stepIndex: stepIndex + 1 })
      },

      previousStep: () => {
        const { stepIndex } = get()
        set({ stepIndex: Math.max(0, stepIndex - 1) })
      },

      endTour: () => {
        const { activeTourId, completedTourIds } = get()

        set({
          ...transientDefaults,
          completedTourIds:
            activeTourId && !completedTourIds.includes(activeTourId)
              ? [...completedTourIds, activeTourId]
              : completedTourIds,
        })
      },

      setPanelOpen: (open) => set({ panelOpen: open }),
      setAutoStartEnabled: (enabled) => set({ autoStartEnabled: enabled }),

      resetProgress: () => set({ completedTourIds: [], autoStartEnabled: true }),
    }),
    {
      name: 'prism-tour-state',
      partialize: (state) => ({
        completedTourIds: state.completedTourIds,
        autoStartEnabled: state.autoStartEnabled,
      }),
    }
  )
)
