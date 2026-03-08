import { cn } from '@/lib/utils'
import { Badge } from '@/components/ui/badge'

interface PerplexityBadgeProps {
  perplexity: number
  className?: string
}

export function PerplexityBadge({ perplexity, className }: PerplexityBadgeProps) {
  const colorClasses =
    perplexity < 3
      ? 'bg-emerald-500/20 text-emerald-400 border-emerald-500/30'
      : perplexity <= 6
        ? 'bg-amber-500/20 text-amber-400 border-amber-500/30'
        : 'bg-red-500/20 text-red-400 border-red-500/30'

  return (
    <Badge className={cn(colorClasses, className)}>
      PPL {perplexity.toFixed(2)}
    </Badge>
  )
}
