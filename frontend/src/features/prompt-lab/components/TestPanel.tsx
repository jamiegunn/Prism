import { useState } from 'react'
import { Play, Clock, Cpu, Zap } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Slider } from '@/components/ui/slider'
import { toast } from 'sonner'
import { useInstances } from '@/features/models/api'
import { useTestPrompt } from '../api'
import { usePromptLabStore } from '../store'
import type { TestPromptResult } from '../types'

interface TestPanelProps {
  templateId: string
  version: number
}

export function TestPanel({ templateId, version }: TestPanelProps) {
  const {
    variableValues,
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
  const [result, setResult] = useState<TestPromptResult | null>(null)

  function handleTest() {
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
        onSuccess: (data) => setResult(data),
        onError: (error) => toast.error(`Test failed: ${error.message}`),
      }
    )
  }

  return (
    <div className="flex flex-col flex-1">
      <div className="px-4 py-3 border-b border-border">
        <span className="text-xs font-medium text-zinc-400">Test</span>
      </div>

      <ScrollArea className="flex-1">
        <div className="p-3 space-y-4">
          {/* Instance selector */}
          <div className="space-y-1">
            <label className="text-xs font-medium text-zinc-300">Instance</label>
            <select
              value={testInstanceId ?? ''}
              onChange={(e) => setTestInstanceId(e.target.value || null)}
              className="w-full bg-zinc-900 border border-border rounded px-2 py-1.5 text-xs text-zinc-300"
            >
              <option value="">Select instance...</option>
              {instances?.map((inst) => (
                <option key={inst.id} value={inst.id}>
                  {inst.name}
                </option>
              ))}
            </select>
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
                min={0}
                max={2}
                step={0.1}
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
                min={0}
                max={1}
                step={0.05}
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
                min={1}
                max={8192}
                step={64}
              />
            </div>
          </div>

          {/* Run button */}
          <Button
            className="w-full gap-2"
            onClick={handleTest}
            disabled={testMutation.isPending}
          >
            <Play className="h-4 w-4" />
            {testMutation.isPending ? 'Running...' : 'Test Prompt'}
          </Button>

          {/* Result */}
          {result && (
            <div className="space-y-3">
              <div className="flex gap-3 text-xs text-zinc-500">
                <span className="flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  {result.latencyMs}ms
                </span>
                <span className="flex items-center gap-1">
                  <Cpu className="h-3 w-3" />
                  {result.totalTokens} tokens
                </span>
                {result.tokensPerSecond && (
                  <span className="flex items-center gap-1">
                    <Zap className="h-3 w-3" />
                    {result.tokensPerSecond.toFixed(1)} t/s
                  </span>
                )}
              </div>

              <div>
                <span className="text-xs text-zinc-400">Output</span>
                <pre className="mt-1 rounded bg-zinc-900 p-3 text-xs text-zinc-300 whitespace-pre-wrap max-h-64 overflow-auto">
                  {result.output}
                </pre>
              </div>

              <div>
                <span className="text-xs text-zinc-400">Rendered Prompt</span>
                <pre className="mt-1 rounded bg-zinc-900/50 p-2 text-[10px] text-zinc-500 whitespace-pre-wrap max-h-32 overflow-auto">
                  {result.renderedPrompt}
                </pre>
              </div>
            </div>
          )}
        </div>
      </ScrollArea>
    </div>
  )
}
