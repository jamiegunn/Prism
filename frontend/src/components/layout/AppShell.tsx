import { useState, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { Sidebar } from './Sidebar'
import { StatusBar } from './StatusBar'
import { ShortcutsDialog } from '@/components/feedback/ShortcutsDialog'
import { useKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts'

interface AppShellProps {
  children: React.ReactNode
}

export function AppShell({ children }: AppShellProps) {
  const navigate = useNavigate()
  const [showShortcuts, setShowShortcuts] = useState(false)

  const shortcuts = useMemo(() => [
    { key: '?', action: () => setShowShortcuts(true), description: 'Show shortcuts' },
    { key: 'p', ctrl: true, shift: true, action: () => navigate('/playground'), description: 'Go to Playground' },
  ], [navigate])

  useKeyboardShortcuts(shortcuts)

  return (
    <div className="flex h-screen overflow-hidden bg-zinc-950 text-zinc-50">
      <Sidebar />
      <div className="flex flex-1 flex-col ml-64 overflow-hidden">
        <main className="flex-1 overflow-y-auto overflow-x-hidden p-6 pb-14">
          {children}
        </main>
        <StatusBar />
      </div>
      <ShortcutsDialog open={showShortcuts} onClose={() => setShowShortcuts(false)} />
    </div>
  )
}
