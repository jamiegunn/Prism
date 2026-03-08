import { Sidebar } from './Sidebar'
import { StatusBar } from './StatusBar'

interface AppShellProps {
  children: React.ReactNode
}

export function AppShell({ children }: AppShellProps) {
  return (
    <div className="flex h-screen overflow-hidden bg-zinc-950 text-zinc-50">
      <Sidebar />
      <div className="flex flex-1 flex-col ml-64 overflow-hidden">
        <main className="flex-1 overflow-y-auto overflow-x-hidden p-6 pb-14">
          {children}
        </main>
        <StatusBar />
      </div>
    </div>
  )
}
