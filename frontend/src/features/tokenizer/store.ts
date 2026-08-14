import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface TokenizerState {
  /** The server whose tokenizer is being used. */
  instanceId: string | null
  setInstanceId: (id: string) => void
}

/**
 * The tokenizer page's own server selection.
 *
 * Deliberately separate from the Token Explorer's. Tokenization is a question about a server's
 * vocabulary, and the server worth asking is often not the one you are exploring predictions
 * on — an Ollama has no tokenizer at all, so the two pages would otherwise fight over one
 * setting. Kept persisted for the same reason every other page keeps its choice: coming back
 * to a page you configured and finding it reset is its own small insult.
 */
export const useTokenizerStore = create<TokenizerState>()(
  persist(
    (set) => ({
      instanceId: null,
      setInstanceId: (id) => set({ instanceId: id }),
    }),
    { name: 'prism-tokenizer-state' }
  )
)
