import { NavLink } from 'react-router-dom'
import {
  MessageSquare,
  Microscope,
  Hash,
  Server,
  Clock,
  FlaskConical,
  TestTubes,
  Database,
  CheckCircle2,
  Layers,
  BarChart3,
  BookOpen,
  Braces,
  Bot,
  NotebookPen,
  Diamond,
  Wand2,
  Compass,
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { WorkspaceSwitcher } from '@/features/workspaces/WorkspaceSwitcher'
import { useTourStore } from '@/features/tour/store'

interface NavItem {
  label: string
  icon: React.ComponentType<{ className?: string }>
  path: string
  active: boolean
  phase?: number
}

const navItems: NavItem[] = [
  { label: 'Playground', icon: MessageSquare, path: '/playground', active: true },
  { label: 'Token Explorer', icon: Microscope, path: '/token-explorer', active: true },
  { label: 'Tokenizer', icon: Hash, path: '/tokenizer', active: true },
  { label: 'Models', icon: Server, path: '/models', active: true },
  { label: 'History', icon: Clock, path: '/history', active: true },
  { label: 'Prompt Lab', icon: FlaskConical, path: '/prompt-lab', active: true },
  { label: 'Experiments', icon: TestTubes, path: '/experiments', active: true },
  { label: 'Datasets', icon: Database, path: '/datasets', active: true },
  { label: 'Evaluation', icon: CheckCircle2, path: '/evaluation', active: true },
  { label: 'Batch Inference', icon: Layers, path: '/batch', active: true },
  { label: 'Analytics', icon: BarChart3, path: '/analytics', active: true },
  { label: 'RAG Workbench', icon: BookOpen, path: '/rag', active: true },
  { label: 'Structured Output', icon: Braces, path: '/structured-output', active: true },
  { label: 'Agents', icon: Bot, path: '/agents', active: true },
  { label: 'Fine-Tuning', icon: Wand2, path: '/fine-tuning', active: true },
  { label: 'Notebooks', icon: NotebookPen, path: '/notebooks', active: true },
]

export function Sidebar() {
  const setPanelOpen = useTourStore((state) => state.setPanelOpen)

  return (
    <div
      data-tour="sidebar"
      className="fixed inset-y-0 left-0 z-30 flex w-64 flex-col border-r border-zinc-800 bg-zinc-900"
    >
      <div className="flex h-14 items-center gap-2 border-b border-zinc-800 px-4">
        <Diamond className="h-6 w-6 text-violet-500" />
        <span className="text-lg font-bold tracking-tight text-zinc-50">Prism</span>

        <button
          type="button"
          data-tour="guide-button"
          onClick={() => setPanelOpen(true)}
          title="Guide and walkthroughs"
          className="ml-auto rounded-md p-1.5 text-zinc-500 transition-colors hover:bg-zinc-800 hover:text-zinc-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-violet-500"
        >
          <Compass className="h-4 w-4" />
          <span className="sr-only">Open the guide</span>
        </button>
      </div>

      <WorkspaceSwitcher />

      <ScrollArea className="flex-1 py-2">
        <nav className="flex flex-col gap-1 px-2">
          {navItems.map((item) => {
            const Icon = item.icon

            if (!item.active) {
              return (
                <div
                  key={item.label}
                  className="flex items-center gap-3 rounded-md px-3 py-2 text-sm text-zinc-500 cursor-not-allowed"
                >
                  <Icon className="h-4 w-4" />
                  <span className="flex-1">{item.label}</span>
                  <Badge variant="secondary" className="text-[10px] px-1.5 py-0 text-zinc-500">
                    Phase {item.phase}
                  </Badge>
                </div>
              )
            }

            return (
              <NavLink
                key={item.label}
                to={item.path}
                // Anchors the tour to nav items by route rather than by label, so renaming
                // one in the sidebar cannot silently unhook a walkthrough step.
                data-tour={`nav-${item.path.replace(/^\//, '')}`}
                className={({ isActive }) =>
                  cn(
                    'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
                    isActive
                      ? 'border-l-2 border-violet-500 bg-zinc-800 text-white'
                      : 'text-zinc-400 hover:bg-zinc-800/50 hover:text-zinc-50'
                  )
                }
              >
                <Icon className="h-4 w-4" />
                <span>{item.label}</span>
              </NavLink>
            )
          })}
        </nav>
      </ScrollArea>
    </div>
  )
}
