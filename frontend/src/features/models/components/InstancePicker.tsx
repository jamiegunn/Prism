import { Select } from '@/components/ui/select'
import { Input } from '@/components/ui/input'
import { cn } from '@/lib/utils'
import { useInstances } from '../api'

interface InstancePickerProps {
  instanceId: string
  model: string
  /** Called with both, because choosing a server usually decides the model too. */
  onChange: (instanceId: string, model: string) => void
  className?: string
}

/**
 * Picks a registered server and the model to run on it.
 *
 * Two pages used to ask for a raw instance **GUID** in a free-text box, next to a free-text
 * model name. The GUID is displayed nowhere in the UI — the only way to obtain one was to call
 * the API by hand — so those forms could not be completed from the app that contained them.
 * Every other page in Prism uses a dropdown; this is that, extracted so there is one of it.
 *
 * The model stays editable rather than being derived, because one server can serve several
 * models and the registered `modelId` is only the one Prism saw first. Choosing a server
 * pre-fills it, which is right almost always and never traps you.
 */
export function InstancePicker({ instanceId, model, onChange, className }: InstancePickerProps) {
  const { data: instances, isLoading } = useInstances()

  const selected = instances?.find((instance) => instance.id === instanceId)

  return (
    <div className={cn('grid grid-cols-2 gap-2', className)}>
      <div className="space-y-1">
        <Select
          value={instanceId}
          disabled={isLoading}
          onChange={(event) => {
            const next = instances?.find((instance) => instance.id === event.target.value)

            // Carry the model across only when it was the previous server's default; anything
            // typed by hand is the reader's and survives the switch.
            const keepTyped = model.length > 0 && model !== selected?.modelId
            onChange(event.target.value, keepTyped ? model : (next?.modelId ?? ''))
          }}
        >
          <option value="">
            {isLoading
              ? 'Loading servers...'
              : instances?.length
                ? 'Select a server...'
                : 'No servers registered'}
          </option>
          {instances?.map((instance) => (
            <option key={instance.id} value={instance.id}>
              {instance.name}
              {instance.status !== 'Online' ? ` (${instance.status})` : ''}
            </option>
          ))}
        </Select>

        {selected && (
          <p className="truncate text-xs text-zinc-500">
            {selected.providerType} &middot; {selected.endpoint}
          </p>
        )}
      </div>

      <div className="space-y-1">
        <Input
          placeholder="Model name"
          value={model}
          onChange={(event) => onChange(instanceId, event.target.value)}
        />
        {selected?.modelId && model !== selected.modelId && (
          <p className="truncate text-xs text-zinc-500">Registered: {selected.modelId}</p>
        )}
      </div>
    </div>
  )
}
