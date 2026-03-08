import { Badge } from '@/components/ui/badge'
import type { InferenceInstance } from '../types'

interface CapabilityBadgesProps {
  instance: InferenceInstance
  className?: string
}

interface Capability {
  key: keyof InferenceInstance
  label: string
}

const capabilities: Capability[] = [
  { key: 'supportsLogprobs', label: 'Logprobs' },
  { key: 'supportsStreaming', label: 'Streaming' },
  { key: 'supportsMetrics', label: 'Metrics' },
  { key: 'supportsTokenize', label: 'Tokenize' },
  { key: 'supportsGuidedDecoding', label: 'Guided Decoding' },
  { key: 'supportsMultimodal', label: 'Multimodal' },
  { key: 'supportsModelSwap', label: 'Model Swap' },
]

export function CapabilityBadges({ instance, className }: CapabilityBadgesProps) {
  const supported = capabilities.filter((cap) => instance[cap.key] === true)

  if (supported.length === 0) return null

  return (
    <div className={className}>
      <div className="flex flex-wrap gap-1">
        {supported.map((cap) => (
          <Badge key={cap.key} variant="secondary" className="text-[10px]">
            {cap.label}
          </Badge>
        ))}
      </div>
    </div>
  )
}
