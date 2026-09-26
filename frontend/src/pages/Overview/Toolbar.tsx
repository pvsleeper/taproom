import { Search, X } from 'lucide-react'
import { forwardRef } from 'react'

export type ClientFilter = 'all' | 'wireless' | 'wired' | 'offline'

const FILTERS: { value: ClientFilter; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'wireless', label: 'Wireless' },
  { value: 'wired', label: 'Wired' },
  { value: 'offline', label: 'Offline' },
]

interface ToolbarProps {
  query: string
  onQueryChange: (value: string) => void
  filter: ClientFilter
  onFilterChange: (value: ClientFilter) => void
}

export const Toolbar = forwardRef<HTMLInputElement, ToolbarProps>(function Toolbar(
  { query, onQueryChange, filter, onFilterChange },
  ref,
) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <div className="relative w-full max-w-xs">
        <Search size={16} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-text-muted" />
        <input
          ref={ref}
          type="text"
          value={query}
          onChange={(e) => onQueryChange(e.target.value)}
          placeholder="Search name, IP or MAC"
          className="w-full rounded-md border border-border bg-surface py-1.5 pr-8 pl-8 text-sm placeholder:text-text-muted focus:border-accent focus:ring-1 focus:ring-accent focus:outline-none"
        />
        {query && (
          <button
            aria-label="Clear search"
            onClick={() => onQueryChange('')}
            className="absolute top-1/2 right-2 -translate-y-1/2 rounded p-0.5 text-text-muted hover:text-text"
          >
            <X size={14} />
          </button>
        )}
      </div>

      <div className="flex rounded-md border border-border bg-surface p-0.5">
        {FILTERS.map((f) => (
          <button
            key={f.value}
            onClick={() => onFilterChange(f.value)}
            className={`
              rounded px-3 py-1 text-xs font-medium transition-colors
              ${filter === f.value ? 'bg-accent-muted text-accent' : 'text-text-muted hover:text-text'}
            `}
          >
            {f.label}
          </button>
        ))}
      </div>
    </div>
  )
})
