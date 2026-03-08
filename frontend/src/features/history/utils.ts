/** Format a date string as relative time (e.g., "2m ago", "3h ago", "5d ago"). */
export function formatRelativeTime(dateStr: string): string {
  const now = Date.now()
  const then = new Date(dateStr).getTime()
  const diffMs = now - then

  if (diffMs < 0) return 'just now'

  const seconds = Math.floor(diffMs / 1000)
  if (seconds < 60) return `${seconds}s ago`

  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`

  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`

  const days = Math.floor(hours / 24)
  if (days < 30) return `${days}d ago`

  const months = Math.floor(days / 30)
  if (months < 12) return `${months}mo ago`

  const years = Math.floor(months / 12)
  return `${years}y ago`
}

/** Format a date string as a full readable timestamp. */
export function formatTimestamp(dateStr: string): string {
  return new Date(dateStr).toLocaleString()
}

/** Return Tailwind classes for source module badges. */
export function getModuleBadgeColor(module: string): string {
  const colors: Record<string, string> = {
    playground: 'bg-violet-600/20 text-violet-300 border-violet-600/30',
    'token-explorer': 'bg-emerald-600/20 text-emerald-300 border-emerald-600/30',
    'prompt-lab': 'bg-amber-600/20 text-amber-300 border-amber-600/30',
    experiments: 'bg-blue-600/20 text-blue-300 border-blue-600/30',
    'batch-inference': 'bg-pink-600/20 text-pink-300 border-pink-600/30',
    rag: 'bg-cyan-600/20 text-cyan-300 border-cyan-600/30',
    agents: 'bg-orange-600/20 text-orange-300 border-orange-600/30',
  }
  return colors[module] ?? 'bg-zinc-600/20 text-zinc-300 border-zinc-600/30'
}

/** Source modules available for filtering. */
export const SOURCE_MODULES = [
  { value: '', label: 'All Sources' },
  { value: 'playground', label: 'Playground' },
  { value: 'token-explorer', label: 'Token Explorer' },
  { value: 'prompt-lab', label: 'Prompt Lab' },
  { value: 'experiments', label: 'Experiments' },
  { value: 'batch-inference', label: 'Batch Inference' },
  { value: 'rag', label: 'RAG' },
  { value: 'agents', label: 'Agents' },
] as const
