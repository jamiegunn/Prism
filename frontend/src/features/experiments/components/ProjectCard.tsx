import { useNavigate } from 'react-router-dom'
import { FolderOpen, Archive, TestTubes } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { cn } from '@/lib/utils'
import type { Project } from '../types'

interface ProjectCardProps {
  project: Project
}

export function ProjectCard({ project }: ProjectCardProps) {
  const navigate = useNavigate()

  return (
    <button
      onClick={() => navigate(`/experiments/projects/${project.id}`)}
      className={cn(
        'w-full text-left rounded-lg border border-border bg-card p-5 space-y-3 transition-colors hover:border-violet-500/50 hover:bg-zinc-800/50',
        project.isArchived && 'opacity-60'
      )}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <FolderOpen className="h-4 w-4 text-violet-400" />
          <h3 className="font-medium text-zinc-100 truncate">{project.name}</h3>
        </div>
        {project.isArchived && (
          <Badge variant="secondary" className="gap-1">
            <Archive className="h-3 w-3" />
            Archived
          </Badge>
        )}
      </div>

      {project.description && (
        <p className="text-sm text-zinc-400 line-clamp-2">{project.description}</p>
      )}

      <div className="flex items-center gap-4 text-xs text-zinc-500">
        <span className="flex items-center gap-1">
          <TestTubes className="h-3 w-3" />
          {project.experimentCount} experiment{project.experimentCount !== 1 ? 's' : ''}
        </span>
        <span>{new Date(project.createdAt).toLocaleDateString()}</span>
      </div>
    </button>
  )
}
