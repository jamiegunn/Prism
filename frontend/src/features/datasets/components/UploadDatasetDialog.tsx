import { useState, useRef } from 'react'
import { Upload } from 'lucide-react'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
  DialogFooter,
  DialogClose,
} from '@/components/ui/dialog'
import { useUploadDataset } from '../api'

export function UploadDatasetDialog() {
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)
  const upload = useUploadDataset()

  function handleSubmit() {
    if (!file || !name.trim()) return
    upload.mutate(
      { file, name: name.trim(), description: description.trim() || undefined },
      {
        onSuccess: () => {
          setName('')
          setDescription('')
          setFile(null)
          if (fileInputRef.current) fileInputRef.current.value = ''
        },
      }
    )
  }

  return (
    <Dialog>
      <DialogTrigger className="inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-md text-sm font-medium transition-colors bg-primary text-primary-foreground hover:bg-primary/90 h-9 px-4 py-2">
        <Upload className="h-4 w-4" />
        Upload Dataset
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Upload Dataset</DialogTitle>
        </DialogHeader>
        <div className="space-y-4 py-4">
          <div>
            <label className="text-sm font-medium">Name *</label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="My dataset"
            />
          </div>
          <div>
            <label className="text-sm font-medium">Description</label>
            <Input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Optional description"
            />
          </div>
          <div>
            <label className="text-sm font-medium">File (CSV, JSON, JSONL) *</label>
            <Input
              ref={fileInputRef}
              type="file"
              accept=".csv,.json,.jsonl"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="mt-1"
            />
          </div>
          {file && (
            <p className="text-xs text-muted-foreground">
              {file.name} ({(file.size / 1024).toFixed(1)} KB)
            </p>
          )}
        </div>
        <DialogFooter>
          <DialogClose className="inline-flex items-center justify-center rounded-md text-sm font-medium border border-input bg-background hover:bg-accent hover:text-accent-foreground h-9 px-4 py-2">
            Cancel
          </DialogClose>
          <Button
            onClick={handleSubmit}
            disabled={!file || !name.trim() || upload.isPending}
          >
            {upload.isPending ? 'Uploading...' : 'Upload'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
