import { useEffect } from 'react'
import { ChevronDown } from 'lucide-react'
import { useWorkspaces } from './api'
import { useWorkspaceStore } from './store'

export function WorkspaceSwitcher() {
  const { data: workspaces } = useWorkspaces()
  const { activeWorkspaceId, setActiveWorkspace } = useWorkspaceStore()

  // Auto-select default workspace on first load
  useEffect(() => {
    if (!activeWorkspaceId && workspaces && workspaces.length > 0) {
      const defaultWs = workspaces.find((w) => w.isDefault) ?? workspaces[0]
      setActiveWorkspace(defaultWs.id)
    }
  }, [workspaces, activeWorkspaceId, setActiveWorkspace])

  const activeWorkspace = workspaces?.find((w) => w.id === activeWorkspaceId)

  return (
    <div className="px-3 py-2 border-b border-zinc-800">
      <div className="relative">
        <select
          value={activeWorkspaceId ?? ''}
          onChange={(e) => setActiveWorkspace(e.target.value || null)}
          className="w-full appearance-none bg-zinc-900 border border-zinc-700 rounded-md px-3 py-1.5 pr-8 text-sm text-zinc-300 focus:outline-none focus:ring-1 focus:ring-violet-600"
        >
          {workspaces?.map((ws) => (
            <option key={ws.id} value={ws.id}>
              {ws.name}
            </option>
          ))}
        </select>
        <ChevronDown className="absolute right-2 top-1/2 -translate-y-1/2 h-3.5 w-3.5 text-zinc-500 pointer-events-none" />
      </div>
      {activeWorkspace?.description && (
        <p className="text-[10px] text-zinc-600 mt-1 truncate">{activeWorkspace.description}</p>
      )}
    </div>
  )
}
