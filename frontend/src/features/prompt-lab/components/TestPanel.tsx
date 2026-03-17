import { useState } from 'react'
import { Play, Clock, Cpu, Zap, Pin, Save, FolderOpen, Trash2, X } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Badge } from '@/components/ui/badge'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Slider } from '@/components/ui/slider'
import { Input } from '@/components/ui/input'
import { toast } from 'sonner'
import { useInstances } from '@/features/models/api'
import { useTestPrompt } from '../api'
import { usePromptLabStore } from '../store'
import type { TestPromptResult } from '../types'

interface TestPanelProps {
  templateId: string
  version: number
}

interface PinnedResult {
  id: string
  instanceName: string
  modelId: string
  result: TestPromptResult
  pinnedAt: number
}

interface SavedInputSet {
  id: string
  name: string
  values: Record<string, string>
}

export function TestPanel({ templateId, version }: TestPanelProps) {
  const {
    variableValues,
    setVariableValues,
    testInstanceId,
    setTestInstanceId,
    testTemperature,
    setTestTemperature,
    testTopP,
    setTestTopP,
    testMaxTokens,
    setTestMaxTokens,
  } = usePromptLabStore()

  const { data: instances } = useInstances()
  const testMutation = useTestPrompt()
  const [results, setResults] = useState<PinnedResult[]>([])
  const [selectedInstances, setSelectedInstances] = useState<string[]>([])
  const [isRunningMulti, setIsRunningMulti] = useState(false)
  const [savedSets, setSavedSets] = useState<SavedInputSet[]>(() => {
    try {
      const stored = localStorage.getItem('prism-prompt-lab-input-sets')
      return stored ? JSON.parse(stored) : []
    } catch { return [] }
  })
  const [showSavedSets, setShowSavedSets] = useState(false)
  const [saveSetName, setSaveSetName] = useState('')

  function persistSavedSets(sets: SavedInputSet[]) {
    setSavedSets(sets)
    localStorage.setItem('prism-prompt-lab-input-sets', JSON.stringify(sets))
  }

  function handleSaveInputSet() {
    if (!saveSetName.trim()) return
    const newSet: SavedInputSet = {
      id: `set-${Date.now()}`,
      name: saveSetName.trim(),
      values: { ...variableValues },
    }
    persistSavedSets([...savedSets, newSet])
    setSaveSetName('')
    toast.success(`Saved input set "${newSet.name}"`)
  }

  function handleLoadInputSet(set: SavedInputSet) {
    setVariableValues(set.values)
    setShowSavedSets(false)
    toast.success(`Loaded "${set.name}"`)
  }

  function handleDeleteInputSet(id: string) {
    persistSavedSets(savedSets.filter((s) => s.id !== id))
  }

  function toggleInstance(instanceId: string) {
    setSelectedInstances((prev) =>
      prev.includes(instanceId)
        ? prev.filter((id) => id !== instanceId)
        : [...prev, instanceId]
    )
  }

  async function handleTestSingle() {
    if (!testInstanceId) {
      toast.error('Select an inference instance')
      return
    }
    testMutation.mutate(
      {
        templateId,
        version,
        variables: variableValues,
        instanceId: testInstanceId,
        temperature: testTemperature,
        topP: testTopP,
        maxTokens: testMaxTokens,
      },
      {
        onSuccess: (data) => {
          const inst = instances?.find((i) => i.id === testInstanceId)
          setResults((prev) => [{
            id: `r-${Date.now()}`,
            instanceName: inst?.name ?? 'unknown',
            modelId: data.modelId,
            result: data,
            pinnedAt: Date.now(),
          }, ...prev])
        },
        onError: (error) => toast.error(`Test failed: ${error.message}`),
      }
    )
  }

  async function handleTestMulti() {
    if (selectedInstances.length === 0) {
      toast.error('Select at least one instance')
      return
    }
    setIsRunningMulti(true)
    for (const instanceId of selectedInstances) {
      try {
        const data = await testMutation.mutateAsync({
          templateId,
          version,
          variables: variableValues,
          instanceId,
          temperature: testTemperature,
          topP: testTopP,
          maxTokens: testMaxTokens,
        })
        const inst = instances?.find((i) => i.id === instanceId)
        setResults((prev) => [{
          id: `r-${Date.now()}-${instanceId}`,
          instanceName: inst?.name ?? 'unknown',
          modelId: data.modelId,
          result: data,
          pinnedAt: Date.now(),
        }, ...prev])
      } catch (err) {
        const inst = instances?.find((i) => i.id === instanceId)
        toast.error(`${inst?.name ?? instanceId}: ${err instanceof Error ? err.message : 'failed'}`)
      }
    }
    setIsRunningMulti(false)
  }

  function removeResult(id: string) {
    setResults((prev) => prev.filter((r) => r.id !== id))
  }

  return (
    <div className="flex flex-col flex-1">
      <div className="px-4 py-3 border-b border-border">
        <span className="text-xs font-medium text-zinc-400">Test</span>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-3 space-y-4">
          {/* Saved input sets */}
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowSavedSets(!showSavedSets)}
              className="h-6 gap-1 px-2 text-[10px] text-zinc-500"
            >
              <FolderOpen className="h-3 w-3" />
              Input Sets ({savedSets.length})
            </Button>
            <div className="flex-1" />
            <Input
              placeholder="Set name..."
              value={saveSetName}
              onChange={(e) => setSaveSetName(e.target.value)}
              className="h-6 text-[10px] bg-zinc-800 w-24"
              onKeyDown={(e) => e.key === 'Enter' && handleSaveInputSet()}
            />
            <Button
              variant="ghost"
              size="sm"
              onClick={handleSaveInputSet}
              disabled={!saveSetName.trim()}
              className="h-6 px-1.5"
            >
              <Save className="h-3 w-3" />
            </Button>
          </div>

          {showSavedSets && savedSets.length > 0 && (
            <div className="space-y-1 rounded border border-zinc-800 p-2">
              {savedSets.map((set) => (
                <div key={set.id} className="flex items-center gap-2 text-xs">
                  <button
                    onClick={() => handleLoadInputSet(set)}
                    className="flex-1 text-left text-zinc-300 hover:text-zinc-100 truncate"
                  >
                    {set.name}
                  </button>
                  <span className="text-zinc-600">{Object.keys(set.values).length} vars</span>
                  <button onClick={() => handleDeleteInputSet(set.id)} className="text-zinc-600 hover:text-red-400">
                    <Trash2 className="h-3 w-3" />
                  </button>
                </div>
              ))}
            </div>
          )}

          {/* Single instance selector */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-zinc-300">Quick Test (single)</label>
            <div className="flex gap-1">
              <select
                value={testInstanceId ?? ''}
                onChange={(e) => setTestInstanceId(e.target.value || null)}
                className="flex-1 bg-zinc-900 border border-border rounded px-2 py-1.5 text-xs text-zinc-300"
              >
                <option value="">Select instance...</option>
                {instances?.map((inst) => (
                  <option key={inst.id} value={inst.id}>{inst.name}</option>
                ))}
              </select>
              <Button
                size="sm"
                className="h-7 gap-1 px-2"
                onClick={handleTestSingle}
                disabled={testMutation.isPending || !testInstanceId}
              >
                <Play className="h-3 w-3" />
              </Button>
            </div>
          </div>

          {/* Multi-instance selector */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-zinc-300">Compare (multi)</label>
            <div className="space-y-1">
              {instances?.map((inst) => (
                <label key={inst.id} className="flex items-center gap-2 text-xs text-zinc-400 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={selectedInstances.includes(inst.id)}
                    onChange={() => toggleInstance(inst.id)}
                    className="rounded border-zinc-600"
                  />
                  {inst.name}
                  {inst.modelId && <span className="text-zinc-600 truncate">({inst.modelId})</span>}
                </label>
              ))}
            </div>
            <Button
              size="sm"
              className="w-full gap-1 mt-1"
              variant="secondary"
              onClick={handleTestMulti}
              disabled={isRunningMulti || selectedInstances.length === 0}
            >
              <Play className="h-3 w-3" />
              {isRunningMulti ? `Running ${selectedInstances.length}...` : `Test ${selectedInstances.length} Instances`}
            </Button>
          </div>

          {/* Parameters */}
          <div className="space-y-3">
            <div className="space-y-1">
              <div className="flex justify-between">
                <label className="text-xs text-zinc-400">Temperature</label>
                <span className="text-xs text-zinc-500 tabular-nums">{testTemperature}</span>
              </div>
              <Slider
                value={testTemperature}
                onChange={(e) => setTestTemperature(Number(e.target.value))}
                min={0} max={2} step={0.1}
              />
            </div>
            <div className="space-y-1">
              <div className="flex justify-between">
                <label className="text-xs text-zinc-400">Top P</label>
                <span className="text-xs text-zinc-500 tabular-nums">{testTopP}</span>
              </div>
              <Slider
                value={testTopP}
                onChange={(e) => setTestTopP(Number(e.target.value))}
                min={0} max={1} step={0.05}
              />
            </div>
            <div className="space-y-1">
              <div className="flex justify-between">
                <label className="text-xs text-zinc-400">Max Tokens</label>
                <span className="text-xs text-zinc-500 tabular-nums">{testMaxTokens}</span>
              </div>
              <Slider
                value={testMaxTokens}
                onChange={(e) => setTestMaxTokens(Number(e.target.value))}
                min={1} max={8192} step={64}
              />
            </div>
          </div>

          {/* Pinned results */}
          {results.length > 0 && (
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-xs font-medium text-zinc-400">
                  Results ({results.length})
                </span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setResults([])}
                  className="h-5 text-[10px] text-zinc-600"
                >
                  Clear All
                </Button>
              </div>
              {results.map((pinned) => (
                <div key={pinned.id} className="rounded border border-zinc-800 bg-zinc-900/50">
                  <div className="flex items-center justify-between px-2 py-1.5 border-b border-zinc-800/50">
                    <div className="flex items-center gap-1.5">
                      <Pin className="h-3 w-3 text-violet-400" />
                      <span className="text-[10px] font-medium text-zinc-300">{pinned.instanceName}</span>
                      <Badge variant="outline" className="text-[9px] px-1">{pinned.modelId}</Badge>
                    </div>
                    <button onClick={() => removeResult(pinned.id)} className="text-zinc-600 hover:text-zinc-300">
                      <X className="h-3 w-3" />
                    </button>
                  </div>
                  <div className="px-2 py-1.5">
                    <div className="flex gap-2 text-[10px] text-zinc-500 mb-1">
                      <span className="flex items-center gap-0.5">
                        <Clock className="h-2.5 w-2.5" />{pinned.result.latencyMs}ms
                      </span>
                      <span className="flex items-center gap-0.5">
                        <Cpu className="h-2.5 w-2.5" />{pinned.result.totalTokens}
                      </span>
                      {pinned.result.tokensPerSecond && (
                        <span className="flex items-center gap-0.5">
                          <Zap className="h-2.5 w-2.5" />{pinned.result.tokensPerSecond.toFixed(1)}t/s
                        </span>
                      )}
                    </div>
                    <pre className="text-[10px] text-zinc-400 whitespace-pre-wrap max-h-24 overflow-auto">
                      {pinned.result.output}
                    </pre>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </ScrollArea>
    </div>
  )
}
