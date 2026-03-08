import { cn } from '@/lib/utils'
import { getTokenBgColor, getTokenColor } from '@/lib/logprobs'
import { Button } from '@/components/ui/button'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Tooltip, TooltipTrigger, TooltipContent } from '@/components/ui/tooltip'
import { Separator } from '@/components/ui/separator'
import { Play, Undo2, Trash2 } from 'lucide-react'
import { useTokenExplorerStore } from '../store'
import { useStepThrough } from '../api'
import { ProbabilityDistribution } from './ProbabilityDistribution'
import type { StepEntry } from '../types'

export function StepThroughView() {
  const store = useTokenExplorerStore()
  const stepMutation = useStepThrough()

  const currentText = store.prompt + store.stepHistory.map((s) => s.token).join('')

  function handleStep() {
    if (!store.instanceId || !store.currentPredictions) return
    const topPrediction = store.currentPredictions.predictions[0]
    if (!topPrediction) return

    performStep(topPrediction.token, false)
  }

  function handleForceToken(token: string) {
    if (!store.instanceId) return
    performStep(token, true)
  }

  function performStep(token: string, wasForced: boolean) {
    if (!store.instanceId || !store.currentPredictions) return

    const predictions = store.currentPredictions.predictions
    const entry = predictions.find((p) => p.token === token)
    const previousTokens = store.stepHistory.map((s) => s.token).join('')

    store.setLoading(true)
    stepMutation.mutate(
      {
        instanceId: store.instanceId,
        prompt: store.prompt,
        selectedToken: token,
        previousTokens,
        topLogprobs: store.topLogprobs,
        temperature: store.temperature,
        enableThinking: store.enableThinking,
      },
      {
        onSuccess: (result) => {
          const stepEntry: StepEntry = {
            token,
            probability: entry?.probability ?? 0,
            logprob: entry?.logprob ?? -Infinity,
            wasForced,
            predictions,
          }
          store.addStep(stepEntry)
          store.setPredictions(result.nextPredictions)
        },
        onSettled: () => {
          store.setLoading(false)
        },
      }
    )
  }

  function handleUndo() {
    if (store.stepHistory.length === 0) return

    const previousSteps = store.stepHistory.slice(0, -1)
    const lastStep = previousSteps.length > 0 ? previousSteps[previousSteps.length - 1] : null

    store.undoStep()

    // Restore predictions from the previous step
    if (lastStep) {
      store.setPredictions({
        predictions: lastStep.predictions,
        inputTokenCount: 0,
        modelId: store.currentPredictions?.modelId ?? '',
        totalProbability: lastStep.predictions.reduce((sum, p) => sum + p.probability, 0),
      })
    }
  }

  const hasSteps = store.stepHistory.length > 0
  const hasPredictions = store.currentPredictions !== null

  return (
    <div className="flex h-full flex-col gap-4">
      {/* Generated text with colored tokens */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <h4 className="text-sm font-medium text-zinc-300">Generated Sequence</h4>
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleUndo}
              disabled={!hasSteps || store.isLoading}
              className="gap-1.5"
            >
              <Undo2 className="h-3.5 w-3.5" />
              Undo
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => store.clearSteps()}
              disabled={!hasSteps || store.isLoading}
              className="gap-1.5"
            >
              <Trash2 className="h-3.5 w-3.5" />
              Clear
            </Button>
            <Button
              size="sm"
              onClick={handleStep}
              disabled={!hasPredictions || store.isLoading}
              className="gap-1.5 bg-violet-600 hover:bg-violet-700"
            >
              <Play className="h-3.5 w-3.5" />
              Step (Greedy)
            </Button>
          </div>
        </div>

        <div className="min-h-[80px] rounded-md border border-zinc-700 bg-zinc-900 p-3">
          {/* Original prompt text */}
          <span className="text-sm text-zinc-400">{store.prompt}</span>

          {/* Step tokens with probability colors */}
          {store.stepHistory.map((step, index) => (
            <Tooltip key={index}>
              <TooltipTrigger>
                <span
                  className={cn(
                    'inline rounded-sm px-0.5 font-mono text-sm',
                    getTokenBgColor(step.logprob),
                    getTokenColor(step.logprob),
                    step.wasForced && 'underline decoration-violet-500 decoration-dotted underline-offset-4'
                  )}
                >
                  {step.token}
                </span>
              </TooltipTrigger>
              <TooltipContent side="bottom">
                <div className="space-y-0.5 text-xs">
                  <div>
                    Token: <span className="font-mono">{JSON.stringify(step.token)}</span>
                  </div>
                  <div>Probability: {(step.probability * 100).toFixed(4)}%</div>
                  <div>Log-prob: {step.logprob.toFixed(6)}</div>
                  <div>{step.wasForced ? 'Forced by user' : 'Model-chosen (greedy)'}</div>
                  <div>Step #{index + 1}</div>
                </div>
              </TooltipContent>
            </Tooltip>
          ))}

          {!hasSteps && !store.prompt && (
            <span className="text-sm text-zinc-600 italic">
              Enter a prompt and click Predict to begin stepping...
            </span>
          )}
        </div>

        {hasSteps && (
          <div className="flex gap-3 text-xs text-zinc-500">
            <span>{store.stepHistory.length} tokens generated</span>
            <span>
              {store.stepHistory.filter((s) => s.wasForced).length} forced
            </span>
          </div>
        )}
      </div>

      <Separator />

      {/* Current predictions for force-selecting */}
      {hasPredictions && (
        <div className="flex-1 space-y-2 overflow-hidden">
          <h4 className="text-sm font-medium text-zinc-300">
            Next Token Predictions
            <span className="ml-2 text-xs text-zinc-500">
              (click to force a token)
            </span>
          </h4>
          <ScrollArea className="h-[calc(100%-2rem)]">
            <ProbabilityDistribution
              predictions={store.currentPredictions!.predictions}
              totalProbability={store.currentPredictions!.totalProbability}
              onTokenClick={handleForceToken}
            />
          </ScrollArea>
        </div>
      )}
    </div>
  )
}
