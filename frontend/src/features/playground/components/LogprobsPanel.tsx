import { useState } from 'react'
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs'
import { TokenHeatmap } from '@/components/logprobs/TokenHeatmap'
import { AlternativeTokensPanel } from '@/components/logprobs/AlternativeTokensPanel'
import { PerplexityBadge } from '@/components/logprobs/PerplexityBadge'
import { EntropyChart } from '@/components/logprobs/EntropyChart'
import { SurpriseHighlighter } from '@/components/logprobs/SurpriseHighlighter'
import type { LogprobsData } from '@/services/types/logprobs'

interface LogprobsPanelProps {
  logprobsData: LogprobsData
  perplexity: number | null
}

export function LogprobsPanel({ logprobsData, perplexity }: LogprobsPanelProps) {
  const [selectedTokenIndex, setSelectedTokenIndex] = useState<number | null>(null)
  const tokens = logprobsData.tokens

  const selectedToken =
    selectedTokenIndex !== null && selectedTokenIndex < tokens.length
      ? tokens[selectedTokenIndex]
      : null

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <span className="text-xs font-medium text-zinc-400">Logprobs Analysis</span>
        {perplexity != null && <PerplexityBadge perplexity={perplexity} />}
      </div>

      <Tabs defaultValue="heatmap">
        <TabsList>
          <TabsTrigger value="heatmap">Heatmap</TabsTrigger>
          <TabsTrigger value="entropy">Entropy</TabsTrigger>
          <TabsTrigger value="surprise">Surprise</TabsTrigger>
        </TabsList>

        <TabsContent value="heatmap">
          <TokenHeatmap
            tokens={tokens}
            onTokenClick={setSelectedTokenIndex}
            selectedIndex={selectedTokenIndex ?? undefined}
          />
        </TabsContent>

        <TabsContent value="entropy">
          <EntropyChart tokens={tokens} />
        </TabsContent>

        <TabsContent value="surprise">
          <SurpriseHighlighter
            tokens={tokens}
            onTokenClick={setSelectedTokenIndex}
            selectedIndex={selectedTokenIndex ?? undefined}
          />
        </TabsContent>
      </Tabs>

      {selectedToken && (
        <div className="border-t border-zinc-700/50 pt-3">
          <p className="text-xs font-medium text-zinc-400 mb-2">
            Alternatives for: <span className="font-mono text-violet-400">{JSON.stringify(selectedToken.token)}</span>
          </p>
          <AlternativeTokensPanel token={selectedToken} />
        </div>
      )}
    </div>
  )
}
