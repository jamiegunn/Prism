import { useState } from 'react'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Select } from '@/components/ui/select'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Play, Loader2 } from 'lucide-react'
import { useInstances } from '@/features/playground/api'
import { useReplayRecord } from '../api'
import type { HistoryRecordDetail } from '../types'
import type { ReplayResult } from '../types'

interface ReplayDialogProps {
  record: HistoryRecordDetail
  open: boolean
  onClose: () => void
}

export function ReplayDialog({ record, open, onClose }: ReplayDialogProps) {
  const [selectedInstance, setSelectedInstance] = useState('')
  const { data: instances } = useInstances()
  const replayMutation = useReplayRecord()
  const [result, setResult] = useState<ReplayResult | null>(null)

  const handleReplay = () => {
    if (!selectedInstance) return
    replayMutation.mutate(
      { id: record.id, instanceId: selectedInstance },
      {
        onSuccess: (data) => setResult(data),
      }
    )
  }

  const handleClose = () => {
    setResult(null)
    setSelectedInstance('')
    replayMutation.reset()
    onClose()
  }

  const onlineInstances = instances?.filter((i) => i.status === 'Online') ?? []

  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && handleClose()}>
      <DialogContent className={result ? 'max-w-4xl' : 'max-w-md'}>
        <DialogHeader>
          <DialogTitle>Replay Inference</DialogTitle>
        </DialogHeader>

        {!result ? (
          <>
            <div className="space-y-4 mt-2">
              <div>
                <label className="text-sm text-zinc-400 mb-1.5 block">Target Instance</label>
                <Select
                  value={selectedInstance}
                  onChange={(e) => setSelectedInstance(e.target.value)}
                >
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
              </div>
              <div className="text-xs text-zinc-500">
                The original request will be sent to the selected instance. The response
                will be compared with the original.
              </div>
            </div>
            <DialogFooter className="mt-4">
              <Button variant="outline" size="sm" onClick={handleClose}>
                Cancel
              </Button>
              <Button
                size="sm"
                className="bg-violet-600 hover:bg-violet-700 text-white"
                onClick={handleReplay}
                disabled={!selectedInstance || replayMutation.isPending}
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
            {/* Side-by-side responses */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <h4 className="text-xs font-medium text-zinc-400 mb-1.5">Original Response</h4>
                <ScrollArea className="rounded bg-zinc-900 p-3 max-h-[300px]">
                  <pre className="text-xs font-mono text-zinc-300 whitespace-pre-wrap">
                    {result.original}
                  </pre>
                </ScrollArea>
              </div>
              <div>
                <h4 className="text-xs font-medium text-zinc-400 mb-1.5">Replay Response</h4>
                <ScrollArea className="rounded bg-zinc-900 p-3 max-h-[300px]">
                  <pre className="text-xs font-mono text-zinc-300 whitespace-pre-wrap">
                    {result.replayResponseContent}
                  </pre>
                </ScrollArea>
              </div>
            </div>

            {/* Diff summary */}
            {result.diffSummary && (
              <div>
                <h4 className="text-xs font-medium text-zinc-400 mb-1.5">Diff Summary</h4>
                <div className="rounded bg-zinc-900 p-3 text-xs font-mono text-zinc-300">
                  {result.diffSummary}
                </div>
              </div>
            )}

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
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono truncate">
                    {record.model}
                  </div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono truncate">
                    {result.replayModel}
                  </div>
                  <div className="px-3 py-2 text-zinc-500 border-t border-zinc-800">--</div>

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Latency</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {record.latencyMs}ms
                  </div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {result.replayLatencyMs}ms
                  </div>
                  <DeltaCell
                    original={record.latencyMs}
                    replay={result.replayLatencyMs}
                    suffix="ms"
                    lowerIsBetter
                  />

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Prompt Tokens</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {record.promptTokens}
                  </div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {result.replayPromptTokens}
                  </div>
                  <DeltaCell original={record.promptTokens} replay={result.replayPromptTokens} />

                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800">Completion Tokens</div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {record.completionTokens}
                  </div>
                  <div className="px-3 py-2 text-zinc-300 border-t border-zinc-800 font-mono">
                    {result.replayCompletionTokens}
                  </div>
                  <DeltaCell
                    original={record.completionTokens}
                    replay={result.replayCompletionTokens}
                  />
                </div>
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" size="sm" onClick={handleClose}>
                Close
              </Button>
            </DialogFooter>
          </div>
        )}

        {replayMutation.isError && (
          <p className="text-xs text-red-400 mt-2">
            Replay failed: {replayMutation.error?.message ?? 'Unknown error'}
          </p>
        )}
      </DialogContent>
    </Dialog>
  )
}

function DeltaCell({
  original,
  replay,
  suffix = '',
  lowerIsBetter = false,
}: {
  original: number
  replay: number
  suffix?: string
  lowerIsBetter?: boolean
}) {
  const delta = replay - original
  if (delta === 0) {
    return (
      <div className="px-3 py-2 text-zinc-500 border-t border-zinc-800 font-mono">
        0{suffix}
      </div>
    )
  }
  const isGood = lowerIsBetter ? delta < 0 : delta > 0
  const color = isGood ? 'text-emerald-400' : 'text-red-400'
  const sign = delta > 0 ? '+' : ''
  return (
    <div className={`px-3 py-2 border-t border-zinc-800 font-mono ${color}`}>
      {sign}{delta}{suffix}
    </div>
  )
}
