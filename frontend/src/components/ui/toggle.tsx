import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

const toggleVariants = cva(
  'inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors hover:bg-zinc-800 hover:text-zinc-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50 data-[state=on]:bg-accent data-[state=on]:text-accent-foreground',
  {
    variants: {
      variant: {
        default: 'bg-transparent',
        outline: 'border border-border bg-transparent hover:bg-accent hover:text-accent-foreground',
      },
      size: {
        default: 'h-10 px-3',
        sm: 'h-9 px-2.5',
        lg: 'h-11 px-5',
      },
    },
    defaultVariants: {
      variant: 'default',
      size: 'default',
    },
  }
)

interface ToggleProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof toggleVariants> {
  pressed?: boolean
  onPressedChange?: (pressed: boolean) => void
}

function Toggle({ className, variant, size, pressed, onPressedChange, ...props }: ToggleProps) {
  const [internalPressed, setInternalPressed] = React.useState(false)
  const isPressed = pressed ?? internalPressed

  const handleClick = (e: React.MouseEvent<HTMLButtonElement>) => {
    const newValue = !isPressed
    setInternalPressed(newValue)
    onPressedChange?.(newValue)
    props.onClick?.(e)
  }

  return (
    <button
      className={cn(toggleVariants({ variant, size, className }))}
      data-state={isPressed ? 'on' : 'off'}
      aria-pressed={isPressed}
      onClick={handleClick}
      {...props}
    />
  )
}

export { Toggle, toggleVariants }
export type { ToggleProps }
