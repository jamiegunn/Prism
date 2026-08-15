import { useState } from 'react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import {
  Activity,
  Check,
  Clock,
  Cpu,
  HardDrive,
  RefreshCw,
  Trash2,
  X,
  Repeat,
  Star,
} from 'lucide-react'
import { toast } from 'sonner'
import { StatusIndicator } from './StatusIndicator'
import { KvCacheGauge } from './KvCacheGauge'
import { useInstanceMetrics, useTriggerHealthCheck, useUnregisterInstance, useSwapModel, useProbeCapabilities, useInstanceModels, useSetDefaultInstance } from '../api'
import type { InferenceInstance } from '../types'

interface InstanceDetailPanelProps {
  instance: InferenceInstance
  onRemoved: () => void
}

interface CapabilityRowProps {
  label: string
  supported: boolean
}

function CapabilityRow({ label, supported }: CapabilityRowProps) {
  return (
    <div className="flex items-center justify-between text-sm">
      <span className="text-zinc-400">{label}</span>
      {supported ? (
        <Check className="h-4 w-4 text-emerald-500" />
      ) : (
        <X className="h-4 w-4 text-zinc-600" />
      )}
    </div>
  )
}

function GpuBar({ label, value, max }: { label: string; value: number; max: number }) {
  const percent = max > 0 ? (value / max) * 100 : 0

  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between text-sm">
        <span className="text-zinc-400">{label}</span>
        <span className="text-zinc-300">
          {value.toFixed(0)} / {max.toFixed(0)} MB
        </span>
      </div>
      <div className="h-2 w-full overflow-hidden rounded-full bg-zinc-800">
        <div
          className={cn(
            'h-full rounded-full transition-all',
            percent > 90 ? 'bg-red-500' : percent > 70 ? 'bg-amber-500' : 'bg-emerald-500'
          )}
          style={{ width: `${Math.min(100, percent)}%` }}
        />
      </div>
    </div>
  )
}

export function InstanceDetailPanel({ instance, onRemoved }: InstanceDetailPanelProps) {
  const { data: metrics } = useInstanceMetrics(instance.id)
  const healthCheckMutation = useTriggerHealthCheck(instance.id)
  const probeMutation = useProbeCapabilities(instance.id)
  const unregisterMutation = useUnregisterInstance()
  const swapModelMutation = useSwapModel(instance.id)
  const setDefaultMutation = useSetDefaultInstance(instance.id)

  const [confirmDelete, setConfirmDelete] = useState(false)
  const [swapModelId, setSwapModelId] = useState('')
  const [showSwapInput, setShowSwapInput] = useState(false)

  // What this server actually has, fetched only once the picker is open. The box used to be free
  // text with a vLLM-shaped placeholder, so choosing a model on an Ollama meant recalling an
  // exact tag from memory — and a typo was accepted with a success toast, because a failed pull
  // streams its error inside a 200.
  const { data: instanceModels } = useInstanceModels(showSwapInput ? instance.id : null)

  function handleHealthCheck() {
    healthCheckMutation.mutate(undefined, {
      onSuccess: () => toast.success('Health check completed'),
      onError: (error) => toast.error(`Health check failed: ${error.message}`),
    })
  }

  function handleProbe() {
    probeMutation.mutate(undefined, {
      onSuccess: (data) => toast.success(`Probe complete — ${data.tier} tier`),
      onError: (error) => toast.error(`Probe failed: ${error.message}`),
    })
  }

  function handleRemove() {
    unregisterMutation.mutate(instance.id, {
      onSuccess: () => {
        toast.success('Instance removed')
        onRemoved()
      },
      onError: (error) => toast.error(`Failed to remove: ${error.message}`),
    })
  }

  function handleSwapModel() {
    if (!swapModelId.trim()) return
    swapModelMutation.mutate(swapModelId.trim(), {
      onSuccess: (updated) => {
        // Named, and in the past tense. "Initiated" described something still in progress for a
        // call that had already finished — and said the same thing whether it had worked or not.
        toast.success(`${instance.name} is now running ${updated.modelId ?? swapModelId.trim()}`)
        setSwapModelId('')
        setShowSwapInput(false)
      },
      onError: (error) => toast.error(`Model swap failed: ${error.message}`),
    })
  }

  return (
    <Card className="h-fit">
      <CardHeader>
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg">{instance.name}</CardTitle>
          <StatusIndicator status={instance.status} />
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Instance Info */}
        <div className="space-y-2">
          <h4 className="text-sm font-semibold text-zinc-300">Details</h4>
          <div className="space-y-1.5 text-sm">
            <InfoRow label="Endpoint" value={instance.endpoint} mono />
            <InfoRow label="Provider" value={instance.providerType} />
            <InfoRow label="Model" value={instance.modelId ?? 'N/A'} mono />
            <InfoRow label="GPU Config" value={instance.gpuConfig ?? 'N/A'} />
            <InfoRow
              label="Max Context"
              value={instance.maxContextLength?.toLocaleString() ?? 'N/A'}
            />
          </div>
          {instance.isDefault && (
            <Badge variant="default" className="mt-1">Default Instance</Badge>
          )}
          {instance.tags.length > 0 && (
            <div className="flex flex-wrap gap-1 pt-1">
              {instance.tags.map((tag) => (
                <Badge key={tag} variant="outline" className="text-xs">
                  {tag}
                </Badge>
              ))}
            </div>
          )}
        </div>

        <Separator className="bg-zinc-800" />

        {/* Metrics */}
        <div className="space-y-3">
          <h4 className="flex items-center gap-2 text-sm font-semibold text-zinc-300">
            <Activity className="h-4 w-4" />
            Live Metrics
          </h4>
          {!instance.supportsMetrics ? (
            <p className="text-sm text-zinc-500">
              Metrics not available for this provider
            </p>
          ) : metrics ? (
            <div className="space-y-3">
              {metrics.gpuUtilization != null && (
                <div className="space-y-1">
                  <div className="flex items-center justify-between text-sm">
                    <span className="flex items-center gap-1.5 text-zinc-400">
                      <Cpu className="h-3.5 w-3.5" />
                      GPU Utilization
                    </span>
                    <span className="text-zinc-300">{metrics.gpuUtilization.toFixed(0)}%</span>
                  </div>
                  <div className="h-2 w-full overflow-hidden rounded-full bg-zinc-800">
                    <div
                      className={cn(
                        'h-full rounded-full transition-all',
                        metrics.gpuUtilization > 90
                          ? 'bg-red-500'
                          : metrics.gpuUtilization > 70
                            ? 'bg-amber-500'
                            : 'bg-emerald-500'
                      )}
                      style={{ width: `${Math.min(100, metrics.gpuUtilization)}%` }}
                    />
                  </div>
                </div>
              )}

              {metrics.gpuMemoryUsed != null && metrics.gpuMemoryTotal != null && (
                <div className="flex items-center gap-1.5">
                  <HardDrive className="h-3.5 w-3.5 text-zinc-400" />
                  <div className="flex-1">
                    <GpuBar
                      label="GPU Memory"
                      value={metrics.gpuMemoryUsed}
                      max={metrics.gpuMemoryTotal}
                    />
                  </div>
                </div>
              )}

              <div className="flex items-center justify-between text-sm">
                <span className="text-zinc-400">KV Cache</span>
                <KvCacheGauge utilization={metrics.kvCacheUtilization} />
              </div>

              {metrics.activeRequests != null && (
                <div className="flex items-center justify-between text-sm">
                  <span className="text-zinc-400">Active Requests</span>
                  <span className="text-zinc-300">{metrics.activeRequests}</span>
                </div>
              )}

              {metrics.requestsPerSecond != null && (
                <div className="flex items-center justify-between text-sm">
                  <span className="text-zinc-400">Requests/sec</span>
                  <span className="text-zinc-300">{metrics.requestsPerSecond.toFixed(1)}</span>
                </div>
              )}

              {metrics.queueDepth != null && (
                <div className="flex items-center justify-between text-sm">
                  <span className="text-zinc-400">Queue Depth</span>
                  <span className="text-zinc-300">{metrics.queueDepth}</span>
                </div>
              )}
            </div>
          ) : (
            <p className="text-sm text-zinc-500">Loading metrics...</p>
          )}
        </div>

        <Separator className="bg-zinc-800" />

        {/* Capabilities */}
        <div className="space-y-2">
          <h4 className="text-sm font-semibold text-zinc-300">Capabilities</h4>
          <div className="space-y-1.5">
            <CapabilityRow label="Logprobs" supported={instance.supportsLogprobs} />
            <CapabilityRow label="Streaming" supported={instance.supportsStreaming} />
            <CapabilityRow label="Metrics" supported={instance.supportsMetrics} />
            <CapabilityRow label="Tokenize" supported={instance.supportsTokenize} />
            <CapabilityRow label="Guided Decoding" supported={instance.supportsGuidedDecoding} />
            <CapabilityRow label="Multimodal" supported={instance.supportsMultimodal} />
            <CapabilityRow label="Model Swap" supported={instance.supportsModelSwap} />
          </div>
        </div>

        <Separator className="bg-zinc-800" />

        {/* Health Check Info */}
        {instance.lastHealthCheck && (
          <div className="space-y-1 text-sm">
            <div className="flex items-center gap-1.5 text-zinc-400">
              <Clock className="h-3.5 w-3.5" />
              Last Health Check
            </div>
            <p className="text-zinc-300">
              {new Date(instance.lastHealthCheck).toLocaleString()}
            </p>
            {instance.lastHealthError && (
              <p className="text-xs text-red-400">{instance.lastHealthError}</p>
            )}
          </div>
        )}

        {/* Actions */}
        <div className="space-y-2">
          <h4 className="text-sm font-semibold text-zinc-300">Actions</h4>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleHealthCheck}
              disabled={healthCheckMutation.isPending}
            >
              <RefreshCw
                className={cn('mr-1.5 h-3.5 w-3.5', healthCheckMutation.isPending && 'animate-spin')}
              />
              Health Check
            </Button>

            <Button
              variant="outline"
              size="sm"
              onClick={handleProbe}
              disabled={probeMutation.isPending}
            >
              <Activity
                className={cn('mr-1.5 h-3.5 w-3.5', probeMutation.isPending && 'animate-spin')}
              />
              Probe Capabilities
            </Button>

            {!instance.isDefault && (
              <Button
                variant="outline"
                size="sm"
                onClick={() =>
                  setDefaultMutation.mutate(undefined, {
                    onSuccess: () => toast.success(`${instance.name} is now the default`),
                    onError: (error) => toast.error(`Could not set default: ${error.message}`),
                  })
                }
                disabled={setDefaultMutation.isPending}
              >
                <Star className="mr-1.5 h-3.5 w-3.5" />
                {setDefaultMutation.isPending ? 'Setting...' : 'Make Default'}
              </Button>
            )}

            {instance.supportsModelSwap && (
              <Button
                variant="outline"
                size="sm"
                onClick={() => setShowSwapInput(!showSwapInput)}
              >
                <Repeat className="mr-1.5 h-3.5 w-3.5" />
                Swap Model
              </Button>
            )}

            {!confirmDelete ? (
              <Button
                variant="destructive"
                size="sm"
                onClick={() => setConfirmDelete(true)}
              >
                <Trash2 className="mr-1.5 h-3.5 w-3.5" />
                Remove
              </Button>
            ) : (
              <div className="flex items-center gap-2">
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleRemove}
                  disabled={unregisterMutation.isPending}
                >
                  {unregisterMutation.isPending ? 'Removing...' : 'Confirm Remove'}
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setConfirmDelete(false)}
                >
                  Cancel
                </Button>
              </div>
            )}
          </div>

          {showSwapInput && (
            <div className="space-y-2 pt-2">
              <div className="flex items-center gap-2">
                {instanceModels?.canList && instanceModels.models.length > 0 ? (
                  <select
                    value={swapModelId}
                    onChange={(e) => setSwapModelId(e.target.value)}
                    className="flex-1 rounded border border-border bg-zinc-900 px-2 py-1.5 text-sm text-zinc-200"
                  >
                    <option value="">Choose a model...</option>
                    {instanceModels.models.map((model) => {
                      const embeddingOnly = instanceModels.embeddingOnly.includes(model)
                      return (
                        <option key={model} value={model} disabled={embeddingOnly}>
                          {model}
                          {embeddingOnly ? ' — embeddings only, cannot chat' : ''}
                          {model === instance.modelId ? ' (current)' : ''}
                        </option>
                      )
                    })}
                  </select>
                ) : (
                  // A server that cannot enumerate its models — vLLM serves the one it was
                  // started with — leaves nothing to choose from, so typing is the only option.
                  <Input
                    placeholder="Model ID"
                    value={swapModelId}
                    onChange={(e) => setSwapModelId(e.target.value)}
                    className="flex-1"
                  />
                )}
                <Button
                  size="sm"
                  onClick={handleSwapModel}
                  disabled={swapModelMutation.isPending || !swapModelId.trim()}
                >
                  {swapModelMutation.isPending ? 'Swapping...' : 'Swap'}
                </Button>
              </div>

              {instanceModels?.reason && (
                <p className="text-xs text-zinc-500">{instanceModels.reason}</p>
              )}
            </div>
          )}
        </div>
      </CardContent>
    </Card>
  )
}

function InfoRow({ label, value, mono }: { label: string; value: string; mono?: boolean }) {
  return (
    <div className="flex items-center justify-between">
      <span className="text-zinc-500">{label}</span>
      <span className={cn('text-zinc-300', mono && 'font-mono text-xs')}>{value}</span>
    </div>
  )
}
