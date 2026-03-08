import { NavLink } from 'react-router-dom'
import {
  MessageSquare,
  Microscope,
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
} from 'lucide-react'
import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'

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
  { label: 'Agents', icon: Bot, path: '/coming-soon/5', active: false, phase: 5 },
  { label: 'Notebooks', icon: NotebookPen, path: '/coming-soon/5', active: false, phase: 5 },
]

export function Sidebar() {
  return (
    <div className="fixed inset-y-0 left-0 z-30 flex w-64 flex-col border-r border-zinc-800 bg-zinc-900">
      <div className="flex h-14 items-center gap-2 border-b border-zinc-800 px-4">
        <Diamond className="h-6 w-6 text-violet-500" />
        <span className="text-lg font-bold tracking-tight text-zinc-50">Prism</span>
      </div>

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
