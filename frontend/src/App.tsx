import { BrowserRouter } from 'react-router-dom'
import { Toaster } from 'sonner'
import { QueryProvider } from '@/app/providers/QueryProvider'
import { ThemeProvider } from '@/app/providers/ThemeProvider'
import { AppShell } from '@/components/layout/AppShell'
import { ErrorBoundary } from '@/components/feedback/ErrorBoundary'
import { AppRoutes } from '@/app/routes'

export function App() {
  return (
    <QueryProvider>
      <ThemeProvider>
        <BrowserRouter>
          <ErrorBoundary>
            <AppShell>
              <AppRoutes />
            </AppShell>
          </ErrorBoundary>
        </BrowserRouter>
        <Toaster theme="dark" position="bottom-right" />
      </ThemeProvider>
    </QueryProvider>
  )
}
