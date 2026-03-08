import { useState } from 'react'
import { FolderOpen } from 'lucide-react'
import { Input } from '@/components/ui/input'
import { Skeleton } from '@/components/ui/skeleton'
import { useProjects } from './api'
import { useExperimentsStore } from './store'
import { ProjectCard } from './components/ProjectCard'
import { CreateProjectDialog } from './components/CreateProjectDialog'

function ProjectCardSkeleton() {
  return (
    <div className="rounded-lg border border-border bg-card p-5 space-y-3">
      <Skeleton className="h-5 w-40" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-24" />
    </div>
  )
}

export function ExperimentsPage() {
  const [search, setSearch] = useState('')
  const { includeArchived, setIncludeArchived } = useExperimentsStore()
  const { data: projects, isLoading } = useProjects(includeArchived, search || undefined)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Experiments</h1>
          <p className="text-muted-foreground mt-1">
            Organize experiments into projects and track runs.
          </p>
        </div>
        <CreateProjectDialog />
      </div>

      <div className="flex items-center gap-4">
        <Input
          placeholder="Search projects..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          className="max-w-sm"
        />
        <label className="flex items-center gap-2 text-sm text-zinc-400">
          <input
            type="checkbox"
            className="h-4 w-4 rounded border-zinc-700 bg-zinc-900"
            checked={includeArchived}
            onChange={(e) => setIncludeArchived(e.target.checked)}
          />
          Show archived
        </label>
      </div>

      {isLoading ? (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <ProjectCardSkeleton key={i} />
          ))}
        </div>
      ) : !projects || projects.length === 0 ? (
        <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-zinc-700 py-16">
          <FolderOpen className="h-12 w-12 text-zinc-600 mb-4" />
          <h3 className="text-lg font-medium text-zinc-300">No projects yet</h3>
          <p className="text-sm text-zinc-500 mt-1 mb-4">
            Create a project to start organizing your experiments.
          </p>
          <CreateProjectDialog />
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2 xl:grid-cols-3">
          {projects.map((project) => (
            <ProjectCard key={project.id} project={project} />
          ))}
        </div>
      )}
    </div>
  )
}
