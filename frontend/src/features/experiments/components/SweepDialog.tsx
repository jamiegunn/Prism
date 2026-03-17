import { useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Loader2, Play, Plus, X } from 'lucide-react'
import { toast } from 'sonner'
import { useInstances } from '@/features/models/api'
import { useRunSweep } from '../api'
import type { SweepResult } from '../api'

interface SweepDialogProps {
  experimentId: string
  open: boolean
  onClose: () => void
}

export function SweepDialog({ experimentId, open, onClose }: SweepDialogProps) {
  const { data: instances } = useInstances()
  const sweepMutation = useRunSweep(experimentId)

  const [instanceId, setInstanceId] = useState('')
  const [input, setInput] = useState('')
  const [systemPrompt, setSystemPrompt] = useState('')
  const [temperatures, setTemperatures] = useState<number[]>([0.0, 0.3, 0.7, 1.0])
  const [topPs, setTopPs] = useState<number[]>([])
  const [maxTokensList, setMaxTokensList] = useState<number[]>([])
  const [tempInput, setTempInput] = useState('')
  const [topPInput, setTopPInput] = useState('')
  const [maxTokensInput, setMaxTokensInput] = useState('')
  const [captureLogprobs, setCaptureLogprobs] = useState(false)
  const [result, setResult] = useState<SweepResult | null>(null)

  const totalCombinations =
    Math.max(1, temperatures.length) *
    Math.max(1, topPs.length) *
    Math.max(1, maxTokensList.length)

  function addValue(setter: (fn: (prev: number[]) => number[]) => void, input: string, clearFn: (v: string) => void) {
    const num = parseFloat(input)
    if (isNaN(num)) return
    setter((prev) => prev.includes(num) ? prev : [...prev, num].sort((a, b) => a - b))
    clearFn('')
  }

  function removeValue(setter: (fn: (prev: number[]) => number[]) => void, value: number) {
    setter((prev) => prev.filter((v) => v !== value))
  }

  function handleRun() {
    if (!instanceId || !input.trim()) {
      toast.error('Select an instance and enter a prompt')
      return
    }

    sweepMutation.mutate(
      {
        instanceId,
        input: input.trim(),
        systemPrompt: systemPrompt.trim() || undefined,
        temperatureValues: temperatures.length > 0 ? temperatures : undefined,
        topPValues: topPs.length > 0 ? topPs : undefined,
        maxTokensValues: maxTokensList.length > 0 ? maxTokensList : undefined,
        captureLogprobs,
      },
      {
        onSuccess: (data) => {
          setResult(data)
          toast.success(`Sweep complete: ${data.completed}/${data.totalCombinations} succeeded`)
        },
        onError: (err) => toast.error(`Sweep failed: ${err.message}`),
      }
    )
  }

  function handleClose() {
    setResult(null)
    sweepMutation.reset()
    onClose()
  }

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && handleClose()}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Parameter Sweep</DialogTitle>
        </DialogHeader>

        {!result ? (
          <ScrollArea className="max-h-[70vh]">
            <div className="space-y-4 pr-2">
              {/* Instance */}
              <div className="space-y-1">
                <label className="text-xs font-medium text-zinc-300">Instance</label>
                <select
                  value={instanceId}
                  onChange={(e) => setInstanceId(e.target.value)}
                  className="w-full bg-zinc-900 border border-zinc-700 rounded px-2 py-1.5 text-sm text-zinc-300"
                >
                  <option value="">Select instance...</option>
                  {instances?.map((inst) => (
                    <option key={inst.id} value={inst.id}>
                      {inst.name} ({inst.modelId ?? 'no model'})
                    </option>
                  ))}
                </select>
              </div>

              {/* Prompt */}
              <div className="space-y-1">
                <label className="text-xs font-medium text-zinc-300">Prompt</label>
                <Textarea
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  placeholder="Enter the prompt to sweep..."
                  rows={3}
                  className="text-sm"
                />
              </div>

              {/* System Prompt */}
              <div className="space-y-1">
                <label className="text-xs font-medium text-zinc-300">System Prompt (optional)</label>
                <Textarea
                  value={systemPrompt}
                  onChange={(e) => setSystemPrompt(e.target.value)}
                  placeholder="Optional system prompt..."
                  rows={2}
                  className="text-sm"
                />
              </div>

              {/* Temperature values */}
              <ParamValueList
                label="Temperature"
                values={temperatures}
                inputValue={tempInput}
                onInputChange={setTempInput}
                onAdd={() => addValue(setTemperatures, tempInput, setTempInput)}
                onRemove={(v) => removeValue(setTemperatures, v)}
                placeholder="e.g., 0.5"
              />

              {/* Top-P values */}
              <ParamValueList
                label="Top-P (optional)"
                values={topPs}
                inputValue={topPInput}
                onInputChange={setTopPInput}
                onAdd={() => addValue(setTopPs, topPInput, setTopPInput)}
                onRemove={(v) => removeValue(setTopPs, v)}
                placeholder="e.g., 0.9"
              />

              {/* Max Tokens values */}
              <ParamValueList
                label="Max Tokens (optional)"
                values={maxTokensList}
                inputValue={maxTokensInput}
                onInputChange={setMaxTokensInput}
                onAdd={() => addValue(setMaxTokensList, maxTokensInput, setMaxTokensInput)}
                onRemove={(v) => removeValue(setMaxTokensList, v)}
                placeholder="e.g., 512"
              />

              {/* Logprobs toggle */}
              <label className="flex items-center gap-2 text-xs text-zinc-400 cursor-pointer">
                <input
                  type="checkbox"
                  checked={captureLogprobs}
                  onChange={(e) => setCaptureLogprobs(e.target.checked)}
                  className="rounded border-zinc-600"
                />
                Capture logprobs
              </label>

              {/* Summary */}
              <div className="rounded bg-zinc-800/50 px-3 py-2 text-xs text-zinc-400">
                <span className="font-medium text-zinc-300">{totalCombinations}</span> parameter combinations will be executed sequentially.
              </div>
            </div>
          </ScrollArea>
        ) : (
          <div className="space-y-3">
            <div className="rounded bg-zinc-800/50 p-4 text-center">
              <div className="text-2xl font-bold text-zinc-200">{result.completed}/{result.totalCombinations}</div>
              <div className="text-xs text-zinc-500 mt-1">runs completed</div>
            </div>
            {result.failed > 0 && (
              <div className="text-xs text-red-400">{result.failed} runs failed</div>
            )}
            <div className="text-xs text-zinc-500">
              {result.runIds.length} runs created. View them in the experiment detail page.
            </div>
          </div>
        )}

        <DialogFooter>
          <Button variant="outline" size="sm" onClick={handleClose}>
            {result ? 'Close' : 'Cancel'}
          </Button>
          {!result && (
            <Button
              size="sm"
              className="bg-violet-600 hover:bg-violet-700 text-white gap-1.5"
              onClick={handleRun}
              disabled={sweepMutation.isPending || !instanceId || !input.trim()}
            >
              {sweepMutation.isPending ? (
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
              ) : (
                <Play className="h-3.5 w-3.5" />
              )}
              Run Sweep ({totalCombinations})
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}

function ParamValueList({
  label,
  values,
  inputValue,
  onInputChange,
  onAdd,
  onRemove,
  placeholder,
}: {
  label: string
  values: number[]
  inputValue: string
  onInputChange: (v: string) => void
  onAdd: () => void
  onRemove: (v: number) => void
  placeholder: string
}) {
  return (
    <div className="space-y-1.5">
      <label className="text-xs font-medium text-zinc-300">{label}</label>
      <div className="flex gap-1">
        <Input
          value={inputValue}
          onChange={(e) => onInputChange(e.target.value)}
          onKeyDown={(e) => e.key === 'Enter' && (e.preventDefault(), onAdd())}
          placeholder={placeholder}
          className="h-7 text-xs bg-zinc-800 flex-1"
        />
        <Button variant="outline" size="sm" onClick={onAdd} className="h-7 w-7 p-0">
          <Plus className="h-3 w-3" />
        </Button>
      </div>
      {values.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {values.map((v) => (
            <Badge key={v} variant="secondary" className="text-xs gap-1 pr-1">
              {v}
              <button onClick={() => onRemove(v)} className="hover:text-red-400">
                <X className="h-2.5 w-2.5" />
              </button>
            </Badge>
          ))}
        </div>
      )}
    </div>
  )
}
