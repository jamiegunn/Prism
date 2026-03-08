import { useState } from 'react'
import { Server } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import { useInstances } from './api'
import { InstanceCard } from './components/InstanceCard'
import { InstanceDetailPanel } from './components/InstanceDetailPanel'
import { RegisterInstanceDialog } from './components/RegisterInstanceDialog'
import type { InferenceInstance } from './types'

function InstanceCardSkeleton() {
  return (
    <div className="rounded-lg border border-border bg-card p-6 space-y-3">
      <div className="flex items-center justify-between">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-3 w-16" />
      </div>
      <Skeleton className="h-5 w-20" />
      <div className="space-y-2">
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-3/4" />
      </div>
      <div className="flex gap-1">
        <Skeleton className="h-5 w-16" />
        <Skeleton className="h-5 w-14" />
        <Skeleton className="h-5 w-18" />
      </div>
    </div>
  )
}

function EmptyState() {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed border-zinc-700 py-16">
      <Server className="h-12 w-12 text-zinc-600 mb-4" />
      <h3 className="text-lg font-medium text-zinc-300">No inference providers registered</h3>
      <p className="text-sm text-zinc-500 mt-1 mb-4">
        Register an inference provider to get started.
      </p>
      <RegisterInstanceDialog />
    </div>
  )
}

export function ModelsPage() {
  const { data: instances, isLoading } = useInstances()
  const [selectedId, setSelectedId] = useState<string | null>(null)

  const selectedInstance: InferenceInstance | undefined = instances?.find(
    (inst) => inst.id === selectedId
  )

  function handleSelect(id: string) {
    setSelectedId((prev) => (prev === id ? null : id))
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Model Management</h1>
          <p className="text-muted-foreground mt-1">
            View connected providers, loaded models, and GPU utilization.
          </p>
        </div>
        {instances && instances.length > 0 && <RegisterInstanceDialog />}
      </div>

      <div className="flex gap-6">
        {/* Left: Instance Grid */}
        <div className="flex-1 min-w-0">
          {isLoading ? (
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <InstanceCardSkeleton key={i} />
              ))}
            </div>
          ) : !instances || instances.length === 0 ? (
            <EmptyState />
          ) : (
            <div className="grid grid-cols-1 gap-4 xl:grid-cols-2">
              {instances.map((instance) => (
                <InstanceCard
                  key={instance.id}
                  instance={instance}
                  isSelected={instance.id === selectedId}
                  onClick={handleSelect}
                />
              ))}
            </div>
          )}
        </div>

        {/* Right: Detail Panel */}
        <div className="hidden w-96 shrink-0 lg:block">
          {selectedInstance ? (
            <InstanceDetailPanel
              instance={selectedInstance}
              onRemoved={() => setSelectedId(null)}
            />
          ) : (
            <div className="flex h-64 items-center justify-center rounded-lg border border-dashed border-zinc-700">
              <p className="text-sm text-zinc-500">Select an instance to view details</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
