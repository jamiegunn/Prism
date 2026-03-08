import { useState } from 'react'
import { ChevronRight, HelpCircle } from 'lucide-react'
import { cn } from '@/lib/utils'

interface HelpPanelProps {
  title: string
  children: React.ReactNode
}

export function HelpPanel({ title, children }: HelpPanelProps) {
  const [open, setOpen] = useState(false)

  return (
    <div className="mb-3">
      <button
        type="button"
        onClick={() => setOpen(!open)}
        className="flex w-full items-center gap-1.5 rounded-md px-2 py-1.5 text-xs text-zinc-400 hover:bg-zinc-800/50 hover:text-zinc-300 transition-colors"
      >
        <HelpCircle className="h-3.5 w-3.5 shrink-0" />
        <span className="font-medium">{title}</span>
        <ChevronRight
          className={cn(
            'ml-auto h-3.5 w-3.5 shrink-0 transition-transform duration-200',
            open && 'rotate-90'
          )}
        />
      </button>
      {open && (
        <div className="mt-1.5 rounded-md border border-zinc-800 bg-zinc-900/50 px-3 py-2.5 text-xs leading-relaxed text-zinc-400">
          {children}
        </div>
      )}
    </div>
  )
}
