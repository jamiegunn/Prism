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
import { cn } from '@/lib/utils'
import { DatasetPicker } from '@/features/datasets/components/DatasetPicker'
import { useInstances } from '@/features/models/api'
import { describeMutationError } from '@/services/mutationErrors'
import { useStartEvaluation } from '../api'

/**
 * The scorers the backend implements, with what each actually measures.
 *
 * Hard-coded because the API exposes no catalogue endpoint — the names were previously only
 * discoverable by reading `Evaluation/Domain/Scorers`, which is why the page could only ever
 * show scorers that some earlier API call had already used.
 */
const SCORERS: { id: string; label: string; blurb: string }[] = [
  { id: 'exact_match', label: 'Exact match', blurb: 'Identical to the expected value, or not' },
  { id: 'contains', label: 'Contains', blurb: 'The expected value appears somewhere in the answer' },
  { id: 'rouge_l', label: 'ROUGE-L', blurb: 'Longest common subsequence — overlap, order-aware' },
  { id: 'bleu', label: 'BLEU', blurb: 'N-gram precision against the expected text' },
  { id: 'length_ratio', label: 'Length ratio', blurb: 'Answer length over expected length' },
  { id: 'llm_judge', label: 'LLM judge', blurb: 'A model grades the answer. Costs inference.' },
]

/**
 * Starts an evaluation.
 *
 * Until now there was no way to do this from the UI at all: the endpoint existed, the hook
 * existed, and nothing called it, so the page listed runs it gave you no means of creating. The
 * only route in was a hand-written POST carrying two GUIDs that the app never displays.
 */
export function StartEvaluationDialog() {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [datasetId, setDatasetId] = useState('')
  const [splitLabel, setSplitLabel] = useState('')
  const [models, setModels] = useState<string[]>([])
  const [scorers, setScorers] = useState<string[]>(['exact_match'])

  const { data: instances } = useInstances()
  const start = useStartEvaluation()

  // The models to compare are model ids, not instance ids. Offering the registered instances'
  // models is right for the common case and still leaves the field editable below.
  const known = [...new Set((instances ?? []).map((i) => i.modelId).filter(Boolean))] as string[]

  const canStart =
    name.trim().length > 0 && datasetId.length > 0 && models.length > 0 && scorers.length > 0

  function toggle(list: string[], value: string): string[] {
    return list.includes(value) ? list.filter((entry) => entry !== value) : [...list, value]
  }

  function handleStart() {
    start.mutate(
      {
        name: name.trim(),
        datasetId,
        splitLabel: splitLabel || undefined,
        models,
        scoringMethods: scorers,
      },
      {
        onSuccess: () => {
          toast.success('Evaluation started')
          setOpen(false)
          setName('')
          setModels([])
        },
        onError: (error) => toast.error(describeMutationError(error)),
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground transition-colors hover:bg-primary/90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2">
        <Plus className="h-4 w-4" />
        New Evaluation
      </DialogTrigger>

      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>Start an evaluation</DialogTitle>
          <DialogDescription>
            Every record is run through every model you pick, and each answer is scored against
            the dataset&apos;s expected value.
          </DialogDescription>
        </DialogHeader>

        <div className="max-h-[60vh] space-y-4 overflow-y-auto py-4">
          <div className="space-y-1">
            <label className="text-sm font-medium text-zinc-300">Name</label>
            <Input
              placeholder="Summarisation — 8B vs 70B"
              value={name}
              onChange={(event) => setName(event.target.value)}
            />
          </div>

          <DatasetPicker
            datasetId={datasetId}
            splitLabel={splitLabel}
            onChange={(nextDataset, nextSplit) => {
              setDatasetId(nextDataset)
              setSplitLabel(nextSplit)
            }}
          />

          <div className="space-y-1">
            <label className="text-sm font-medium text-zinc-300">
              Models to compare
              <span className="ml-2 font-normal text-zinc-500">{models.length} selected</span>
            </label>

            {known.length > 0 ? (
              <div className="flex flex-wrap gap-2">
                {known.map((model) => (
                  <button
                    key={model}
                    type="button"
                    onClick={() => setModels((current) => toggle(current, model))}
                    className={cn(
                      'rounded-full border px-3 py-1 text-xs transition-colors',
                      models.includes(model)
                        ? 'border-violet-500 bg-violet-500/15 text-violet-200'
                        : 'border-zinc-700 text-zinc-400 hover:border-zinc-600'
                    )}
                  >
                    {model}
                  </button>
                ))}
              </div>
            ) : (
              <p className="text-xs text-zinc-500">
                No registered servers report a model. Connect one on the Models page, or type a
                model id below.
              </p>
            )}

            <Input
              className="mt-2"
              placeholder="Add a model id and press Enter"
              onKeyDown={(event) => {
                if (event.key !== 'Enter') return
                event.preventDefault()

                const value = event.currentTarget.value.trim()
                if (!value) return

                setModels((current) => (current.includes(value) ? current : [...current, value]))
                event.currentTarget.value = ''
              }}
            />

            {models.length > 0 && (
              <p className="pt-1 text-xs text-zinc-500">Comparing: {models.join(', ')}</p>
            )}
          </div>

          <div className="space-y-1">
            <label className="text-sm font-medium text-zinc-300">Scoring</label>
            <div className="grid gap-1.5 sm:grid-cols-2">
              {SCORERS.map((scorer) => (
                <label
                  key={scorer.id}
                  className="flex cursor-pointer items-start gap-2 rounded border border-zinc-800 p-2 hover:border-zinc-700"
                >
                  <input
                    type="checkbox"
                    className="mt-0.5 h-3.5 w-3.5 accent-violet-500"
                    checked={scorers.includes(scorer.id)}
                    onChange={() => setScorers((current) => toggle(current, scorer.id))}
                  />
                  <span className="min-w-0">
                    <span className="block text-xs font-medium text-zinc-200">{scorer.label}</span>
                    <span className="block text-[11px] leading-snug text-zinc-500">
                      {scorer.blurb}
                    </span>
                  </span>
                </label>
              ))}
            </div>
          </div>

          {/* Said before the click, not after: this runs records x models inference calls. */}
          {canStart && (
            <p className="text-xs text-zinc-500">
              This runs every selected record through {models.length}{' '}
              {models.length === 1 ? 'model' : 'models'}
              {scorers.includes('llm_judge') ? ', plus a judging call per answer' : ''}.
            </p>
          )}
        </div>

        <div className="flex justify-end gap-2">
          <Button variant="ghost" onClick={() => setOpen(false)}>
            Cancel
          </Button>
          <Button onClick={handleStart} disabled={!canStart || start.isPending}>
            {start.isPending ? 'Starting...' : 'Start evaluation'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  )
}
