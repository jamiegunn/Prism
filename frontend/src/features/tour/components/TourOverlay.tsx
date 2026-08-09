import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { useLocation, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { placeCallout, spotlightRect, type Placement, type Rect } from '../geometry'
import type { Tour } from '../types'

interface TourOverlayProps {
  tour: Tour
  stepIndex: number
  onNext: () => void
  onBack: () => void
  onClose: () => void
}

/** Callout size. Fixed so placement can be computed before the callout has been laid out. */
const CALLOUT: Rect = { top: 0, left: 0, width: 360, height: 216 }

/** How long to keep looking for an anchor after a route change before giving up on it. */
const ANCHOR_TIMEOUT_MS = 1200

/**
 * Reads an anchor's position from the DOM.
 *
 * @param anchor The `data-tour` value, or undefined for an unanchored step.
 * @returns The element's rectangle, or null when it is absent or not rendered.
 */
function measureAnchor(anchor: string | undefined): Rect | null {
  if (!anchor) return null

  const element = document.querySelector(`[data-tour="${anchor}"]`)
  if (!(element instanceof HTMLElement)) return null

  const rect = element.getBoundingClientRect()

  // A zero-sized box means the element is present but hidden — a collapsed panel, say.
  // Spotlighting it would put a hole in the corner of the screen and look like a bug.
  if (rect.width === 0 && rect.height === 0) return null

  return { top: rect.top, left: rect.left, width: rect.width, height: rect.height }
}

/**
 * The spotlight, its callout, and the keyboard model for moving through a walkthrough.
 *
 * Nothing else in this codebase implements Escape-to-close or a focus trap — Dialog and Sheet
 * both lack them. This one does, because a tour deliberately takes over the screen: leaving
 * someone tab-cycling through the page behind a scrim they cannot dismiss would be a worse
 * introduction to the tool than no tour at all.
 */
export function TourOverlay({ tour, stepIndex, onNext, onBack, onClose }: TourOverlayProps) {
  const step = tour.steps[stepIndex]
  const navigate = useNavigate()
  const location = useLocation()
  const calloutRef = useRef<HTMLDivElement>(null)

  const [target, setTarget] = useState<Rect | null>(null)
  const [placement, setPlacement] = useState<Placement>(() =>
    placeCallout(null, CALLOUT, { width: window.innerWidth, height: window.innerHeight })
  )

  const onRoute = !step?.route || location.pathname === step.route

  // Take the step's route before looking for its anchor, since the anchor usually lives on
  // the page we are about to open.
  useEffect(() => {
    if (step?.route && location.pathname !== step.route) {
      navigate(step.route)
    }
  }, [step?.route, location.pathname, navigate])

  const reposition = useCallback(() => {
    const rect = measureAnchor(step?.anchor)
    setTarget(rect)
    setPlacement(
      placeCallout(spotlightRect(rect), CALLOUT, {
        width: window.innerWidth,
        height: window.innerHeight,
      }, step?.side)
    )
  }, [step?.anchor, step?.side])

  // Poll briefly for the anchor: after a route change the page mounts asynchronously, and a
  // single measurement would miss it and silently degrade every step to a centred card.
  useLayoutEffect(() => {
    if (!onRoute) return undefined

    let frame = 0
    const deadline = performance.now() + ANCHOR_TIMEOUT_MS

    const look = () => {
      reposition()

      if (!measureAnchor(step?.anchor) && performance.now() < deadline) {
        frame = requestAnimationFrame(look)
      }
    }

    look()
    return () => cancelAnimationFrame(frame)
  }, [reposition, onRoute, step?.anchor, stepIndex])

  // Keep up with scrolling and resizing. The existing Tooltip does not, and its callouts
  // drift away from what they describe as soon as the page moves.
  useEffect(() => {
    const handle = () => reposition()

    window.addEventListener('resize', handle)
    window.addEventListener('scroll', handle, true)

    return () => {
      window.removeEventListener('resize', handle)
      window.removeEventListener('scroll', handle, true)
    }
  }, [reposition])

  // Bring the subject into view before pointing at it.
  useEffect(() => {
    if (!step?.anchor) return

    const element = document.querySelector(`[data-tour="${step.anchor}"]`)
    if (element instanceof HTMLElement) {
      element.scrollIntoView({ block: 'nearest', inline: 'nearest' })
    }
  }, [step?.anchor, stepIndex])

  useEffect(() => {
    const handleKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        event.preventDefault()
        onClose()
        return
      }

      if (event.key === 'ArrowRight') {
        event.preventDefault()
        onNext()
        return
      }

      if (event.key === 'ArrowLeft') {
        event.preventDefault()
        onBack()
        return
      }

      // Keep focus inside the callout. Without this, Tab walks into the page underneath,
      // which is unreachable behind the scrim and so looks like focus simply vanished.
      if (event.key === 'Tab') {
        const focusable = calloutRef.current?.querySelectorAll<HTMLElement>('button')
        if (!focusable || focusable.length === 0) return

        const first = focusable[0]
        const last = focusable[focusable.length - 1]

        if (event.shiftKey && document.activeElement === first) {
          event.preventDefault()
          last.focus()
        } else if (!event.shiftKey && document.activeElement === last) {
          event.preventDefault()
          first.focus()
        }
      }
    }

    window.addEventListener('keydown', handleKey)
    return () => window.removeEventListener('keydown', handleKey)
  }, [onNext, onBack, onClose])

  // Move focus into the callout so the keyboard model works without a click first.
  useEffect(() => {
    calloutRef.current?.querySelector('button')?.focus()
  }, [stepIndex])

  if (!step) return null

  const hole = spotlightRect(target)
  const isLast = stepIndex === tour.steps.length - 1

  return createPortal(
    <div className="fixed inset-0 z-[10000]" role="dialog" aria-modal="true" aria-label={tour.title}>
      {/* The scrim is a shadow cast outwards from the hole, so there is exactly one element
          to position and the lit area stays clickable. */}
      {hole ? (
        <div
          className="pointer-events-none absolute rounded-lg ring-2 ring-violet-400/80 transition-all duration-200"
          style={{
            top: hole.top,
            left: hole.left,
            width: hole.width,
            height: hole.height,
            boxShadow: '0 0 0 9999px rgba(9, 9, 11, 0.72)',
          }}
        />
      ) : (
        <div className="absolute inset-0 bg-zinc-950/72" onClick={onClose} />
      )}

      <div
        ref={calloutRef}
        className={cn(
          'absolute w-[360px] rounded-lg border border-zinc-700 bg-zinc-900 p-4 shadow-2xl',
          'transition-all duration-200'
        )}
        style={{ top: placement.top, left: placement.left }}
      >
        <div className="mb-2 flex items-start justify-between gap-3">
          <p className="text-[11px] font-semibold uppercase tracking-wider text-violet-400">
            {tour.title}
          </p>
          <button
            type="button"
            onClick={onClose}
            aria-label="Leave the tour"
            className="rounded p-0.5 text-zinc-500 transition-colors hover:text-zinc-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>

        <h2 className="mb-1.5 text-sm font-semibold text-zinc-50">{step.title}</h2>
        <p className="text-[13px] leading-relaxed text-zinc-400">{step.body}</p>

        {step.action && (
          <p className="mt-2.5 rounded border border-violet-500/30 bg-violet-500/10 px-2.5 py-1.5 text-[12px] leading-relaxed text-violet-200">
            {step.action}
          </p>
        )}

        <div className="mt-4 flex items-center justify-between">
          <span className="font-mono text-[11px] tabular-nums text-zinc-500">
            {stepIndex + 1} / {tour.steps.length}
          </span>

          <div className="flex items-center gap-1.5">
            {stepIndex > 0 && (
              <Button variant="ghost" size="sm" onClick={onBack} className="h-7 gap-1 text-xs">
                <ArrowLeft className="h-3 w-3" />
                Back
              </Button>
            )}
            <Button size="sm" onClick={onNext} className="h-7 gap-1 text-xs">
              {isLast ? 'Done' : 'Next'}
              {!isLast && <ArrowRight className="h-3 w-3" />}
            </Button>
          </div>
        </div>
      </div>
    </div>,
    document.body
  )
}
