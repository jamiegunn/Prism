import { useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { ArrowLeft, Download, CheckCircle2, Archive, Shuffle } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { toast } from 'sonner'
import { useExperiment, useRuns, useChangeExperimentStatus } from './api'
import { RunTable } from './components/RunTable'
import { RunStatsSummary } from './components/RunStatsSummary'
import { RunDetailPanel } from './components/RunDetailPanel'
import { RunComparisonView } from './components/RunComparisonView'
import { SweepDialog } from './components/SweepDialog'
import type { Run, RunComparison, ExperimentStatus } from './types'

export function ExperimentDetailPage() {
  const { experimentId } = useParams<{ experimentId: string }>()
  const navigate = useNavigate()
  const { data: experiment, isLoading } = useExperiment(experimentId ?? null)
  const { data: runsData } = useRuns(experimentId ?? '', { pageSize: 100 })
  const statusMutation = useChangeExperimentStatus()

  const [selectedRun, setSelectedRun] = useState<Run | null>(null)
  const [comparison, setComparison] = useState<RunComparison | null>(null)
  const [showSweep, setShowSweep] = useState(false)

  function handleStatusChange(status: ExperimentStatus) {
    if (!experimentId) return
    statusMutation.mutate(
      { id: experimentId, status },
      {
        onSuccess: () => toast.success(`Status changed to ${status}`),
        onError: (error) => toast.error(`Failed: ${error.message}`),
      }
    )
  }

  function handleExport(format: string) {
    if (!experimentId) return
    window.open(`/api/v1/experiments/${experimentId}/runs/export?format=${format}`, '_blank')
  }

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="h-4 w-96" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (!experiment) {
    return (
      <div className="flex flex-col items-center py-16">
        <p className="text-zinc-400">Experiment not found</p>
        <Button variant="outline" className="mt-4" onClick={() => navigate('/experiments')}>
          Back to experiments
        </Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="sm" onClick={() => navigate(-1)}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <div className="flex-1">
          <div className="flex items-center gap-3">
            <h1 className="text-3xl font-bold tracking-tight">{experiment.name}</h1>
            <Badge variant="outline">{experiment.status}</Badge>
          </div>
          {experiment.description && (
            <p className="text-muted-foreground mt-1">{experiment.description}</p>
          )}
          {experiment.hypothesis && (
            <p className="text-sm text-zinc-500 italic mt-1">
              Hypothesis: {experiment.hypothesis}
            </p>
          )}
        </div>
        <div className="flex gap-2">
          {experiment.status === 'Active' && (
            <Button
              variant="outline"
              size="sm"
              className="gap-1"
              onClick={() => handleStatusChange('Completed')}
            >
              <CheckCircle2 className="h-3 w-3" />
              Complete
            </Button>
          )}
          {experiment.status === 'Active' && (
            <Button
              variant="outline"
              size="sm"
              className="gap-1"
              onClick={() => setShowSweep(true)}
            >
              <Shuffle className="h-3 w-3" />
              Sweep
            </Button>
          )}
          {experiment.status !== 'Archived' && (
            <Button
              variant="outline"
              size="sm"
              className="gap-1"
              onClick={() => handleStatusChange('Archived')}
            >
              <Archive className="h-3 w-3" />
              Archive
            </Button>
          )}
          <Button
            variant="outline"
            size="sm"
            className="gap-1"
            onClick={() => handleExport('csv')}
          >
            <Download className="h-3 w-3" />
            CSV
          </Button>
          <Button
            variant="outline"
            size="sm"
            className="gap-1"
            onClick={() => handleExport('json')}
          >
            <Download className="h-3 w-3" />
            JSON
          </Button>
        </div>
      </div>

      {/* Stats summary */}
      {runsData?.items && runsData.items.length > 0 && (
        <RunStatsSummary runs={runsData.items} />
      )}

      {/* Content */}
      <div className="flex gap-6">
        {/* Left: Run Table */}
        <div className="flex-1 min-w-0">
          <RunTable
            experimentId={experiment.id}
            onSelectRun={(run) => {
              setComparison(null)
              setSelectedRun(run)
            }}
            onCompareResult={(result) => {
              setSelectedRun(null)
              setComparison(result)
            }}
          />
        </div>

        {/* Right: Detail / Comparison Panel */}
        <div className="hidden w-[420px] shrink-0 lg:block">
          {comparison ? (
            <RunComparisonView
              comparison={comparison}
              onClose={() => setComparison(null)}
            />
          ) : selectedRun ? (
            <RunDetailPanel
              run={selectedRun}
              onClose={() => setSelectedRun(null)}
            />
          ) : (
            <div className="flex h-64 items-center justify-center rounded-lg border border-dashed border-zinc-700">
              <p className="text-sm text-zinc-500">
                Select a run or compare multiple runs
              </p>
            </div>
          )}
        </div>
      </div>

      {experimentId && (
        <SweepDialog
          experimentId={experimentId}
          open={showSweep}
          onClose={() => setShowSweep(false)}
        />
      )}
    </div>
  )
}
