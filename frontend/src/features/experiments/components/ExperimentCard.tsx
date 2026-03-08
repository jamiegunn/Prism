import { useNavigate } from 'react-router-dom'
import { TestTubes, Play, CheckCircle2, Archive } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import type { Experiment, ExperimentStatus } from '../types'

const statusConfig: Record<ExperimentStatus, { icon: typeof Play; label: string; className: string }> = {
  Active: { icon: Play, label: 'Active', className: 'bg-green-500/10 text-green-400 border-green-500/20' },
  Completed: { icon: CheckCircle2, label: 'Completed', className: 'bg-blue-500/10 text-blue-400 border-blue-500/20' },
  Archived: { icon: Archive, label: 'Archived', className: 'bg-zinc-500/10 text-zinc-400 border-zinc-500/20' },
}

interface ExperimentCardProps {
  experiment: Experiment
}

export function ExperimentCard({ experiment }: ExperimentCardProps) {
  const navigate = useNavigate()
  const config = statusConfig[experiment.status]
  const StatusIcon = config.icon

  return (
    <button
      onClick={() => navigate(`/experiments/${experiment.id}`)}
      className="w-full text-left rounded-lg border border-border bg-card p-5 space-y-3 transition-colors hover:border-violet-500/50 hover:bg-zinc-800/50"
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <TestTubes className="h-4 w-4 text-violet-400" />
          <h3 className="font-medium text-zinc-100 truncate">{experiment.name}</h3>
        </div>
        <Badge variant="outline" className={cn('gap-1', config.className)}>
          <StatusIcon className="h-3 w-3" />
          {config.label}
        </Badge>
      </div>

      {experiment.description && (
        <p className="text-sm text-zinc-400 line-clamp-2">{experiment.description}</p>
      )}

      {experiment.hypothesis && (
        <p className="text-xs text-zinc-500 italic line-clamp-1">
          Hypothesis: {experiment.hypothesis}
        </p>
      )}

      <div className="flex items-center gap-4 text-xs text-zinc-500">
        <span>{experiment.runCount} run{experiment.runCount !== 1 ? 's' : ''}</span>
        <span>{new Date(experiment.createdAt).toLocaleDateString()}</span>
      </div>
    </button>
  )
}
