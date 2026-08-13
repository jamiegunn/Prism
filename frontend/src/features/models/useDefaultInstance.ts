import { useEffect, useRef } from 'react'
import { useInstances } from './api'
import type { InferenceInstance } from './types'

/**
 * Picks the instance a page should start on.
 *
 * @param instances Registered instances.
 * @returns The best candidate, or null when there is nothing worth selecting.
 *
 * @remarks
 * Preference order, and the reason for each step:
 *
 * 1. The instance marked as default, if it is online. Someone went out of their way to mark it.
 * 2. Any other online instance. A working model beats a stated preference that is switched off.
 * 3. Nothing. Selecting an offline instance would let a user send a prompt that cannot succeed,
 *    which is worse than making them choose — the failure would look like a bug in Prism rather
 *    than a model that is not running.
 *
 * The development seed data registers two instances, only one of which is usually reachable, so
 * "first in the list" would land on the wrong one about half the time.
 */
export function pickDefaultInstance(
  instances: InferenceInstance[] | undefined,
): InferenceInstance | null {
  if (!instances || instances.length === 0) {
    return null
  }

  const online = instances.filter((i) => i.status === 'Online')

  return online.find((i) => i.isDefault) ?? online[0] ?? null
}

/**
 * Decides whether the current selection should be replaced.
 *
 * @param selectedId What the page currently has selected.
 * @param instances Registered instances.
 * @returns The id to select, or null to leave it alone.
 *
 * @remarks
 * Two cases need handling, and only two. Nothing selected is the obvious one. The other is a
 * selection that no longer exists: the instance id is persisted in localStorage, so removing an
 * instance and coming back leaves a page pointing at a dead id, showing a blank dropdown and
 * refusing to send with no explanation.
 *
 * A selected instance that has merely gone offline is deliberately left alone. Silently moving
 * someone to a different model mid-session would change their results without saying so, which
 * matters more here than convenience.
 *
 * Restoring one is a different moment from staying on one. The seeded instances have fixed ids,
 * so a page whose stored choice was the seeded vLLM finds it present on every later install and
 * keeps it — the Token Explorer opened on a server nobody is running, and the first prediction
 * failed, with a working model sitting in the same dropdown. `isRestoring` marks that first
 * resolution after mount, where replacing an unusable choice is help rather than interference.
 */
export function resolveSelection(
  selectedId: string | null,
  instances: InferenceInstance[] | undefined,
  isRestoring = false,
): string | null {
  if (!instances) {
    return null
  }

  const current = selectedId === null ? undefined : instances.find((i) => i.id === selectedId)

  if (current) {
    if (!isRestoring || current.status === 'Online') {
      return null
    }

    // A stored choice that cannot answer, replaced only if there is something that can — and
    // never with another dead one, which would be movement without improvement.
    const usable = pickDefaultInstance(instances)
    return usable && usable.id !== current.id ? usable.id : null
  }

  return pickDefaultInstance(instances)?.id ?? null
}

/**
 * Selects a sensible instance on first load, so a page that has one working model does not
 * open on "Select an instance...".
 *
 * @param selectedId The page's current selection.
 * @param onSelect Called with the id to select.
 *
 * @remarks
 * Pages keep their own selection in their own persisted store, which is what allows the
 * Playground and the Token Explorer to be pointed at different models at the same time. That is
 * worth keeping, so this fills the gap rather than centralising the state.
 */
export function useDefaultInstance(
  selectedId: string | null,
  onSelect: (id: string) => void,
): void {
  const { data } = useInstances()

  // True until the first list arrives, which is the moment a stored selection is being restored
  // rather than held. After that, what is selected is what this session chose.
  const restoring = useRef(true)

  useEffect(() => {
    if (!data) {
      return
    }

    const next = resolveSelection(selectedId, data, restoring.current)
    restoring.current = false

    if (next !== null) {
      onSelect(next)
    }
  }, [selectedId, data, onSelect])
}
