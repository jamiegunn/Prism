import { Clock, Compass, Lock, Play, RotateCcw } from 'lucide-react'
import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Separator } from '@/components/ui/separator'
import { cn } from '@/lib/utils'
import type { TourOffer } from '../selection'

interface GuidePanelProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  offers: TourOffer[]
  completedTourIds: string[]
  autoStartEnabled: boolean
  onStart: (tourId: string) => void
  onAutoStartChange: (enabled: boolean) => void
  onResetProgress: () => void
}

/**
 * The guide: the welcome tour, plus the task walkthroughs, plus the switch that decides
 * whether any of it appears unprompted.
 *
 * Walkthroughs that cannot run yet are listed with the reason rather than hidden, so the
 * panel describes what the tool does even before it is set up. Hiding them would make the
 * guide emptiest exactly when someone needs it most.
 */
export function GuidePanel({
  open,
  onOpenChange,
  offers,
  completedTourIds,
  autoStartEnabled,
  onStart,
  onAutoStartChange,
  onResetProgress,
}: GuidePanelProps) {
  const welcome = offers.filter((offer) => offer.tour.kind === 'welcome')
  const situations = offers.filter((offer) => offer.tour.kind === 'situation')

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-[420px] overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="flex items-center gap-2">
            <Compass className="h-4 w-4 text-violet-400" />
            Guide
          </SheetTitle>
          <SheetDescription>
            Short walkthroughs that each end with something done, rather than a description of
            the buttons.
          </SheetDescription>
        </SheetHeader>

        <div className="mt-5 space-y-2">
          {welcome.map((offer) => (
            <TourRow
              key={offer.tour.id}
              offer={offer}
              completed={completedTourIds.includes(offer.tour.id)}
              onStart={onStart}
            />
          ))}
        </div>

        <Separator className="my-5" />

        <p className="mb-2 text-[11px] font-semibold uppercase tracking-wider text-zinc-500">
          Try something
        </p>

        <div className="space-y-2">
          {situations.map((offer) => (
            <TourRow
              key={offer.tour.id}
              offer={offer}
              completed={completedTourIds.includes(offer.tour.id)}
              onStart={onStart}
            />
          ))}
        </div>

        <Separator className="my-5" />

        <label className="flex cursor-pointer items-start gap-2.5 text-[13px] text-zinc-400">
          <input
            type="checkbox"
            checked={autoStartEnabled}
            onChange={(event) => onAutoStartChange(event.target.checked)}
            className="mt-0.5 h-3.5 w-3.5 accent-violet-500"
          />
          <span>
            Show the tour when Prism opens
            <span className="mt-0.5 block text-[12px] text-zinc-500">
              Only until you have seen it once. It never reappears on its own after that.
            </span>
          </span>
        </label>

        <Button
          variant="ghost"
          size="sm"
          onClick={onResetProgress}
          className="mt-3 h-7 gap-1.5 px-0 text-xs text-zinc-500 hover:text-zinc-200"
        >
          <RotateCcw className="h-3 w-3" />
          Forget what I have seen
        </Button>
      </SheetContent>
    </Sheet>
  )
}

interface TourRowProps {
  offer: TourOffer
  completed: boolean
  onStart: (tourId: string) => void
}

function TourRow({ offer, completed, onStart }: TourRowProps) {
  const { tour, available, blockedReason } = offer

  return (
    <div
      className={cn(
        'rounded-lg border p-3 transition-colors',
        available
          ? 'border-zinc-800 bg-zinc-900/50 hover:border-zinc-700'
          : 'border-zinc-800/60 bg-zinc-900/20'
      )}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0">
          <p className={cn('text-sm font-medium', available ? 'text-zinc-100' : 'text-zinc-500')}>
            {tour.title}
          </p>
          <p className="mt-0.5 text-[12px] leading-relaxed text-zinc-500">{tour.outcome}</p>
        </div>

        {completed && (
          <Badge variant="secondary" className="shrink-0 text-[10px]">
            Seen
          </Badge>
        )}
      </div>

      <div className="mt-2.5 flex items-center justify-between gap-3">
        <span className="flex shrink-0 items-center gap-1 whitespace-nowrap font-mono text-[11px] tabular-nums text-zinc-600">
          <Clock className="h-3 w-3" />
          {tour.minutes} min
        </span>

        {available ? (
          <Button size="sm" onClick={() => onStart(tour.id)} className="h-7 gap-1 text-xs">
            <Play className="h-3 w-3" />
            {completed ? 'Again' : 'Start'}
          </Button>
        ) : (
          <span className="flex items-center gap-1.5 text-[11px] text-amber-500/80">
            <Lock className="h-3 w-3 shrink-0" />
            {blockedReason}
          </span>
        )}
      </div>
    </div>
  )
}
