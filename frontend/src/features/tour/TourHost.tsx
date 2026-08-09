import { useEffect, useMemo, useRef } from 'react'
import { useLocation } from 'react-router-dom'
import { useInstances } from '@/features/models/api'
import { useHistoryRecords } from '@/features/history/api'
import { GuidePanel } from './components/GuidePanel'
import { TourOverlay } from './components/TourOverlay'
import { describeOffers, pageTourFor, shouldAutoStart, shouldOfferPageTour } from './selection'
import { useTourStore } from './store'
import { allTours, findTour, WELCOME_TOUR_ID } from './tours'
import type { TourContext } from './types'

/**
 * Mounts the guide and the tour overlay, and decides when the tour opens on its own.
 *
 * Lives in the shell rather than on a page because a walkthrough moves between routes; owning
 * it from any one page would end the tour the moment it navigated away.
 */
export function TourHost() {
  const { pathname } = useLocation()
  const {
    activeTourId,
    stepIndex,
    panelOpen,
    completedTourIds,
    autoStartEnabled,
    startTour,
    nextStep,
    previousStep,
    endTour,
    setPanelOpen,
    setAutoStartEnabled,
    resetProgress,
  } = useTourStore()

  const { data: instances } = useInstances()

  // One page of history is enough to know whether there is anything to look back at, and
  // avoids pulling the whole table just to decide whether to offer a walkthrough.
  const { data: history } = useHistoryRecords({ page: 1, pageSize: 1 })

  const context: TourContext = useMemo(
    () => ({
      hasProvider: (instances?.length ?? 0) > 0,
      hasLogprobs: (instances ?? []).some((instance) => instance.supportsLogprobs),
      hasHistory: (history?.items.length ?? 0) > 0,
    }),
    [instances, history]
  )

  const offers = useMemo(() => describeOffers(allTours, context), [context])

  // Both queries have answered, so a requirement that still reads as unmet really is unmet.
  const contextReady = instances !== undefined && history !== undefined

  useEffect(() => {
    if (shouldAutoStart({ completedTourIds, autoStartEnabled }, WELCOME_TOUR_ID)) {
      startTour(WELCOME_TOUR_ID)
    }
    // Deliberately runs once. Re-running on every change to the persisted flags would restart
    // the tour the instant `endTour` recorded it, which is an infinite loop with a scrim.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Offer an area's tour the first time you arrive there.
  //
  // Keyed on the pathname alone, and the store is read imperatively rather than through the
  // hook, so ending a tour cannot re-trigger this: `endTour` changes `activeTourId` and
  // `completedTourIds` but not the route, so the effect simply does not run again. Reading
  // those reactively would reopen a tour the moment it was closed.
  const offeredFor = useRef<string | null>(null)

  useEffect(() => {
    if (offeredFor.current === pathname) return

    const state = useTourStore.getState()
    const tour = pageTourFor(allTours, pathname)

    if (shouldOfferPageTour(tour, state, context, state.activeTourId !== null)) {
      offeredFor.current = pathname
      startTour(tour!.id)
      return
    }

    // Not offered — but only stop reconsidering once the answer cannot change. On the first
    // render of a route both queries are still in flight, so every requirement reads as unmet;
    // latching there would permanently suppress each gated tour on the one visit it is meant
    // to appear, which is exactly what it did before this guard.
    if (contextReady) {
      offeredFor.current = pathname
    }
  }, [pathname, context, contextReady, startTour])

  const activeTour = activeTourId ? findTour(activeTourId) : undefined

  return (
    <>
      <GuidePanel
        open={panelOpen}
        onOpenChange={setPanelOpen}
        offers={offers}
        currentArea={pathname}
        completedTourIds={completedTourIds}
        autoStartEnabled={autoStartEnabled}
        onStart={startTour}
        onAutoStartChange={setAutoStartEnabled}
        onResetProgress={resetProgress}
      />

      {activeTour && (
        <TourOverlay
          tour={activeTour}
          stepIndex={stepIndex}
          onNext={nextStep}
          onBack={previousStep}
          onClose={endTour}
        />
      )}
    </>
  )
}
