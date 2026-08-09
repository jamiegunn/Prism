import { useEffect, useMemo } from 'react'
import { useInstances } from '@/features/models/api'
import { useHistoryRecords } from '@/features/history/api'
import { GuidePanel } from './components/GuidePanel'
import { TourOverlay } from './components/TourOverlay'
import { describeOffers, shouldAutoStart } from './selection'
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

  useEffect(() => {
    if (shouldAutoStart({ completedTourIds, autoStartEnabled }, WELCOME_TOUR_ID)) {
      startTour(WELCOME_TOUR_ID)
    }
    // Deliberately runs once. Re-running on every change to the persisted flags would restart
    // the tour the instant `endTour` recorded it, which is an infinite loop with a scrim.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const activeTour = activeTourId ? findTour(activeTourId) : undefined

  return (
    <>
      <GuidePanel
        open={panelOpen}
        onOpenChange={setPanelOpen}
        offers={offers}
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
