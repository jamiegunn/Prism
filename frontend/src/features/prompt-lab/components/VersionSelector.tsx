import { useState } from 'react'
import { GitBranch, Plus } from 'lucide-react'
import { Button } from '@/components/ui/button'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Textarea } from '@/components/ui/textarea'
import { Input } from '@/components/ui/input'
import { toast } from 'sonner'
import { useCreateVersion } from '../api'
import type { PromptVersion } from '../types'

interface VersionSelectorProps {
  versions: PromptVersion[]
  currentVersion: number
  onSelect: (version: number) => void
  templateId: string
}

export function VersionSelector({
  versions,
  currentVersion,
  onSelect,
  templateId,
}: VersionSelectorProps) {
  const [showNewVersion, setShowNewVersion] = useState(false)
  const [newTemplate, setNewTemplate] = useState('')
  const [newSystemPrompt, setNewSystemPrompt] = useState('')
  const [newNotes, setNewNotes] = useState('')
  const createVersion = useCreateVersion()

  function handleCreate() {
    if (!newTemplate.trim()) {
      toast.error('Template body is required')
      return
    }
    createVersion.mutate(
      {
        templateId,
        userTemplate: newTemplate,
        systemPrompt: newSystemPrompt || undefined,
        notes: newNotes || undefined,
      },
      {
        onSuccess: (version) => {
          toast.success(`Version ${version.version} created`)
          onSelect(version.version)
          setShowNewVersion(false)
          setNewTemplate('')
          setNewSystemPrompt('')
          setNewNotes('')
        },
        onError: (error) => toast.error(`Failed: ${error.message}`),
      }
    )
  }

  // Pre-fill from current version
  function handleOpenNew() {
    const current = versions.find((v) => v.version === currentVersion)
    if (current) {
      setNewTemplate(current.userTemplate)
      setNewSystemPrompt(current.systemPrompt ?? '')
    }
    setShowNewVersion(true)
  }

  return (
    <div className="flex items-center gap-2">
      <GitBranch className="h-4 w-4 text-zinc-500" />
      <select
        value={currentVersion}
        onChange={(e) => onSelect(Number(e.target.value))}
        className="bg-zinc-900 border border-border rounded px-2 py-1 text-sm text-zinc-300"
      >
        {versions.map((v) => (
          <option key={v.version} value={v.version}>
            v{v.version}
            {v.notes ? ` — ${v.notes}` : ''}
          </option>
        ))}
      </select>

      <Button size="sm" variant="outline" className="gap-1" onClick={handleOpenNew}>
        <Plus className="h-3 w-3" />
        New Version
      </Button>

      <Dialog open={showNewVersion} onOpenChange={setShowNewVersion}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Create New Version</DialogTitle>
          </DialogHeader>

          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <label className="text-sm font-medium text-zinc-300">System Prompt</label>
              <Textarea
                value={newSystemPrompt}
                onChange={(e) => setNewSystemPrompt(e.target.value)}
                rows={2}
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-zinc-300">User Template</label>
              <Textarea
                value={newTemplate}
                onChange={(e) => setNewTemplate(e.target.value)}
                rows={6}
                className="font-mono text-sm"
              />
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium text-zinc-300">Notes</label>
              <Input
                value={newNotes}
                onChange={(e) => setNewNotes(e.target.value)}
                placeholder="What changed in this version?"
              />
            </div>
          </div>

          <DialogFooter>
            <Button variant="outline" onClick={() => setShowNewVersion(false)}>
              Cancel
            </Button>
            <Button onClick={handleCreate} disabled={createVersion.isPending}>
              {createVersion.isPending ? 'Creating...' : 'Create Version'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  )
}
