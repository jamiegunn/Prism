import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { zodResolver } from '@hookform/resolvers/zod'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'
import { Plus } from 'lucide-react'
import { toast } from 'sonner'
import { useCreateExperiment } from '../api'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  description: z.string().max(2000).optional(),
  hypothesis: z.string().max(2000).optional(),
})

type FormValues = z.infer<typeof schema>

interface CreateExperimentDialogProps {
  projectId: string
}

export function CreateExperimentDialog({ projectId }: CreateExperimentDialogProps) {
  const [open, setOpen] = useState(false)
  const createMutation = useCreateExperiment()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { name: '', description: '', hypothesis: '' },
  })

  function onSubmit(values: FormValues) {
    createMutation.mutate(
      {
        projectId,
        name: values.name,
        description: values.description || undefined,
        hypothesis: values.hypothesis || undefined,
      },
      {
        onSuccess: () => {
          toast.success('Experiment created')
          setOpen(false)
          reset()
        },
        onError: (error) => {
          toast.error(`Failed to create experiment: ${error.message}`)
        },
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger className="inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2 gap-2">
        <Plus className="h-4 w-4" />
        New Experiment
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create Experiment</DialogTitle>
          <DialogDescription>
            Add a new experiment to track runs and compare results.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 py-4">
          <div className="space-y-2">
            <label htmlFor="exp-name" className="text-sm font-medium text-zinc-300">
              Name
            </label>
            <Input id="exp-name" placeholder="Temperature sweep" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-red-500">{errors.name.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <label htmlFor="exp-desc" className="text-sm font-medium text-zinc-300">
              Description (optional)
            </label>
            <Textarea
              id="exp-desc"
              placeholder="What are you testing?"
              rows={2}
              {...register('description')}
            />
          </div>

          <div className="space-y-2">
            <label htmlFor="exp-hypothesis" className="text-sm font-medium text-zinc-300">
              Hypothesis (optional)
            </label>
            <Textarea
              id="exp-hypothesis"
              placeholder="Lower temperature will produce more consistent outputs..."
              rows={2}
              {...register('hypothesis')}
            />
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => { setOpen(false); reset() }}>
              Cancel
            </Button>
            <Button type="submit" disabled={createMutation.isPending}>
              {createMutation.isPending ? 'Creating...' : 'Create'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
