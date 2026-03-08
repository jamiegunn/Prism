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
import { useCreateTemplate } from '../api'

const schema = z.object({
  name: z.string().min(1, 'Name is required').max(200),
  userTemplate: z.string().min(1, 'Template body is required'),
  category: z.string().max(100).optional(),
  description: z.string().max(2000).optional(),
  systemPrompt: z.string().optional(),
  tags: z.string().optional(),
})

type FormValues = z.infer<typeof schema>

export function CreateTemplateDialog() {
  const [open, setOpen] = useState(false)
  const createMutation = useCreateTemplate()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: '',
      userTemplate: '',
      category: '',
      description: '',
      systemPrompt: '',
      tags: '',
    },
  })

  function onSubmit(values: FormValues) {
    const tags = values.tags
      ? values.tags.split(',').map((t) => t.trim()).filter(Boolean)
      : undefined

    createMutation.mutate(
      {
        name: values.name,
        userTemplate: values.userTemplate,
        category: values.category || undefined,
        description: values.description || undefined,
        systemPrompt: values.systemPrompt || undefined,
        tags,
      },
      {
        onSuccess: () => {
          toast.success('Template created')
          setOpen(false)
          reset()
        },
        onError: (error) => {
          toast.error(`Failed to create template: ${error.message}`)
        },
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger className="inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2 gap-2">
        <Plus className="h-4 w-4" />
        New Template
      </DialogTrigger>

      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Create Prompt Template</DialogTitle>
          <DialogDescription>
            Use {'{{variable}}'} syntax for dynamic values.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 py-4">
          <div className="space-y-2">
            <label htmlFor="tpl-name" className="text-sm font-medium text-zinc-300">
              Name
            </label>
            <Input id="tpl-name" placeholder="Summarization prompt" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-red-500">{errors.name.message}</p>
            )}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <label htmlFor="tpl-category" className="text-sm font-medium text-zinc-300">
                Category
              </label>
              <Input id="tpl-category" placeholder="Summarization" {...register('category')} />
            </div>
            <div className="space-y-2">
              <label htmlFor="tpl-tags" className="text-sm font-medium text-zinc-300">
                Tags (comma-separated)
              </label>
              <Input id="tpl-tags" placeholder="research, nlp" {...register('tags')} />
            </div>
          </div>

          <div className="space-y-2">
            <label htmlFor="tpl-desc" className="text-sm font-medium text-zinc-300">
              Description
            </label>
            <Input id="tpl-desc" placeholder="What does this prompt do?" {...register('description')} />
          </div>

          <div className="space-y-2">
            <label htmlFor="tpl-system" className="text-sm font-medium text-zinc-300">
              System Prompt (optional)
            </label>
            <Textarea
              id="tpl-system"
              placeholder="You are a helpful assistant..."
              rows={2}
              {...register('systemPrompt')}
            />
          </div>

          <div className="space-y-2">
            <label htmlFor="tpl-body" className="text-sm font-medium text-zinc-300">
              User Template
            </label>
            <Textarea
              id="tpl-body"
              placeholder="Summarize the following text: {{text}}"
              rows={4}
              className="font-mono text-sm"
              {...register('userTemplate')}
            />
            {errors.userTemplate && (
              <p className="text-xs text-red-500">{errors.userTemplate.message}</p>
            )}
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
