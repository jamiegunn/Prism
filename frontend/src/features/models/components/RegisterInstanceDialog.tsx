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
import { Select } from '@/components/ui/select'
import { Plus } from 'lucide-react'
import { toast } from 'sonner'
import { useRegisterInstance } from '../api'

const registerSchema = z.object({
  name: z.string().min(1, 'Name is required').max(100),
  endpoint: z.string().url('Must be a valid URL'),
  providerType: z.string().min(1, 'Provider type is required'),
  isDefault: z.boolean().optional(),
  tags: z.string().optional(),
})

type RegisterFormValues = z.infer<typeof registerSchema>

const PROVIDER_TYPES = [
  { value: 'vLLM', label: 'vLLM' },
  { value: 'Ollama', label: 'Ollama' },
  { value: 'LMStudio', label: 'LM Studio' },
  { value: 'OpenAICompatible', label: 'OpenAI Compatible' },
]

export function RegisterInstanceDialog() {
  const [open, setOpen] = useState(false)
  const registerMutation = useRegisterInstance()

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      name: '',
      endpoint: '',
      providerType: '',
      isDefault: false,
      tags: '',
    },
  })

  function onSubmit(values: RegisterFormValues) {
    const tags = values.tags
      ? values.tags.split(',').map((t) => t.trim()).filter(Boolean)
      : undefined

    registerMutation.mutate(
      {
        name: values.name,
        endpoint: values.endpoint,
        providerType: values.providerType,
        isDefault: values.isDefault,
        tags,
      },
      {
        onSuccess: () => {
          toast.success('Instance registered successfully')
          setOpen(false)
          reset()
        },
        onError: (error) => {
          toast.error(`Failed to register: ${error.message}`)
        },
      }
    )
  }

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger className="inline-flex items-center justify-center whitespace-nowrap rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 bg-primary text-primary-foreground hover:bg-primary/90 h-10 px-4 py-2 gap-2">
        <Plus className="h-4 w-4" />
        Register Instance
      </DialogTrigger>

      <DialogContent>
        <DialogHeader>
          <DialogTitle>Register Inference Instance</DialogTitle>
          <DialogDescription>
            Connect a new inference provider to the workbench.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4 py-4">
          <div className="space-y-2">
            <label htmlFor="name" className="text-sm font-medium text-zinc-300">
              Name
            </label>
            <Input id="name" placeholder="My vLLM Server" {...register('name')} />
            {errors.name && (
              <p className="text-xs text-red-500">{errors.name.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <label htmlFor="endpoint" className="text-sm font-medium text-zinc-300">
              Endpoint URL
            </label>
            <Input
              id="endpoint"
              placeholder="http://localhost:8000"
              {...register('endpoint')}
            />
            {errors.endpoint && (
              <p className="text-xs text-red-500">{errors.endpoint.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <label htmlFor="providerType" className="text-sm font-medium text-zinc-300">
              Provider Type
            </label>
            <Select id="providerType" {...register('providerType')}>
              <option value="">Select a provider...</option>
              {PROVIDER_TYPES.map((pt) => (
                <option key={pt.value} value={pt.value}>
                  {pt.label}
                </option>
              ))}
            </Select>
            {errors.providerType && (
              <p className="text-xs text-red-500">{errors.providerType.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <label htmlFor="tags" className="text-sm font-medium text-zinc-300">
              Tags (comma-separated, optional)
            </label>
            <Input id="tags" placeholder="gpu, production" {...register('tags')} />
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              id="isDefault"
              className="h-4 w-4 rounded border-zinc-700 bg-zinc-900"
              {...register('isDefault')}
            />
            <label htmlFor="isDefault" className="text-sm text-zinc-300">
              Set as default instance
            </label>
          </div>

          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => {
                setOpen(false)
                reset()
              }}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={registerMutation.isPending}>
              {registerMutation.isPending ? 'Registering...' : 'Register'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
