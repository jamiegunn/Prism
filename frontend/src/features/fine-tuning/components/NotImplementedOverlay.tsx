import { useState } from 'react'
import { Construction } from 'lucide-react'
import { createPortal } from 'react-dom'
import { Button } from '@/components/ui/button'

/**
 * Says plainly that fine-tuning is not built.
 *
 * The page looks finished — two tabs, forms, a table — and it is not. Prism trains nothing:
 * there is no training code in the backend, and a registered LoRA adapter is a row that no
 * inference path ever reads, with an `IsActive` flag nothing ever writes. Someone arriving here
 * to fine-tune a model will spend a while looking for the button before concluding the tool is
 * broken, which is the worst of the three possible outcomes.
 *
 * It does not claim the whole page is dead, because that would be its own falsehood: dataset
 * export is real, finished, and the reason to come here at all. So the overlay separates what
 * is missing from what works and lets you carry on to the part that does.
 */
export function NotImplementedOverlay() {
  const [dismissed, setDismissed] = useState(false)

  if (dismissed) return null

  return createPortal(
    <div className="fixed inset-0 z-[120] flex items-center justify-center bg-zinc-950/80 p-6 backdrop-blur-sm">
      <div className="max-w-lg rounded-lg border border-amber-700/50 bg-zinc-900 p-6 shadow-2xl">
        <div className="mb-3 flex items-center gap-2">
          <Construction className="h-5 w-5 text-amber-400" />
          <h2 className="text-base font-semibold text-zinc-50">Fine-tuning is not implemented</h2>
        </div>

        <p className="text-sm leading-relaxed text-zinc-400">
          Prism does not train models. There is no training in the backend, and registering a
          LoRA adapter here records a row that nothing reads &mdash; it will not affect any
          inference Prism runs.
        </p>

        <p className="mt-3 text-sm leading-relaxed text-zinc-400">
          <span className="font-medium text-zinc-200">Dataset export does work.</span> It turns a
          dataset into Alpaca, ShareGPT, ChatML or OpenAI JSONL for a trainer you run elsewhere,
          and it is the reason this page is still here.
        </p>

        <p className="mt-3 text-xs leading-relaxed text-zinc-500">
          Whether training belongs in Prism is undecided. Until then, nothing on this page is
          quietly pretending.
        </p>

        <div className="mt-5 flex justify-end">
          <Button onClick={() => setDismissed(true)}>Continue to export</Button>
        </div>
      </div>
    </div>,
    document.body
  )
}
