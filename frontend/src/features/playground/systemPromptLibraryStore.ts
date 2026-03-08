import { create } from 'zustand'
import { persist } from 'zustand/middleware'

export interface SavedPrompt {
  id: string
  name: string
  content: string
  createdAt: string
}

interface SystemPromptLibraryState {
  prompts: SavedPrompt[]
  add: (name: string, content: string) => void
  remove: (id: string) => void
  rename: (id: string, name: string) => void
  update: (id: string, content: string) => void
}

const defaultPrompts: SavedPrompt[] = [
  {
    id: 'default-assistant',
    name: 'General Assistant',
    content: 'You are a helpful, harmless, and honest AI assistant. Answer questions clearly and concisely.',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'default-coder',
    name: 'Code Helper',
    content: 'You are an expert software engineer. Write clean, well-documented code. Explain your reasoning step by step. Prefer simple solutions over complex ones.',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'default-json',
    name: 'JSON Extractor',
    content: 'Extract the requested information from the input and return it as valid JSON. Do not include any explanation, markdown formatting, or text outside the JSON object.',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'default-analyst',
    name: 'Research Analyst',
    content: 'You are a careful research analyst. Examine claims critically, cite evidence, acknowledge uncertainty, and distinguish between established facts and speculation.',
    createdAt: new Date().toISOString(),
  },
  {
    id: 'default-concise',
    name: 'Concise Responder',
    content: 'Be extremely concise. Answer in as few words as possible while remaining accurate and helpful. No filler, no preamble.',
    createdAt: new Date().toISOString(),
  },
]

function generateId(): string {
  return `sp-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
}

export const useSystemPromptLibrary = create<SystemPromptLibraryState>()(
  persist(
    (set) => ({
      prompts: defaultPrompts,

      add: (name, content) =>
        set((state) => ({
          prompts: [
            {
              id: generateId(),
              name,
              content,
              createdAt: new Date().toISOString(),
            },
            ...state.prompts,
          ],
        })),

      remove: (id) =>
        set((state) => ({
          prompts: state.prompts.filter((p) => p.id !== id),
        })),

      rename: (id, name) =>
        set((state) => ({
          prompts: state.prompts.map((p) =>
            p.id === id ? { ...p, name } : p
          ),
        })),

      update: (id, content) =>
        set((state) => ({
          prompts: state.prompts.map((p) =>
            p.id === id ? { ...p, content } : p
          ),
        })),
    }),
    {
      name: 'prism-system-prompt-library',
    }
  )
)
