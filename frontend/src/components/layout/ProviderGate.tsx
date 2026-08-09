import { useLocation, useNavigate } from 'react-router-dom'
import { AlertTriangle, ArrowRight, ServerOff } from 'lucide-react'
import { useInstances } from '@/features/models/api'
import type { InferenceInstance } from '@/features/models/types'

/**
 * Works out what, if anything, is standing between the user and a working model.
 *
 * @param instances Registered instances, or undefined while loading.
 * @param isError Whether the instance list could not be fetched at all.
 * @returns A banner description, or null when everything is fine.
 */
export function describeProviderState(
  instances: InferenceInstance[] | undefined,
  isError: boolean,
): { tone: 'error' | 'warning'; title: string; detail: string; action: string } | null {
  if (isError) {
    return {
      tone: 'error',
      title: 'Cannot reach the Prism API',
      detail:
        'The backend is not responding, so nothing on this page reflects real data. Start it with ./dev.sh, or run ./scripts/doctor.sh to find out what is wrong.',
      action: '',
    }
  }

  // Undefined means the request is still in flight. Showing a "no provider" banner for the
  // half-second before the list arrives would flash on every page load.
  if (!instances) {
    return null
  }

  if (instances.length === 0) {
    return {
      tone: 'warning',
      title: 'No model connected',
      detail:
        'Prism reads a model as it generates. Until one is connected, every panel here is empty. Prism can look for a server running on this machine and connect it for you.',
      action: 'Connect a model',
    }
  }

  // Registered is not the same as reachable. A stale registration pointing at a server that is
  // no longer running produces empty panels and no error, which reads as the product being
  // broken rather than the model being off.
  const online = instances.filter((i) => i.status === 'Online')

  if (online.length === 0) {
    return {
      tone: 'warning',
      title:
        instances.length === 1
          ? `${instances[0].name} is not responding`
          : `None of your ${instances.length} models are responding`,
      detail:
        'They are registered but not reachable, so requests will fail. Check the inference server is running, or connect a different one.',
      action: 'Review models',
    }
  }

  return null
}

/**
 * A banner shown above every page while there is no model to talk to.
 *
 * The first-run experience used to live only on the Models page: someone landing on the
 * Playground with nothing connected got a disabled text box and no explanation. This states the
 * problem wherever they happen to be, and offers the one action that fixes it.
 *
 * It is deliberately not dismissible. Nothing in the product does anything useful in this
 * state, so letting it be hidden would only hide the reason the app appears broken.
 */
export function ProviderGate() {
  const navigate = useNavigate()
  const location = useLocation()
  const { data, isError } = useInstances()

  const state = describeProviderState(data, isError)

  if (!state) {
    return null
  }

  // On the Models page the banner would sit directly above the screen that fixes it, which is
  // noise — that page states its own case better.
  if (location.pathname.startsWith('/models')) {
    return null
  }

  const isCritical = state.tone === 'error'

  return (
    <div
      role="status"
      className={[
        'flex items-start gap-3 border-b px-6 py-3',
        isCritical
          ? 'border-red-900 bg-red-950/50 text-red-100'
          : 'border-amber-900 bg-amber-950/40 text-amber-100',
      ].join(' ')}
    >
      {isCritical ? (
        <ServerOff className="mt-0.5 h-4 w-4 shrink-0 text-red-400" />
      ) : (
        <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0 text-amber-400" />
      )}

      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium">{state.title}</p>
        <p className="mt-0.5 text-xs leading-relaxed opacity-90">{state.detail}</p>
      </div>

      {state.action && (
        <button
          type="button"
          onClick={() => navigate('/models')}
          className="shrink-0 inline-flex items-center gap-1.5 rounded bg-amber-500 px-3 py-1.5 text-xs font-medium text-amber-950 hover:bg-amber-400"
        >
          {state.action}
          <ArrowRight className="h-3.5 w-3.5" />
        </button>
      )}
    </div>
  )
}
