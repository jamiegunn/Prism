export function StatusBar() {
  return (
    <div className="fixed bottom-0 right-0 left-64 z-20 flex h-8 items-center justify-between border-t border-zinc-800 bg-zinc-900 px-4 text-xs text-zinc-400">
      <div className="flex items-center gap-2">
        <span className="inline-block h-2 w-2 rounded-full bg-emerald-500" />
        <span>Connected</span>
      </div>

      <div className="flex items-center gap-2">
        <span className="text-zinc-500">Model:</span>
        <span>No model loaded</span>
      </div>

      <div className="flex items-center gap-2">
        <span className="text-zinc-500">GPU:</span>
        <span>&mdash;</span>
      </div>
    </div>
  )
}
