import { useState } from 'react'
import { Plus } from 'lucide-react'
import { toast } from 'sonner'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Select } from '@/components/ui/select'
import { DatasetPicker } from '@/features/datasets/components/DatasetPicker'
import { useInstances } from '@/features/models/api'
import { describeMutationError } from '@/services/mutationErrors'
import { useCreateBatchJob } from '../api'

/**
 * Creates a batch job.
 *
 * As with Evaluation, the endpoint and the hook both existed and nothing called either, so the
 * page tracked the progress of jobs it gave you no way to start.
 */
export function CreateBatchJobDialog() {
  const [open, setOpen] = useState(false)
  const [datasetId, setDatasetId] = useState('')
  const [splitLabel, setSplitLabel] = useState('')
  const [model, setModel] = useState('')
  const [concurrency, setConcurrency] = useState(4)
  const [maxRetries, setMaxRetries] = useState(2)
  const [captureLogprobs, setCaptureLogprobs] = useState(false)

  const { data: instances } = useInstances()
  const create = useCreateBatchJob()

  const known = [...new Set((instances ?? []).map((i) => i.modelId).filter(Boolean))] as string[]

  // Logprobs make every stored response substantially larger, which matters far more over a
  // whole dataset than over one chat turn — so it is off unless asked for, and the option is
  // only meaningful when a registered server actually returns them.
  const logprobsAvailable = (instances ?? []).some((instance) => instance.supportsLogprobs)

  const canCreate = datasetId.length > 0 && model.trim().length > 0 && concurrency > 0

  function handleCreate() {
    create.mutate(
      {
        datasetId,
        splitLabel: splitLabel || undefined,
        model: model.trim(),
        concurrency,
        maxRetries,
        captureLogprobs,
      },
      {
        onSuccess: () => {
          toast.success('Batch job queued')
          setOpen(false)
        },
        onError: (error) => toast.error(describeMutationError(error)),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded bg-violet-600 px-4 py-2 text-sm text-white transition-colors hover:bg-violet-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">
        <Plus className="h-4 w-4" />
        New Batch Job
      </DialogTrigger>

      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Run a model over a dataset</DialogTitle>
          <DialogDescription>
            Every record is sent to the model. The job can be paused and resumed, and failed
            records can be retried on their own afterwards.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4 py-4">
          <DatasetPicker
            datasetId={datasetId}
            splitLabel={splitLabel}
            onChange={(nextDataset, nextSplit) => {
              setDatasetId(nextDataset)
              setSplitLabel(nextSplit)
            }}
          />

          <div className="space-y-1">
            <label className="text-sm font-medium text-zinc-300">Model</label>
            {known.length > 0 ? (
              <Select value={model} onChange={(event) => setModel(event.target.value)}>
                <option value="">Select a model...</option>
                {known.map((candidate) => (
                  <option key={candidate} value={candidate}>
                    {candidate}
                  </option>
                ))}
              </Select>
            ) : (
              <Input
                placeholder="Model id"
                value={model}
                onChange={(event) => setModel(event.target.value)}
              />
            )}
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <label className="text-sm font-medium text-zinc-300">Concurrency</label>
              <Input
                type="number"
                min={1}
                max={32}
                value={concurrency}
                onChange={(event) => setConcurrency(Number(event.target.value) || 1)}
              />
              <p className="text-[11px] text-zinc-500">
                How many records run at once. A local server is usually the bottleneck.
              </p>
            </div>

            <div className="space-y-1">
              <label className="text-sm font-medium text-zinc-300">Max retries</label>
              <Input
                type="number"
                min={0}
                max={10}
                value={maxRetries}
                onChange={(event) => setMaxRetries(Number(event.target.value) || 0)}
              />
              <p className="text-[11px] text-zinc-500">
                Per record. Whatever still fails can be retried from the job card.
              </p>
            </div>
          </div>

          <label className="flex cursor-pointer items-start gap-2 text-sm text-zinc-300">
            <input
              type="checkbox"
              className="mt-0.5 h-3.5 w-3.5 accent-violet-500"
              checked={captureLogprobs}
              disabled={!logprobsAvailable}
              onChange={(event) => setCaptureLogprobs(event.target.checked)}
            />
            <span>
              Capture token probabilities
              <span className="mt-0.5 block text-[11px] text-zinc-500">
                {logprobsAvailable
                  ? 'Stores per-token probabilities for every record. Useful later, and much larger.'
                  : 'No registered server reports token probabilities.'}
              </span>
            </span>
          </label>
        </div>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Cancel
          </Button>
          <Button onClick={handleCreate} disabled={!canCreate || create.isPending}>
            {create.isPending ? 'Queueing...' : 'Queue job'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
