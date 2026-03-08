import { useCallback } from 'react'
import { Upload } from 'lucide-react'
import { useIngestDocument } from '../api'

interface DocumentUploadProps {
  collectionId: string
}

export function DocumentUpload({ collectionId }: DocumentUploadProps) {
  const ingestDoc = useIngestDocument(collectionId)

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault()
      const files = Array.from(e.dataTransfer.files)
      files.forEach((file) => ingestDoc.mutate(file))
    },
    [ingestDoc]
  )

  const handleFileSelect = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const files = Array.from(e.target.files ?? [])
      files.forEach((file) => ingestDoc.mutate(file))
      e.target.value = ''
    },
    [ingestDoc]
  )

  return (
    <div
      className="rounded-lg border-2 border-dashed border-zinc-700 p-6 text-center hover:border-violet-500 transition-colors cursor-pointer"
      onDrop={handleDrop}
      onDragOver={(e) => e.preventDefault()}
    >
      <input
        type="file"
        id="doc-upload"
        className="hidden"
        multiple
        accept=".txt,.md,.html,.htm"
        onChange={handleFileSelect}
      />
      <label htmlFor="doc-upload" className="cursor-pointer">
        <Upload className="mx-auto h-8 w-8 text-zinc-500 mb-2" />
        <p className="text-sm text-zinc-400">
          {ingestDoc.isPending
            ? 'Uploading & processing...'
            : 'Drop files here or click to upload'}
        </p>
        <p className="text-xs text-zinc-500 mt-1">Supports: TXT, MD, HTML</p>
      </label>
    </div>
  )
}
