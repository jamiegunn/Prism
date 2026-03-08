import * as React from 'react'
import { cn } from '@/lib/utils'

interface SliderProps extends Omit<React.InputHTMLAttributes<HTMLInputElement>, 'type'> {}

const Slider = React.forwardRef<HTMLInputElement, SliderProps>(
  function Slider({ className, ...props }, ref) {
    return (
      <input
        type="range"
        className={cn(
          'w-full h-2 rounded-lg appearance-none cursor-pointer bg-zinc-700 accent-violet-600',
          className
        )}
        ref={ref}
        {...props}
      />
    )
  }
)

Slider.displayName = 'Slider'

export { Slider }
export type { SliderProps }
