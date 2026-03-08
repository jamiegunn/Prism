import { Microscope, Loader2, Brain } from 'lucide-react'
import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { Slider } from '@/components/ui/slider'
import { Select } from '@/components/ui/select'
import { Textarea } from '@/components/ui/textarea'
import { ScrollArea } from '@/components/ui/scroll-area'
import { Separator } from '@/components/ui/separator'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useInstances } from '@/features/playground/api'
import { useTokenExplorerStore } from './store'
import { usePredictNextToken, useExploreBranch } from './api'
import { ProbabilityDistribution } from './components/ProbabilityDistribution'
import { StepThroughView } from './components/StepThroughView'
import { BranchTreeView } from './components/BranchTreeView'
import { SamplingVisualization } from './components/SamplingVisualization'
import { TokenizerView } from './components/TokenizerView'
import { TokenCompareView } from './components/TokenCompareView'
import { ParamLabel } from '@/components/ui/param-label'
import { HelpPanel } from './components/HelpPanel'

export function TokenExplorerPage() {
  const instances = useInstances()
  const store = useTokenExplorerStore()
  const predictMutation = usePredictNextToken()
  const branchMutation = useExploreBranch()

  const selectedInstance = instances.data?.find((i) => i.id === store.instanceId)

  function handlePredict() {
    if (!store.instanceId || !store.prompt.trim()) return

    store.setLoading(true)
    store.clearSteps()
    predictMutation.mutate(
      {
        instanceId: store.instanceId,
        prompt: store.prompt,
        topLogprobs: store.topLogprobs,
        temperature: store.temperature,
        enableThinking: store.enableThinking,
      },
      {
        onSuccess: (data) => {
          store.setPredictions(data)
        },
        onSettled: () => {
          store.setLoading(false)
        },
      }
    )
  }

  function handleBranchFromPrediction(token: string) {
    if (!store.instanceId || !store.prompt.trim()) return

    const currentText = store.prompt + store.stepHistory.map((s) => s.token).join('')

    store.setLoading(true)
    branchMutation.mutate(
      {
        instanceId: store.instanceId,
        prompt: currentText,
        forcedToken: token,
        topLogprobs: store.topLogprobs,
        temperature: store.temperature,
        enableThinking: store.enableThinking,
      },
      {
        onSuccess: (data) => {
          store.addBranch(token, data)
        },
        onSettled: () => {
          store.setLoading(false)
        },
      }
    )
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
      e.preventDefault()
      handlePredict()
    }
  }

  const canPredict = !!store.instanceId && store.prompt.trim().length > 0 && !store.isLoading

  return (
    <div className="flex h-[calc(100vh-4rem)] flex-col gap-0">
      {/* Header */}
      <div className="shrink-0 px-6 py-4">
        <div className="flex items-center gap-3">
          <Microscope className="h-6 w-6 text-violet-500" />
          <div>
            <h1 className="text-2xl font-bold tracking-tight text-zinc-100">
              Token Explorer
            </h1>
            <p className="text-sm text-zinc-400">
              Inspect next-token predictions, step through generation, and explore alternative branches.
            </p>
          </div>
        </div>
      </div>

      {/* Main 3-panel layout */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left panel: Controls */}
        <div className="w-80 shrink-0 border-r border-zinc-800">
          <ScrollArea className="h-full">
            <div className="space-y-5 p-4">
              {/* Instance selector */}
              <div className="space-y-2">
                <ParamLabel
                  label="Model / Instance"
                  tooltip="The inference engine and model to use. Each instance connects to a running provider (vLLM, Ollama, etc.) serving a specific model."
                />
                <Select
                  value={store.instanceId ?? ''}
                  onChange={(e) => store.setInstanceId(e.target.value || null)}
                >
                  <option value="">Select an instance...</option>
                  {instances.data?.map((instance) => (
                    <option key={instance.id} value={instance.id}>
                      {instance.name}
                      {instance.modelId ? ` (${instance.modelId})` : ''}
                    </option>
                  ))}
                </Select>
                {selectedInstance && (
                  <p className="truncate text-xs text-zinc-500">
                    {selectedInstance.providerType} &middot;{' '}
                    {selectedInstance.endpoint}
                  </p>
                )}
              </div>

              <Separator />

              {/* Prompt */}
              <div className="space-y-2">
                <ParamLabel
                  label="Prompt"
                  tooltip="The input text the model will analyze. The model predicts which token is most likely to come next after this text."
                />
                <Textarea
                  value={store.prompt}
                  onChange={(e) => store.setPrompt(e.target.value)}
                  onKeyDown={handleKeyDown}
                  placeholder="Enter a prompt to analyze next-token predictions..."
                  className="min-h-[120px] resize-y text-sm"
                />
                <p className="text-xs text-zinc-600">Ctrl+Enter to predict</p>
              </div>

              <Separator />

              {/* Temperature */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <ParamLabel
                    label="Temperature"
                    tooltip="Controls randomness in token selection. 0 = greedy (always pick the most likely token). Higher values spread probability more evenly, making less likely tokens more probable."
                  />
                  <span className="font-mono text-xs text-zinc-500">
                    {store.temperature.toFixed(2)}
                  </span>
                </div>
                <Slider
                  min={0}
                  max={2}
                  step={0.01}
                  value={store.temperature}
                  onChange={(e) => store.setTemperature(Number(e.target.value))}
                />
                <p className="text-xs text-zinc-600">
                  0 = greedy (deterministic)
                </p>
              </div>

              {/* Top-p (visualization) */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <ParamLabel
                    label="Top-p (visualization)"
                    tooltip="Nucleus sampling threshold. In the prediction chart, tokens whose cumulative probability falls within this cutoff are highlighted. This shows which tokens would be in the sampling pool during actual generation."
                  />
                  <span className="font-mono text-xs text-zinc-500">
                    {store.topP.toFixed(2)}
                  </span>
                </div>
                <Slider
                  min={0}
                  max={1}
                  step={0.01}
                  value={store.topP}
                  onChange={(e) => store.setTopP(Number(e.target.value))}
                />
              </div>

              {/* Top-k (visualization) */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <ParamLabel
                    label="Top-k (visualization)"
                    tooltip="Limits the visualization to the k most probable tokens. Only the top-k candidates are highlighted in the prediction chart. This mirrors how top-k sampling restricts the candidate pool during generation."
                  />
                  <span className="font-mono text-xs text-zinc-500">
                    {store.topK}
                  </span>
                </div>
                <Slider
                  min={1}
                  max={100}
                  step={1}
                  value={store.topK}
                  onChange={(e) => store.setTopK(Number(e.target.value))}
                />
              </div>

              {/* Top Logprobs */}
              <div className="space-y-2">
                <div className="flex items-center justify-between">
                  <ParamLabel
                    label="Top Logprobs"
                    tooltip="How many alternative tokens to request from the model at each position. Higher values show more of the probability distribution but may be slower. vLLM supports up to 20."
                  />
                  <span className="font-mono text-xs text-zinc-500">
                    {store.topLogprobs}
                  </span>
                </div>
                <Slider
                  min={1}
                  max={20}
                  step={1}
                  value={store.topLogprobs}
                  onChange={(e) => store.setTopLogprobs(Number(e.target.value))}
                />
              </div>

              {/* Enable Thinking */}
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-1.5">
                  <Brain className="h-3.5 w-3.5 text-zinc-400" />
                  <ParamLabel
                    label="Enable Thinking"
                    tooltip="When on, the model uses chain-of-thought reasoning (e.g., Qwen3's <think> tags) before answering. Turn off for token analysis so predictions show actual output tokens instead of reasoning markers."
                  />
                </div>
                <button
                  type="button"
                  role="switch"
                  aria-checked={store.enableThinking}
                  onClick={() => store.setEnableThinking(!store.enableThinking)}
                  className={cn(
                    'relative inline-flex h-5 w-9 shrink-0 cursor-pointer rounded-full border-2 border-transparent transition-colors',
                    store.enableThinking ? 'bg-violet-600' : 'bg-zinc-700'
                  )}
                >
                  <span
                    className={cn(
                      'pointer-events-none inline-block h-4 w-4 rounded-full bg-white shadow-lg transition-transform',
                      store.enableThinking ? 'translate-x-4' : 'translate-x-0'
                    )}
                  />
                </button>
              </div>
              <p className="text-xs text-zinc-600">
                Off = skip chain-of-thought (recommended for token analysis)
              </p>

              <Separator />

              {/* Predict button */}
              <Button
                onClick={handlePredict}
                disabled={!canPredict}
                className="w-full gap-2 bg-violet-600 hover:bg-violet-700"
              >
                {store.isLoading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Microscope className="h-4 w-4" />
                )}
                Predict Next Token
              </Button>

              {/* Reset */}
              <Button
                variant="outline"
                size="sm"
                onClick={store.reset}
                className="w-full text-xs"
              >
                Reset All
              </Button>
            </div>
          </ScrollArea>
        </div>

        {/* Center panel: Tabs */}
        <div className="flex-1 overflow-hidden">
          <Tabs defaultValue="predictions" className="flex h-full flex-col">
            <div className="shrink-0 border-b border-zinc-800 px-4 py-2">
              <TabsList>
                <TabsTrigger value="predictions">Predictions</TabsTrigger>
                <TabsTrigger value="step-through">Step Through</TabsTrigger>
                <TabsTrigger value="branches">
                  Branches
                  {store.branches.length > 0 && (
                    <span className="ml-1.5 inline-flex h-5 w-5 items-center justify-center rounded-full bg-violet-600 text-xs">
                      {store.branches.length}
                    </span>
                  )}
                </TabsTrigger>
                <TabsTrigger value="tokenizer">Tokenizer</TabsTrigger>
                <TabsTrigger value="compare">Compare</TabsTrigger>
              </TabsList>
            </div>

            <TabsContent value="predictions" className="flex-1 overflow-hidden mt-0">
              <ScrollArea className="h-full p-4">
                <HelpPanel title="How Predictions Work">
                  <p className="mb-2">
                    <strong className="text-zinc-300">What:</strong> Given your prompt, the model predicts which token is most likely to come next. This tab shows the top candidates ranked by probability.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">Why:</strong> Understanding token probabilities reveals how confident the model is, where it hesitates between alternatives, and how sampling parameters (temperature, top-p, top-k) shape the final output.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">How to read the results:</strong> Each bar represents a candidate token. The bar length and percentage show probability. Cumulative probability tracks the running total from top to bottom. Tokens within the top-p / top-k thresholds are highlighted &mdash; only these would be sampled during generation.
                  </p>
                  <p>
                    <strong className="text-zinc-300">Tip:</strong> Click any token to explore a branch &mdash; the model will generate a continuation starting from that forced token.
                  </p>
                </HelpPanel>
                {store.currentPredictions ? (
                  <ProbabilityDistribution
                    predictions={store.currentPredictions.predictions}
                    totalProbability={store.currentPredictions.totalProbability}
                    onTokenClick={handleBranchFromPrediction}
                  />
                ) : (
                  <div className="flex h-64 flex-col items-center justify-center gap-2 text-zinc-500">
                    <Microscope className="h-8 w-8 text-zinc-600" />
                    <p className="text-sm">No predictions yet.</p>
                    <p className="text-xs text-zinc-600">
                      Enter a prompt and click Predict to see next-token probabilities.
                    </p>
                  </div>
                )}
              </ScrollArea>
            </TabsContent>

            <TabsContent value="step-through" className="flex-1 overflow-hidden mt-0">
              <div className="flex h-full flex-col p-4">
                <HelpPanel title="How Step Through Works">
                  <p className="mb-2">
                    <strong className="text-zinc-300">What:</strong> Step Through lets you walk through text generation one token at a time. At each step, the model predicts the next token, you choose one (or accept the greedy top pick), and it advances.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">Why:</strong> This exposes the autoregressive process that underpins all LLM text generation. You can see exactly how each token choice influences the predictions that follow, and force alternative tokens to observe how the generation path changes.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">How to read the results:</strong> The Generated Sequence shows accumulated tokens, color-coded by log-probability (green = high confidence, red = low). Dotted underlines mark tokens you forced. Hover any token for exact probability and log-prob values.
                  </p>
                  <p>
                    <strong className="text-zinc-300">Tip:</strong> Click any token in the prediction list below the sequence to force it as the next token instead of the greedy choice. Use Undo to backtrack.
                  </p>
                </HelpPanel>
                <div className="min-h-0 flex-1">
                  <StepThroughView />
                </div>
              </div>
            </TabsContent>

            <TabsContent value="branches" className="flex-1 overflow-hidden mt-0">
              <div className="flex h-full flex-col p-4">
                <HelpPanel title="How Branch Exploration Works">
                  <p className="mb-2">
                    <strong className="text-zinc-300">What:</strong> Branch exploration forces a specific starting token and lets the model generate a full continuation from there. This creates an alternative &ldquo;branch&rdquo; of the generation tree.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">Why:</strong> When the model assigns non-trivial probability to multiple next tokens, each choice can lead to very different outputs. Branching lets you compare these divergent paths side by side, revealing how sensitive the output is to a single token choice.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">How to read the results:</strong> Each card shows a branch starting from a forced token. The generated continuation is displayed with per-token log-probability coloring. Perplexity measures how &ldquo;surprised&rdquo; the model is by the branch &mdash; lower perplexity means the model found the text more natural. Top alternatives at each position show what the model would have preferred.
                  </p>
                  <p>
                    <strong className="text-zinc-300">Tip:</strong> Click a token in the Predictions tab to create a branch from it, or use the Step Through tab to force a token and then branch from the current position.
                  </p>
                </HelpPanel>
                <div className="min-h-0 flex-1">
                  <BranchTreeView />
                </div>
              </div>
            </TabsContent>

            <TabsContent value="tokenizer" className="flex-1 overflow-hidden mt-0">
              <div className="flex h-full flex-col p-4">
                <HelpPanel title="How the Tokenizer Works">
                  <p className="mb-2">
                    <strong className="text-zinc-300">What:</strong> The tokenizer breaks text into the subword tokens that the model actually processes. Each colored block is one token. This shows you the model&apos;s true &ldquo;vocabulary units.&rdquo;
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">Why:</strong> Token boundaries directly affect model behavior. Common words may be a single token while rare words get split into multiple pieces. Understanding tokenization helps explain prompt length limits, cost calculations, and why the model sometimes struggles with spelling, counting, or code formatting.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">How to read the results:</strong> Each colored block is a single token. Hover to see the token ID, byte representation, and byte length. The summary shows total token count, character count, and byte count. Whitespace and special characters are made visible with display markers.
                  </p>
                  <p>
                    <strong className="text-zinc-300">Tip:</strong> Try pasting code, URLs, or non-English text to see how different content gets tokenized. Numbers are often split in surprising ways.
                  </p>
                </HelpPanel>
                <div className="min-h-0 flex-1 overflow-hidden">
                  <TokenizerView embedded />
                </div>
              </div>
            </TabsContent>

            <TabsContent value="compare" className="flex-1 overflow-hidden mt-0">
              <div className="flex h-full flex-col p-4">
                <HelpPanel title="How Token Comparison Works">
                  <p className="mb-2">
                    <strong className="text-zinc-300">What:</strong> Compare tokenization runs the same text through multiple model instances, showing how each model&apos;s tokenizer breaks it down differently.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">Why:</strong> Different model families (Llama, Qwen, Mistral, GPT) use different tokenizers with different vocabularies. The same text can produce vastly different token counts, which affects context window usage, inference cost, and even model behavior at token boundaries.
                  </p>
                  <p className="mb-2">
                    <strong className="text-zinc-300">How to read the results:</strong> Each row shows one model&apos;s tokenization. Compare token counts and look at where token boundaries fall. Fewer tokens for the same text generally means a more efficient tokenizer for that content type.
                  </p>
                  <p>
                    <strong className="text-zinc-300">Tip:</strong> Try comparing with multilingual text, code, or structured data &mdash; tokenizer differences are most dramatic with non-English content.
                  </p>
                </HelpPanel>
                <div className="min-h-0 flex-1 overflow-hidden">
                  <TokenCompareView embedded />
                </div>
              </div>
            </TabsContent>
          </Tabs>
        </div>

        {/* Right panel: Sampling Analysis */}
        <div className="w-64 shrink-0 border-l border-zinc-800">
          <ScrollArea className="h-full p-4">
            <SamplingVisualization />
          </ScrollArea>
        </div>
      </div>
    </div>
  )
}
