import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Layers, Pause, Play, XCircle, RotateCcw } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { Skeleton } from '@/components/ui/skeleton'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import {
  useBatchJobs,
  usePauseBatchJob,
  useResumeBatchJob,
  useCancelBatchJob,
  useRetryFailedBatch,
} from './api'
import type { BatchJob } from './types'

const STATUS_COLORS: Record<string, string> = {
  Queued: 'bg-yellow-500/10 text-yellow-500',
  Running: 'bg-blue-500/10 text-blue-500',
  Paused: 'bg-orange-500/10 text-orange-500',
  Completed: 'bg-green-500/10 text-green-500',
  Failed: 'bg-red-500/10 text-red-500',
  Cancelled: 'bg-gray-500/10 text-gray-500',
}

export function BatchInferencePage() {
  const [statusFilter, setStatusFilter] = useState<string | undefined>()
  const navigate = useNavigate()
  const { data: jobs, isLoading } = useBatchJobs(statusFilter)
  const pause = usePauseBatchJob()
  const resume = useResumeBatchJob()
  const cancel = useCancelBatchJob()
  const retry = useRetryFailedBatch()

  return (
    <div className="flex flex-col gap-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">Batch Inference</h1>
        <p className="text-sm text-muted-foreground">
          Large-scale inference processing with progress tracking and retries
        </p>
      </div>

      <div className="flex gap-2">
        {['All', 'Queued', 'Running', 'Completed', 'Failed'].map((s) => (
          <Button
            key={s}
            variant={statusFilter === (s === 'All' ? undefined : s) ? 'default' : 'outline'}
            size="sm"
            onClick={() => setStatusFilter(s === 'All' ? undefined : s)}
          >
            {s}
          </Button>
        ))}
      </div>

      {isLoading ? (
        <div className="space-y-4">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-28" />
          ))}
        </div>
      ) : jobs && jobs.length > 0 ? (
        <div className="space-y-4">
          {jobs.map((job) => (
            <BatchJobCard
              key={job.id}
              job={job}
              onPause={() => pause.mutate(job.id)}
              onResume={() => resume.mutate(job.id)}
              onCancel={() => cancel.mutate(job.id)}
              onRetry={() => retry.mutate(job.id)}
            />
          ))}
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Layers className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-medium mb-1">No batch jobs</h3>
          <p className="text-sm text-muted-foreground">
            Create a batch job from a dataset to process records at scale
          </p>
        </div>
      )}
    </div>
  )
}

function BatchJobCard({
  job,
  onPause,
  onResume,
  onCancel,
  onRetry,
}: {
  job: BatchJob
  onPause: () => void
  onResume: () => void
  onCancel: () => void
  onRetry: () => void
}) {
  return (
    <Card>
      <CardHeader className="pb-2">
        <div className="flex items-center justify-between">
          <CardTitle className="text-base">{job.model}</CardTitle>
          <div className="flex items-center gap-2">
            <Badge className={STATUS_COLORS[job.status] ?? ''}>{job.status}</Badge>
            {job.status === 'Running' && (
              <Button variant="ghost" size="sm" onClick={onPause}><Pause className="h-4 w-4" /></Button>
            )}
            {job.status === 'Paused' && (
              <Button variant="ghost" size="sm" onClick={onResume}><Play className="h-4 w-4" /></Button>
            )}
            {['Queued', 'Running', 'Paused'].includes(job.status) && (
              <Button variant="ghost" size="sm" onClick={onCancel}><XCircle className="h-4 w-4" /></Button>
            )}
            {(job.status === 'Completed' || job.status === 'Failed') && job.failedRecords > 0 && (
              <Button variant="ghost" size="sm" onClick={onRetry}><RotateCcw className="h-4 w-4" /></Button>
            )}
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="flex items-center gap-4 text-xs text-muted-foreground mb-2">
          <span>{job.completedRecords}/{job.totalRecords} records</span>
          {job.failedRecords > 0 && <span className="text-destructive">{job.failedRecords} failed</span>}
          <span>{job.tokensUsed.toLocaleString()} tokens</span>
          <span>Concurrency: {job.concurrency}</span>
        </div>
        {(job.status === 'Running' || job.status === 'Queued') && (
          <div className="space-y-1">
            <div className="flex justify-between text-xs">
              <span>Progress</span>
              <span>{(job.progress * 100).toFixed(0)}%</span>
            </div>
            <div className="h-1.5 rounded-full bg-muted overflow-hidden">
              <div className="h-full bg-primary rounded-full transition-all" style={{ width: `${job.progress * 100}%` }} />
            </div>
          </div>
        )}
        {job.errorMessage && (
          <p className="text-xs text-destructive mt-2">{job.errorMessage}</p>
        )}
      </CardContent>
    </Card>
  )
}
