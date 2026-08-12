import { useMemo, useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { Slider } from '@/components/ui/slider'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Badge } from '@/components/ui/badge'
import { Play, Loader2, Settings2, ChevronDown, ChevronUp, AlertTriangle } from 'lucide-react'
import { cn } from '@/lib/utils'
import { useInstances } from '@/features/playground/api'
import { useInstanceModels } from '@/features/models/api'
import { useReplayRecord } from '../api'
import { diffWords } from '../diff'
import { describeMutationError } from '@/services/mutationErrors'
import type { HistoryRecordDetail, ReplayResult } from '../types'

interface ReplayDialogProps {
  record: HistoryRecordDetail
  open: boolean
  onClose: () => void
}

export function ReplayDialog({ record, open, onClose }: ReplayDialogProps) {
  const [selectedInstance, setSelectedInstance] = useState('')
  const [showOverrides, setShowOverrides] = useState(false)
  const [overrideTemperature, setOverrideTemperature] = useState<number | undefined>(undefined)
  const [overrideMaxTokens, setOverrideMaxTokens] = useState<string>('')
  const [overrideTopP, setOverrideTopP] = useState<number | undefined>(undefined)
  const [overrideModel, setOverrideModel] = useState<string>('')
  const { data: instances } = useInstances()
  const { data: instanceModels } = useInstanceModels(selectedInstance || null)
  const replayMutation = useReplayRecord()
  const [result, setResult] = useState<ReplayResult | null>(null)

  const handleReplay = () => {
    if (!selectedInstance) return
    replayMutation.mutate(
      {
        id: record.id,
        instanceId: selectedInstance,
        overrideModel: overrideModel.trim() || undefined,
        overrideTemperature,
        overrideMaxTokens: overrideMaxTokens ? Number(overrideMaxTokens) : undefined,
        overrideTopP,
      },
      { onSuccess: (data) => setResult(data) }
    )
  }

  const handleClose = () => {
    setResult(null)
    setSelectedInstance('')
    setShowOverrides(false)
    setOverrideTemperature(undefined)
    setOverrideMaxTokens('')
    setOverrideTopP(undefined)
    setOverrideModel('')
    replayMutation.reset()
    onClose()
  }

  const onlineInstances = instances?.filter((i) => i.status === 'Online') ?? []
  const hasOverrides =
    overrideTemperature !== undefined ||
    overrideMaxTokens !== '' ||
    overrideTopP !== undefined ||
    overrideModel.trim() !== ''

  // The replay runs the model the record names. Whether the chosen instance can serve it is the
  // question worth answering before the run rather than after a "model not found" — and when it
  // cannot, the list below is what turns that into a choice instead of a dead end.
  const servedModels = instanceModels?.models ?? []
  const recordModelIsServed =
    record.model.length === 0 ||
    servedModels.length === 0 ||
    servedModels.includes(record.model)
  const modelMismatch = !recordModelIsServed && overrideModel.trim() === ''

  // What temperature the replay actually ran at decides whether a difference means anything.
  // The recorded request is the source for it, since that is what the replay re-sends.
  const originalTemperature = useMemo(() => {
    try {
      const parsed: unknown = JSON.parse(record.requestJson)
      const value = (parsed as { temperature?: unknown } | null)?.temperature
      return typeof value === 'number' ? value : null
    } catch {
      return null
    }
  }, [record.requestJson])
  const effectiveTemperature = overrideTemperature ?? originalTemperature

  // Max tokens is a free-text number field, so it can hold something the API will reject.
  // Catching it here means the reader is told before a request is spent finding out.
  const maxTokensValue = overrideMaxTokens === '' ? null : Number(overrideMaxTokens)
  const maxTokensError =
    maxTokensValue !== null && (!Number.isInteger(maxTokensValue) || maxTokensValue < 1)
      ? 'Max tokens must be a whole number of 1 or more.'
      : null

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && handleClose()}>
      <DialogContent className={result ? 'max-w-5xl' : 'max-w-lg'}>
        <DialogHeader>
          <DialogTitle>Replay Inference</DialogTitle>
        </DialogHeader>

        {!result ? (
          <>
            <div className="space-y-4 mt-2">
              <div>
                <label className="text-sm text-zinc-400 mb-1.5 block">Target Instance</label>
                <Select value={selectedInstance} onChange={(e) => setSelectedInstance(e.target.value)}>
                  <option value="">Select an instance...</option>
                  {onlineInstances.map((inst) => (
                    <option key={inst.id} value={inst.id}>
                      {inst.name} ({inst.modelId ?? 'no model'})
                    </option>
                  ))}
                </Select>
                {onlineInstances.length === 0 && (
                  <p className="text-xs text-zinc-500 mt-1">No online instances available.</p>
                )}
                {modelMismatch && (
                  <div className="mt-2 flex gap-2 rounded border border-amber-800/60 bg-amber-950/30 px-2.5 py-2 text-xs text-amber-200/90">
                    <AlertTriangle className="h-3.5 w-3.5 flex-shrink-0 text-amber-400 mt-0.5" />
                    <span>
                      This instance does not serve{' '}
                      <span className="font-mono">{record.model}</span>, which is the model the record
                      ran on, so the replay will fail as asked. Pick one it does have under{' '}
                      <button
                        type="button"
                        className="underline underline-offset-2 hover:text-amber-100"
                        onClick={() => setShowOverrides(true)}
                      >
                        Parameter Overrides
                      </button>
                      {' '}— the comparison marks the model as changed.
                    </span>
                  </div>
                )}
              </div>

              {/* Parameter overrides */}
              <div>
                <button
                  onClick={() => setShowOverrides(!showOverrides)}
                  className="flex items-center gap-1.5 text-xs text-zinc-500 hover:text-zinc-300 transition-colors"
                >
                  <Settings2 className="h-3 w-3" />
                  Parameter Overrides
                  {hasOverrides && <Badge variant="secondary" className="text-[9px] px-1 py-0">active</Badge>}
                  {showOverrides ? <ChevronUp className="h-3 w-3" /> : <ChevronDown className="h-3 w-3" />}
                </button>
                {showOverrides && (
                  <div className="mt-3 space-y-3 rounded-md border border-zinc-800 bg-zinc-900/50 p-3">
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className="text-xs text-zinc-400">Temperature</label>
                        <span className="text-xs font-mono text-zinc-500">
                          {overrideTemperature?.toFixed(2) ?? 'original'}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <Slider
                          value={String(overrideTemperature ?? 0.7)}
                          onChange={(e) => setOverrideTemperature(Number(e.target.value))}
                          min={0}
                          max={2}
                          step={0.05}
                          className="flex-1"
                        />
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-5 px-1.5 text-[10px] text-zinc-600"
                          onClick={() => setOverrideTemperature(undefined)}
                        >
                          Reset
                        </Button>
                      </div>
                    </div>
                    <div className="space-y-1.5">
                      <div className="flex items-center justify-between">
                        <label className="text-xs text-zinc-400">Top-P</label>
                        <span className="text-xs font-mono text-zinc-500">
                          {overrideTopP?.toFixed(2) ?? 'original'}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <Slider
                          value={String(overrideTopP ?? 1.0)}
                          onChange={(e) => setOverrideTopP(Number(e.target.value))}
                          min={0}
                          max={1}
                          step={0.05}
                          className="flex-1"
                        />
                        <Button
                          variant="ghost"
                          size="sm"
                          className="h-5 px-1.5 text-[10px] text-zinc-600"
                          onClick={() => setOverrideTopP(undefined)}
                        >
                          Reset
                        </Button>
                      </div>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-xs text-zinc-400">Max Tokens</label>
                      <Input
                        type="number"
                        min={1}
                        step={1}
                        placeholder="original"
                        value={overrideMaxTokens}
                        onChange={(e) => setOverrideMaxTokens(e.target.value)}
                        className="h-7 text-xs bg-zinc-800"
                      />
                      {maxTokensError && (
                        <p className="text-[11px] text-red-400">{maxTokensError}</p>
                      )}
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-xs text-zinc-400">Model</label>
                      {servedModels.length > 0 ? (
                        <Select
                          value={overrideModel}
                          onChange={(e) => setOverrideModel(e.target.value)}
                          className="h-7 text-xs bg-zinc-800 font-mono"
                        >
                          <option value="">
                            {recordModelIsServed
                              ? `${record.model || 'as recorded'} (as recorded)`
                              : `${record.model} (as recorded — not on this instance)`}
                          </option>
                          {servedModels
                            .filter((m) => m !== record.model)
                            .map((m) => (
                              <option key={m} value={m}>
                                {m}
                              </option>
                            ))}
                        </Select>
                      ) : (
                        <Input
                          placeholder={record.model || 'original'}
                          value={overrideModel}
                          onChange={(e) => setOverrideModel(e.target.value)}
                          className="h-7 text-xs bg-zinc-800 font-mono"
                        />
                      )}
                      <p className="text-[11px] text-zinc-600">
                        {instanceModels && !instanceModels.canList && instanceModels.reason
                          ? instanceModels.reason
                          : 'Leave as recorded to replay the model the record was made with.'}
                      </p>
                    </div>
                  </div>
                )}
              </div>

              <div className="text-xs text-zinc-500">
                The original request will be sent to the selected instance
                {hasOverrides && ' with parameter overrides applied'}.
              </div>
            </div>
            <DialogFooter className="mt-4">
              <Button variant="outline" size="sm" onClick={handleClose}>Cancel</Button>
              <Button
                size="sm"
                className="bg-violet-600 hover:bg-violet-700 text-white"
                onClick={handleReplay}
                disabled={!selectedInstance || maxTokensError !== null || replayMutation.isPending}
              >
                {replayMutation.isPending ? (
                  <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                ) : (
                  <Play className="h-3.5 w-3.5 mr-1.5" />
                )}
                Replay
              </Button>
            </DialogFooter>
          </>
        ) : (
          <div className="mt-2 space-y-4">
            {/* Side-by-side responses with inline diff */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <h4 className="text-xs font-medium text-zinc-400 mb-1.5">Original Response</h4>
                <ScrollArea className="rounded bg-zinc-900 p-3 max-h-[300px]">
                  {result.originalResponseContent === null ? (
                    <p className="text-xs text-zinc-500 italic">
                      The original call failed before it produced a response, so there is nothing to
                      compare against.
                    </p>
                  ) : (
                    <DiffText
                      original={result.originalResponseContent}
                      replay={result.replayResponseContent}
                      side="original"
                    />
                  )}
                </ScrollArea>
              </div>
              <div>
                <h4 className="text-xs font-medium text-zinc-400 mb-1.5">Replay Response</h4>
                <ScrollArea className="rounded bg-zinc-900 p-3 max-h-[300px]">
                  <DiffText
                    original={result.originalResponseContent ?? ''}
                    replay={result.replayResponseContent}
                    side="replay"
                  />
                </ScrollArea>
              </div>
            </div>

            {/* Diff summary, and what the sampling settings let you conclude from it */}
            <div className="space-y-1.5">
              {result.diffSummary && (
                <div className="rounded bg-zinc-900 px-3 py-2 text-xs font-mono text-zinc-400">
                  {result.diffSummary}
                </div>
              )}
              <SamplingCaveat
                temperature={effectiveTemperature}
                identical={result.originalResponseContent === result.replayResponseContent}
              />
            </div>

            <Separator />

            {/* Metrics comparison */}
            <div>
              <h4 className="text-xs font-medium text-zinc-400 mb-2">Metrics Comparison</h4>
              <div className="rounded border border-zinc-800 overflow-hidden">
                <div className="grid grid-cols-4 text-xs">
                  <div className="bg-zinc-800/50 px-3 py-2 font-medium text-zinc-400">Metric</div>
                  <div className="bg-zinc-800/50 px-3 py-2 font-medium text-zinc-400">Original</div>
                  <div className="bg-zinc-800/50 px-3 py-2 font-medium text-zinc-400">Replay</div>
                  <div className="bg-zinc-800/50 px-3 py-2 font-medium text-zinc-400">Delta</div>

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Model</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono truncate">{record.model}</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono truncate">{result.replayModel}</div>
                  <div className="px-3 py-2 text-zinc-500 border-t border-zinc-800">
                    {record.model !== result.replayModel ? (
                      <Badge variant="outline" className="text-[9px] border-amber-700 text-amber-400">changed</Badge>
                    ) : '--'}
                  </div>

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Latency</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{record.latencyMs}ms</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{result.replayLatencyMs}ms</div>
                  <DeltaCell original={record.latencyMs} replay={result.replayLatencyMs} suffix="ms" lowerIsBetter />

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Prompt Tokens</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{record.promptTokens}</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{result.replayPromptTokens}</div>
                  <DeltaCell original={record.promptTokens} replay={result.replayPromptTokens} />

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Completion Tokens</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{record.completionTokens}</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">{result.replayCompletionTokens}</div>
                  <DeltaCell original={record.completionTokens} replay={result.replayCompletionTokens} />
                </div>
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" size="sm" onClick={handleClose}>Close</Button>
            </DialogFooter>
          </div>
        )}

        {replayMutation.isError && (
          <p className="text-xs text-red-400 mt-2">
            Replay failed: {describeMutationError(replayMutation.error)}
          </p>
        )}
      </DialogContent>
    </Dialog>
  )
}

/**
 * Says what the sampling settings allow the reader to conclude from the comparison.
 *
 * Without this the screen shows a red-and-green diff and a character count for a replay that
 * sampled, which reads as a finding. It is not one: at any temperature above zero two runs of
 * an unchanged model on unchanged hardware differ, and a researcher who takes that as drift has
 * been misled by the presentation rather than by the data.
 */
function SamplingCaveat({ temperature, identical }: { temperature: number | null; identical: boolean }) {
  if (temperature === null) {
    return (
      <p className="text-[11px] text-zinc-500">
        The recorded request did not state a temperature, so whether the server sampled — and
        therefore whether a difference means anything — is not known from here.
      </p>
    )
  }

  if (temperature > 0) {
    return (
      <p className="text-[11px] text-amber-300/80">
        Sampling was on (temperature {temperature}). Two runs differ under sampling even when
        nothing has changed, so a difference here is not by itself evidence of drift — replay at
        temperature 0 to compare deterministically.
      </p>
    )
  }

  return (
    <p className="text-[11px] text-zinc-500">
      {identical
        ? 'Temperature 0: greedy decoding reproduced the original exactly.'
        : 'Temperature 0: with greedy decoding a difference points at something real — the model, the server or the parameters.'}
    </p>
  )
}

/** Word-level diff highlighting, aligned by longest common subsequence. */
function DiffText({ original, replay, side }: { original: string; replay: string; side: 'original' | 'replay' }) {
  const text = side === 'original' ? original : replay

  if (original === replay) {
    return <pre className="text-xs font-mono text-zinc-300 whitespace-pre-wrap">{text}</pre>
  }

  const diff = diffWords(original, replay)
  const segments = side === 'original' ? diff.original : diff.replay

  return (
    <pre className="text-xs font-mono whitespace-pre-wrap">
      {segments.map((segment, i) => (
        <span
          key={i}
          className={cn(
            segment.changed && segment.text.trim()
              ? side === 'original'
                ? 'bg-red-900/30 text-red-300'
                : 'bg-emerald-900/30 text-emerald-300'
              : 'text-zinc-300'
          )}
        >
          {segment.text}
        </span>
      ))}
    </pre>
  )
}

/**
 * One delta in the metrics table.
 *
 * `lowerIsBetter` opts a row into good/bad colouring. Rows that leave it unset are shown in
 * neutral grey, because for a token count neither direction is an improvement — colouring
 * "+15 prompt tokens" green claimed a verdict the number does not support.
 */
function DeltaCell({
  original,
  replay,
  suffix = '',
  lowerIsBetter,
}: {
  original: number
  replay: number
  suffix?: string
  lowerIsBetter?: boolean
}) {
  const delta = replay - original
  if (delta === 0) {
    return <div className="px-3 py-2 text-zinc-500 border-t border-zinc-800 font-mono">0{suffix}</div>
  }
  const color =
    lowerIsBetter === undefined
      ? 'text-zinc-300'
      : (lowerIsBetter ? delta < 0 : delta > 0)
        ? 'text-emerald-400'
        : 'text-red-400'
  const sign = delta > 0 ? '+' : ''
  return (
    <div className={`px-3 py-2 border-t border-zinc-800 font-mono ${color}`}>
      {sign}{delta}{suffix}
    </div>
  )
}
