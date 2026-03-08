import { useState, useRef, useCallback } from 'react'
import { createPortal } from 'react-dom'
import { cn } from '@/lib/utils'

interface TooltipProviderProps {
  children: React.ReactNode
}

function TooltipProvider({ children }: TooltipProviderProps) {
  return <>{children}</>
}

interface TooltipContextValue {
  open: boolean
  triggerRect: DOMRect | null
  side: 'top' | 'bottom' | 'left' | 'right'
  setOpen: (open: boolean) => void
  setTriggerRect: (rect: DOMRect | null) => void
  setSide: (side: 'top' | 'bottom' | 'left' | 'right') => void
}

// Simple module-level state to avoid context overhead for hover tooltips
let activeTooltip: {
  id: number
  setOpen: (open: boolean) => void
} | null = null
let tooltipIdCounter = 0

interface TooltipProps extends React.HTMLAttributes<HTMLDivElement> {}

function Tooltip({ className, children, ...props }: TooltipProps) {
  const [open, setOpen] = useState(false)
  const [triggerRect, setTriggerRect] = useState<DOMRect | null>(null)
  const idRef = useRef(++tooltipIdCounter)
  const sideRef = useRef<'top' | 'bottom' | 'left' | 'right'>('top')

  const handleOpen = useCallback((isOpen: boolean) => {
    setOpen(isOpen)
    if (isOpen) {
      activeTooltip = { id: idRef.current, setOpen }
    } else if (activeTooltip?.id === idRef.current) {
      activeTooltip = null
    }
  }, [])

  return (
    <div
      className={cn('group relative inline-flex', className)}
      data-tooltip-id={idRef.current}
      data-tooltip-open={open}
      data-tooltip-side={sideRef.current}
      {...props}
    >
      {/* Pass state down via data attributes + event handlers */}
      <TooltipInternalContext.Provider
        value={{ open, triggerRect, side: sideRef.current, setOpen: handleOpen, setTriggerRect, setSide: (s) => { sideRef.current = s } }}
      >
        {children}
      </TooltipInternalContext.Provider>
    </div>
  )
}

import { createContext, useContext } from 'react'

const TooltipInternalContext = createContext<TooltipContextValue>({
  open: false,
  triggerRect: null,
  side: 'top',
  setOpen: () => {},
  setTriggerRect: () => {},
  setSide: () => {},
})

interface TooltipTriggerProps extends React.HTMLAttributes<HTMLDivElement> {}

function TooltipTrigger({ className, children, ...props }: TooltipTriggerProps) {
  const ref = useRef<HTMLDivElement>(null)
  const ctx = useContext(TooltipInternalContext)

  const handleMouseEnter = useCallback(() => {
    if (ref.current) {
      ctx.setTriggerRect(ref.current.getBoundingClientRect())
    }
    ctx.setOpen(true)
  }, [ctx])

  const handleMouseLeave = useCallback(() => {
    ctx.setOpen(false)
  }, [ctx])

  return (
    <div
      ref={ref}
      className={cn('inline-flex', className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      {...props}
    >
      {children}
    </div>
  )
}

interface TooltipContentProps extends React.HTMLAttributes<HTMLDivElement> {
  side?: 'top' | 'bottom' | 'left' | 'right'
}

function TooltipContent({ className, side = 'top', children, ...props }: TooltipContentProps) {
  const ctx = useContext(TooltipInternalContext)
  const contentRef = useRef<HTMLDivElement>(null)

  // Register side preference
  ctx.setSide(side)

  if (!ctx.open || !ctx.triggerRect) return null

  const rect = ctx.triggerRect
  const margin = 8

  let style: React.CSSProperties = {}

  switch (side) {
    case 'bottom':
      style = {
        top: rect.bottom + margin,
        left: Math.max(8, Math.min(rect.left, window.innerWidth - 304)),
      }
      break
    case 'top':
      style = {
        bottom: window.innerHeight - rect.top + margin,
        left: Math.max(8, Math.min(rect.left, window.innerWidth - 304)),
      }
      break
    case 'left':
      style = {
        top: rect.top + rect.height / 2,
        right: window.innerWidth - rect.left + margin,
        transform: 'translateY(-50%)',
      }
      break
    case 'right':
      style = {
        top: rect.top + rect.height / 2,
        left: rect.right + margin,
        transform: 'translateY(-50%)',
      }
      break
  }

  return createPortal(
    <div
      ref={contentRef}
      className={cn(
        'fixed z-[9999] rounded-md border border-border bg-popover px-3 py-1.5 text-sm text-popover-foreground shadow-md',
        className
      )}
      style={style}
      {...props}
    >
      {children}
    </div>,
    document.body
  )
}

export { TooltipProvider, Tooltip, TooltipTrigger, TooltipContent }
