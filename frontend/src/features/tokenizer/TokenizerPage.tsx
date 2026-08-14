import { Hash } from 'lucide-react'
import { Select } from '@/components/ui/select'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { useInstances } from '@/features/models/api'
import { useDefaultInstance } from '@/features/models/useDefaultInstance'
import { HelpPanel } from '@/features/token-explorer/components/HelpPanel'
import { TokenizerView } from './components/TokenizerView'
import { TokenCompareView } from './components/TokenCompareView'
import { useTokenizerStore } from './store'

/**
 * Tokenization: what units a model actually reads, and whether two models agree on them.
 *
 * These were two tabs on the Token Explorer, sharing that page's left rail — a prompt, a
 * temperature, top-p, top-k, top-logprobs and a Predict button, none of which tokenization uses.
 * Compare even carried its own instance selector, so the page showed two server pickers at once
 * and the one on the left did nothing to what you were looking at. They are a different question
 * from "what would the model say next", and they read better as their own page.
 */
export function TokenizerPage() {
  const { data: instances } = useInstances()
  const { instanceId, setInstanceId } = useTokenizerStore()

  useDefaultInstance(instanceId, setInstanceId)

  const selected = instances?.find((i) => i.id === instanceId)

  return (
    <div className="flex h-full flex-col">
      <div className="flex items-start gap-3 px-1 pb-4">
        <Hash className="mt-1 h-6 w-6 text-violet-400" />
        <div className="flex-1">
          <h1 className="text-2xl font-semibold text-zinc-50">Tokenizer</h1>
          <p className="text-sm text-zinc-400">
            See the units a model reads, count them exactly, and check whether two models agree
            on where a word ends.
          </p>
        </div>

        <div className="w-72" data-tour="tokenizer-server">
          <label className="mb-1 block text-xs text-zinc-500">Server</label>
          <Select
            value={instanceId ?? ''}
            onChange={(e) => setInstanceId(e.target.value)}
            className="text-sm"
          >
            <option value="">Select a server...</option>
            {instances?.map((instance) => (
              <option key={instance.id} value={instance.id}>
                {instance.name}
                {instance.modelId ? ` (${instance.modelId})` : ''}
                {instance.supportsTokenize ? '' : ' — no tokenizer'}
              </option>
            ))}
          </Select>
          {selected && (
            <p className="mt-1 text-xs text-zinc-600">
              {selected.providerType} &middot; {selected.endpoint}
            </p>
          )}
        </div>
      </div>

      <Tabs defaultValue="tokenize" className="flex min-h-0 flex-1 flex-col">
        <TabsList className="mb-3 self-start" data-tour="tokenizer-tabs">
          <TabsTrigger value="tokenize">Tokenize</TabsTrigger>
          <TabsTrigger value="compare">Compare</TabsTrigger>
        </TabsList>

        <TabsContent value="tokenize" className="mt-0 min-h-0 flex-1 overflow-hidden">
          <div className="flex h-full flex-col">
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
              <TokenizerView instanceId={instanceId} embedded />
            </div>
          </div>
        </TabsContent>

        <TabsContent value="compare" className="mt-0 min-h-0 flex-1 overflow-hidden">
          <div className="flex h-full flex-col">
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
  )
}
