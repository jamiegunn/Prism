import { Info } from 'lucide-react'
import { Tooltip, TooltipTrigger, TooltipContent } from './tooltip'

interface ParamLabelProps {
  label: string
  tooltip: string
}

export function ParamLabel({ label, tooltip }: ParamLabelProps) {
  return (
    <Tooltip>
      <TooltipTrigger>
        <span className="inline-flex items-center gap-1 text-xs font-medium text-zinc-400">
          {label}
          <Info className="h-3 w-3 text-zinc-600" />
        </span>
      </TooltipTrigger>
      <TooltipContent
        side="bottom"
        className="w-72 whitespace-normal text-xs leading-relaxed"
      >
        {tooltip}
      </TooltipContent>
    </Tooltip>
  )
}
