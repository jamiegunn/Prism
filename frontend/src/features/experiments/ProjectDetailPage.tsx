import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Archive } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { toast } from 'sonner'
import { useProject, useExperiments, useArchiveProject } from './api'
import { ExperimentCard } from './components/ExperimentCard'
import { CreateExperimentDialog } from './components/CreateExperimentDialog'

export function ProjectDetailPage() {
  const { projectId } = useParams<{ projectId: string }>()
  const navigate = useNavigate()
  const { data: project, isLoading: projectLoading } = useProject(projectId ?? null)
  const { data: experiments, isLoading: experimentsLoading } = useExperiments(projectId)
  const archiveMutation = useArchiveProject()

  function handleArchive() {
    if (!projectId) return
    archiveMutation.mutate(projectId, {
      onSuccess: () => {
        toast.success('Project archived')
        navigate('/experiments')
      },
      onError: (error) => toast.error(`Archive failed: ${error.message}`),
    })
  }

  if (projectLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-96" />
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <Skeleton className="h-32" />
          <Skeleton className="h-32" />
        </div>
      </div>
    )
  }

  if (!project) {
    return (
      <div className="flex flex-col items-center py-16">
        <p className="text-zinc-400">Project not found</p>
        <Button variant="outline" className="mt-4" onClick={() => navigate('/experiments')}>
          Back to projects
        </Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => navigate('/experiments')}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div className="flex-1">
          <h1 className="text-3xl font-bold tracking-tight">{project.name}</h1>
          {project.description && (
            <p className="text-muted-foreground mt-1">{project.description}</p>
          )}
        </div>
        <div className="flex gap-2">
          {!project.isArchived && (
            <Button
              variant="outline"
              size="sm"
              className="gap-1"
              onClick={handleArchive}
              disabled={archiveMutation.isPending}
            >
              <Archive className="h-3 w-3" />
              Archive
            </Button>
          )}
          <CreateExperimentDialog projectId={project.id} />
        </div>
      </div>

      {experimentsLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-32" />
          ))}
        </div>
      ) : !experiments || experiments.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-zinc-700 py-16">
          <h3 className="text-lg font-medium text-zinc-300">No experiments yet</h3>
          <p className="text-sm text-zinc-500 mt-1 mb-4">
            Create an experiment to start tracking runs.
          </p>
          <CreateExperimentDialog projectId={project.id} />
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {experiments.map((exp) => (
            <ExperimentCard key={exp.id} experiment={exp} />
          ))}
        </div>
      )}
    </div>
  )
}
