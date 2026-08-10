import { Select } from '@/components/ui/select'
import { useDatasets } from '../api'

interface DatasetPickerProps {
  datasetId: string
  splitLabel: string
  /** Called with both, since changing dataset invalidates the split. */
  onChange: (datasetId: string, splitLabel: string) => void
}

/**
 * Picks a dataset and, optionally, one of its splits.
 *
 * Both Evaluation and Batch need exactly this, and both previously needed it badly enough that
 * neither had a create form at all — the dataset id is a GUID, and the only place it appears in
 * the UI is a detail page URL.
 *
 * Changing dataset clears the split rather than carrying it across. Split labels are per-dataset
 * strings, so keeping "test" when moving to a dataset that has no test split is how you end up
 * running an evaluation over nothing.
 */
export function DatasetPicker({ datasetId, splitLabel, onChange }: DatasetPickerProps) {
  const { data: datasets, isLoading } = useDatasets()

  const selected = datasets?.find((dataset) => dataset.id === datasetId)

  return (
    <div className="grid grid-cols-2 gap-3">
      <div className="space-y-1">
        <label className="text-sm font-medium text-zinc-300">Dataset</label>
        <Select
          value={datasetId}
          disabled={isLoading}
          onChange={(event) => onChange(event.target.value, '')}
        >
          <option value="">
            {isLoading
              ? 'Loading...'
              : datasets?.length
                ? 'Select a dataset...'
                : 'No datasets uploaded'}
          </option>
          {datasets?.map((dataset) => (
            <option key={dataset.id} value={dataset.id}>
              {dataset.name} ({dataset.recordCount} records)
            </option>
          ))}
        </Select>
      </div>

      <div className="space-y-1">
        <label className="text-sm font-medium text-zinc-300">Split</label>
        <Select
          value={splitLabel}
          disabled={!selected || selected.splits.length === 0}
          onChange={(event) => onChange(datasetId, event.target.value)}
        >
          <option value="">
            {selected && selected.splits.length === 0
              ? 'No splits — whole dataset'
              : 'Whole dataset'}
          </option>
          {selected?.splits.map((split) => (
            <option key={split.name} value={split.name}>
              {split.name} ({split.recordCount})
            </option>
          ))}
        </Select>
      </div>
    </div>
  )
}
