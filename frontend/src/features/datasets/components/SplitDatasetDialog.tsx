import { useState } from 'react'
import { Scissors } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
  DialogClose,
} from '@/components/ui/dialog'
import { useSplitDataset } from '../api'

interface SplitDatasetDialogProps {
  datasetId: string
}

export function SplitDatasetDialog({ datasetId }: SplitDatasetDialogProps) {
  const [trainRatio, setTrainRatio] = useState(0.7)
  const [testRatio, setTestRatio] = useState(0.2)
  const [valRatio, setValRatio] = useState(0.1)
  const [seed, setSeed] = useState<string>('')
  const split = useSplitDataset()

  const total = trainRatio + testRatio + valRatio
  const isValid = Math.abs(total - 1.0) < 0.001

  function handleSubmit() {
    if (!isValid) return
    split.mutate({
      id: datasetId,
      trainRatio,
      testRatio,
      valRatio,
      seed: seed ? parseInt(seed, 10) : null,
    })
  }

  return (
    <Dialog>
      <DialogTrigger className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors border border-input bg-background hover:bg-accent hover:text-accent-foreground h-9 px-4 py-2">
        <Scissors className="h-4 w-4" />
        Split
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Split Dataset</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div className="grid grid-cols-3 gap-3">
            <div>
              <label className="text-sm font-medium">Train</label>
              <Input
                type="number"
                min={0}
                max={1}
                step={0.05}
                value={trainRatio}
                onChange={(e) => setTrainRatio(parseFloat(e.target.value) || 0)}
              />
            </div>
            <div>
              <label className="text-sm font-medium">Test</label>
              <Input
                type="number"
                min={0}
                max={1}
                step={0.05}
                value={testRatio}
                onChange={(e) => setTestRatio(parseFloat(e.target.value) || 0)}
              />
            </div>
            <div>
              <label className="text-sm font-medium">Validation</label>
              <Input
                type="number"
                min={0}
                max={1}
                step={0.05}
                value={valRatio}
                onChange={(e) => setValRatio(parseFloat(e.target.value) || 0)}
              />
            </div>
          </div>
          <p className={`text-xs ${isValid ? 'text-muted-foreground' : 'text-destructive'}`}>
            Total: {total.toFixed(2)} {isValid ? '(valid)' : '(must equal 1.0)'}
          </p>
          <div>
            <label className="text-sm font-medium">Random Seed (optional)</label>
            <Input
              type="number"
              value={seed}
              onChange={(e) => setSeed(e.target.value)}
              placeholder="42"
            />
          </div>
        </div>
        <DialogFooter>
          <DialogClose className="inline-flex items-center justify-center rounded-md text-sm font-medium border border-input bg-background hover:bg-accent hover:text-accent-foreground h-9 px-4 py-2">
            Cancel
          </DialogClose>
          <Button onClick={handleSubmit} disabled={!isValid || split.isPending}>
            {split.isPending ? 'Splitting...' : 'Split'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
