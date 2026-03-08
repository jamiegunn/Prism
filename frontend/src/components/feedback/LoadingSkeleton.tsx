import { Skeleton } from '@/components/ui/skeleton'
import { cn } from '@/lib/utils'

interface LoadingSkeletonProps {
  rows?: number
  cols?: number
  className?: string
}

export function LoadingSkeleton({ rows = 3, cols = 1, className }: LoadingSkeletonProps) {
  return (
    <div className={cn('space-y-3', className)}>
      {Array.from({ length: rows }).map((_, rowIndex) => (
        <div key={rowIndex} className="flex gap-3">
          {Array.from({ length: cols }).map((_, colIndex) => (
            <Skeleton
              key={colIndex}
              className="h-6 flex-1"
            />
          ))}
        </div>
      ))}
    </div>
  )
}
