import { useEffect } from 'react'

interface ShortcutConfig {
  key: string
  ctrl?: boolean
  shift?: boolean
  alt?: boolean
  action: () => void
  description: string
}

/**
 * Registers keyboard shortcuts that trigger actions.
 * Shortcuts are active when no input/textarea is focused.
 */
export function useKeyboardShortcuts(shortcuts: ShortcutConfig[]) {
  useEffect(() => {
    function handler(e: KeyboardEvent) {
      const target = e.target as HTMLElement
      const isInput = target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable
      if (isInput) return

      for (const shortcut of shortcuts) {
        const ctrlMatch = shortcut.ctrl ? (e.ctrlKey || e.metaKey) : !(e.ctrlKey || e.metaKey)
        const shiftMatch = shortcut.shift ? e.shiftKey : !e.shiftKey
        const altMatch = shortcut.alt ? e.altKey : !e.altKey

        if (e.key.toLowerCase() === shortcut.key.toLowerCase() && ctrlMatch && shiftMatch && altMatch) {
          e.preventDefault()
          shortcut.action()
          return
        }
      }
    }

    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [shortcuts])
}

/**
 * Common shortcut definitions for reference.
 */
export const SHORTCUT_LABELS: Record<string, string> = {
  'ctrl+k': 'Search / Command',
  'ctrl+n': 'New Conversation',
  'ctrl+e': 'Export',
  '?': 'Show Shortcuts',
}
