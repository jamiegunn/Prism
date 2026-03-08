import { useState } from 'react'
import { Settings2, X } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Slider } from '@/components/ui/slider'
import { Select } from '@/components/ui/select'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Badge } from '@/components/ui/badge'
import { Separator } from '@/components/ui/separator'
import { ParamLabel } from '@/components/ui/param-label'
import { useInstances } from '../api'
import { usePlaygroundStore } from '../store'

interface ParameterSidebarProps {
  className?: string
}

export function ParameterSidebar({ className }: ParameterSidebarProps) {
  const instances = useInstances()
  const store = usePlaygroundStore()
  const [stopInput, setStopInput] = useState('')

  const selectedInstance = instances.data?.find((i) => i.id === store.selectedInstanceId)

  function handleAddStopSequence(e: React.KeyboardEvent<HTMLInputElement>) {
    if (e.key === 'Enter' && stopInput.trim()) {
      e.preventDefault()
      const updated = [...store.stopSequences, stopInput.trim()]
      store.setStopSequences(updated)
      setStopInput('')
    }
  }

  function handleRemoveStopSequence(index: number) {
    const updated = store.stopSequences.filter((_, i) => i !== index)
    store.setStopSequences(updated)
  }

  return (
    <ScrollArea className={cn('h-full', className)}>
      <div className="space-y-6 p-4">
        <div className="flex items-center gap-2">
          <Settings2 className="h-4 w-4 text-zinc-400" />
          <h3 className="text-sm font-semibold text-zinc-200">Parameters</h3>
        </div>

        {/* Instance Selector */}
        <div className="space-y-2">
          <ParamLabel
            label="Model / Instance"
            tooltip="The inference engine and model to use for chat. Each instance connects to a running provider (vLLM, Ollama, etc.) serving a specific model."
          />
          <Select
            value={store.selectedInstanceId ?? ''}
            onChange={(e) => store.setSelectedInstanceId(e.target.value || null)}
          >
            <option value="">Select an instance...</option>
            {instances.data?.map((instance) => (
              <option key={instance.id} value={instance.id}>
                {instance.name} {instance.modelId ? `(${instance.modelId})` : ''}
              </option>
            ))}
          </Select>
          {selectedInstance && (
            <p className="text-xs text-zinc-500 truncate">
              {selectedInstance.providerType} &middot; {selectedInstance.endpoint}
            </p>
          )}
        </div>

        <Separator />

        {/* Temperature */}
        <ParameterSlider
          label="Temperature"
          tooltip="Controls randomness. 0 = deterministic (always picks the most likely token). Higher values make the output more creative and varied by flattening the probability distribution."
          value={store.temperature}
          onChange={store.setTemperature}
          min={0}
          max={2}
          step={0.1}
        />

        {/* Top P */}
        <ParameterSlider
          label="Top P"
          tooltip="Nucleus sampling. The model only considers tokens whose cumulative probability reaches this threshold. Lower values = more focused output; 1.0 = consider all tokens."
          value={store.topP}
          onChange={store.setTopP}
          min={0}
          max={1}
          step={0.05}
        />

        {/* Top K */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <ParamLabel
              label="Top K"
              tooltip="Only the top K most probable tokens are considered at each generation step. Lower values make output more predictable. Works alongside Top P to control the sampling pool."
            />
            <span className="text-xs font-mono text-zinc-500">{store.topK}</span>
          </div>
          <Input
            type="number"
            min={1}
            max={200}
            value={store.topK}
            onChange={(e) => store.setTopK(Number(e.target.value))}
            className="h-8 text-xs"
          />
        </div>

        {/* Max Tokens */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <ParamLabel
              label="Max Tokens"
              tooltip="Maximum number of tokens the model will generate in its response. One token is roughly 3-4 characters of English text. The model may stop earlier if it reaches a natural ending or a stop sequence."
            />
            <span className="text-xs font-mono text-zinc-500">{store.maxTokens}</span>
          </div>
          <Input
            type="number"
            min={1}
            max={32768}
            value={store.maxTokens}
            onChange={(e) => store.setMaxTokens(Number(e.target.value))}
            className="h-8 text-xs"
          />
        </div>

        <Separator />

        {/* Stop Sequences */}
        <div className="space-y-2">
          <ParamLabel
            label="Stop Sequences"
            tooltip="The model stops generating when it produces any of these strings. Useful for controlling output format, e.g., stopping at newlines or specific delimiters. Type a sequence and press Enter to add it."
          />
          <Input
            placeholder="Type and press Enter..."
            value={stopInput}
            onChange={(e) => setStopInput(e.target.value)}
            onKeyDown={handleAddStopSequence}
            className="h-8 text-xs"
          />
          {store.stopSequences.length > 0 && (
            <div className="flex flex-wrap gap-1">
              {store.stopSequences.map((seq, index) => (
                <Badge key={index} variant="secondary" className="gap-1 text-xs">
                  <span className="font-mono">{seq}</span>
                  <button
                    onClick={() => handleRemoveStopSequence(index)}
                    className="ml-0.5 hover:text-red-400"
                  >
                    <X className="h-3 w-3" />
                  </button>
                </Badge>
              ))}
            </div>
          )}
        </div>

        <Separator />

        {/* Frequency Penalty */}
        <ParameterSlider
          label="Frequency Penalty"
          tooltip="Penalizes tokens proportionally to how often they have already appeared. Positive values reduce repetition; negative values encourage it. Applied per-token based on count."
          value={store.frequencyPenalty}
          onChange={store.setFrequencyPenalty}
          min={-2}
          max={2}
          step={0.1}
        />

        {/* Presence Penalty */}
        <ParameterSlider
          label="Presence Penalty"
          tooltip="Penalizes tokens that have appeared at all, regardless of frequency. Positive values encourage the model to talk about new topics; negative values make it stick to established ones."
          value={store.presencePenalty}
          onChange={store.setPresencePenalty}
          min={-2}
          max={2}
          step={0.1}
        />

        <Separator />

        {/* Logprobs */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <ParamLabel
              label="Logprobs"
              tooltip="When enabled, the model returns the log-probability it assigned to each token it generated. This powers the logprobs heatmap and per-token confidence analysis in the bottom panel."
            />
            <button
              onClick={() => store.setLogprobs(!store.logprobs)}
              className={cn(
                'relative inline-flex h-5 w-9 items-center rounded-full transition-colors',
                store.logprobs ? 'bg-violet-600' : 'bg-zinc-700'
              )}
            >
              <span
                className={cn(
                  'inline-block h-3.5 w-3.5 rounded-full bg-white transition-transform',
                  store.logprobs ? 'translate-x-4.5' : 'translate-x-0.5'
                )}
              />
            </button>
          </div>
          {store.logprobs && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <ParamLabel
                  label="Top Logprobs"
                  tooltip="How many alternative tokens to return at each position. Shows what the model considered besides the token it chose. Higher values give a fuller picture of the probability distribution."
                />
                <span className="text-xs font-mono text-zinc-500">{store.topLogprobs}</span>
              </div>
              <Slider
                min={1}
                max={20}
                step={1}
                value={store.topLogprobs}
                onChange={(e) => store.setTopLogprobs(Number(e.target.value))}
              />
            </div>
          )}
        </div>

        <Separator />

        {/* Reset */}
        <Button
          variant="outline"
          size="sm"
          onClick={store.reset}
          className="w-full text-xs"
        >
          Reset to Defaults
        </Button>
      </div>
    </ScrollArea>
  )
}

interface ParameterSliderProps {
  label: string
  tooltip: string
  value: number
  onChange: (value: number) => void
  min: number
  max: number
  step: number
}

function ParameterSlider({ label, tooltip, value, onChange, min, max, step }: ParameterSliderProps) {
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <ParamLabel label={label} tooltip={tooltip} />
        <span className="text-xs font-mono text-zinc-500">{value.toFixed(step < 1 ? 2 : 0)}</span>
      </div>
      <Slider
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={(e) => onChange(Number(e.target.value))}
      />
    </div>
  )
}
