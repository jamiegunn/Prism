import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'
import { Badge } from '@/components/ui/badge'

interface ShortcutEntry {
  keys: string
  description: string
  scope?: string
}

const SHORTCUTS: ShortcutEntry[] = [
  { keys: '?', description: 'Show keyboard shortcuts', scope: 'Global' },
  { keys: 'g p', description: 'Go to Playground', scope: 'Navigation' },
  { keys: 'g t', description: 'Go to Token Explorer', scope: 'Navigation' },
  { keys: 'g m', description: 'Go to Models', scope: 'Navigation' },
  { keys: 'g h', description: 'Go to History', scope: 'Navigation' },
  { keys: 'g l', description: 'Go to Prompt Lab', scope: 'Navigation' },
  { keys: 'g e', description: 'Go to Experiments', scope: 'Navigation' },
]

interface ShortcutsDialogProps {
  open: boolean
  onClose: () => void
}

export function ShortcutsDialog({ open, onClose }: ShortcutsDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Keyboard Shortcuts</DialogTitle>
        </DialogHeader>
        <div className="space-y-1 mt-2">
          {SHORTCUTS.map((shortcut) => (
            <div
              key={shortcut.keys}
              className="flex items-center justify-between py-1.5 text-sm"
            >
              <span className="text-zinc-400">{shortcut.description}</span>
              <div className="flex items-center gap-1">
                {shortcut.keys.split(' ').map((k, i) => (
                  <span key={i}>
                    {i > 0 && <span className="text-zinc-600 mx-0.5">then</span>}
                    <Badge variant="outline" className="font-mono text-xs px-1.5">
                      {k}
                    </Badge>
                  </span>
                ))}
              </div>
            </div>
          ))}
        </div>
      </DialogContent>
    </Dialog>
  )
}
