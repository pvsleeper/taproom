import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingState,
} from '@tanstack/react-table'
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronDown, Search, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import type { EnrichedConnection, LocalConnection } from '../../api/types'
import { countryFlag } from '../../lib/countryFlag'
import { formatBytes } from '../../lib/format'
import { NameBadge } from './NameBadge'

const columnHelper = createColumnHelper<EnrichedConnection>()

function formatRate(bps: number | null): string {
  if (bps === null) return '–'
  const mbps = bps / 1_000_000
  if (mbps < 0.1) return '<0.1 Mbps'
  return `${mbps < 10 ? mbps.toFixed(1) : Math.round(mbps)} Mbps`
}

function formatAge(seconds: number): string {
  const m = Math.floor(seconds / 60)
  const s = seconds % 60
  if (m === 0) return `${s}s`
  const h = Math.floor(m / 60)
  if (h === 0) return `${m}m ${s}s`
  return `${h}h ${m % 60}m`
}

interface ConnectionsTableProps {
  connections: EnrichedConnection[]
  local: LocalConnection[]
  locationFilterKey: string | null
  onClearLocationFilter: () => void
  hoveredRemote: string | null
  onHoverRemote: (key: string | null) => void
  markerKeyFor: (c: EnrichedConnection) => string
}

export function ConnectionsTable({
  connections,
  local,
  locationFilterKey,
  onClearLocationFilter,
  onHoverRemote,
  markerKeyFor,
}: ConnectionsTableProps) {
  const [query, setQuery] = useState('')
  const [sorting, setSorting] = useState<SortingState>([{ id: 'rate', desc: true }])
  const [localOpen, setLocalOpen] = useState(false)

  const filtered = useMemo(() => {
    let rows = connections
    if (locationFilterKey) {
      rows = rows.filter((c) => markerKeyFor(c) === locationFilterKey)
    }
    const q = query.trim().toLowerCase()
    if (q) {
      rows = rows.filter(
        (c) =>
          c.name.toLowerCase().includes(q) ||
          c.remoteIp.includes(q) ||
          (c.org ?? '').toLowerCase().includes(q),
      )
    }
    return rows
  }, [connections, query, locationFilterKey, markerKeyFor])

  const columns = useMemo(
    () => [
      columnHelper.accessor('name', {
        header: 'Name',
        cell: (info) => {
          const c = info.row.original
          return (
            <div className="flex items-center gap-1.5">
              <NameBadge source={c.nameSource} />
              <span
                title={c.otherNames.length > 0 ? `Also: ${c.otherNames.join(', ')}` : undefined}
                className="truncate"
              >
                {c.name}
              </span>
            </div>
          )
        },
      }),
      columnHelper.display({
        id: 'remote',
        header: 'Remote',
        cell: (info) => (
          <span className="font-mono text-xs text-text-muted">
            {info.row.original.remoteIp}:{info.row.original.remotePort} · {info.row.original.protocol}
          </span>
        ),
      }),
      columnHelper.display({
        id: 'location',
        header: 'Location',
        cell: (info) => {
          const c = info.row.original
          return c.country ? (
            <span className="text-xs">
              {countryFlag(c.country)} {c.city ? `${c.city}, ` : ''}
              {c.country}
            </span>
          ) : (
            <span className="text-xs text-text-muted">—</span>
          )
        },
      }),
      columnHelper.display({
        id: 'network',
        header: 'Network',
        cell: (info) => (
          <span className="text-xs text-text-muted">{info.row.original.org ?? '—'}</span>
        ),
      }),
      columnHelper.accessor((row) => (row.downBps ?? 0) + (row.upBps ?? 0), {
        id: 'rate',
        header: 'Rate',
        cell: (info) => {
          const c = info.row.original
          return (
            <span className="font-mono text-xs">
              ↓{formatRate(c.downBps)} ↑{formatRate(c.upBps)}
            </span>
          )
        },
      }),
      columnHelper.accessor('bytes', {
        header: 'Total',
        cell: (info) => <span className="font-mono text-xs text-text-muted">{formatBytes(info.getValue())}</span>,
      }),
      columnHelper.accessor('ageSeconds', {
        header: 'Age',
        cell: (info) => <span className="text-xs text-text-muted">{formatAge(info.getValue())}</span>,
      }),
    ],
    [],
  )

  const table = useReactTable({
    data: filtered,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    enableMultiSort: false,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getRowId: (row) => `${row.remoteIp}:${row.remotePort}`,
  })

  return (
    <div className="rounded-lg border border-border">
      <div className="flex items-center justify-between gap-2 border-b border-border p-3">
        <div className="relative w-full max-w-xs">
          <Search size={14} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-text-muted" />
          <input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder="Search name, IP or org"
            className="w-full rounded-md border border-border bg-surface py-1.5 pr-8 pl-8 text-sm placeholder:text-text-muted focus:border-accent focus:ring-1 focus:ring-accent focus:outline-none"
          />
          {query && (
            <button
              aria-label="Clear search"
              onClick={() => setQuery('')}
              className="absolute top-1/2 right-2 -translate-y-1/2 rounded p-0.5 text-text-muted hover:text-text"
            >
              <X size={14} />
            </button>
          )}
        </div>

        {locationFilterKey && (
          <button
            onClick={onClearLocationFilter}
            className="flex items-center gap-1 rounded-full border border-accent/30 bg-accent-muted px-2.5 py-1 text-xs text-accent"
          >
            Filtered by location
            <X size={12} />
          </button>
        )}
      </div>

      <div className="overflow-x-auto">
        <table className="w-full min-w-[700px] border-collapse text-sm">
          <thead>
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id} className="border-b border-border">
                {headerGroup.headers.map((header) => {
                  const sortDirection = header.column.getIsSorted()
                  return (
                    <th
                      key={header.id}
                      onClick={header.column.getToggleSortingHandler()}
                      className="cursor-pointer px-3 py-2 text-left text-xs font-medium text-text-muted select-none hover:text-text"
                    >
                      <div className="flex items-center gap-1">
                        {flexRender(header.column.columnDef.header, header.getContext())}
                        {sortDirection === 'asc' && <ArrowUp size={12} />}
                        {sortDirection === 'desc' && <ArrowDown size={12} />}
                        {!sortDirection && <ArrowUpDown size={12} className="opacity-30" />}
                      </div>
                    </th>
                  )
                })}
              </tr>
            ))}
          </thead>
          <tbody>
            {table.getRowModel().rows.map((row) => (
              <tr
                key={row.id}
                onMouseEnter={() => onHoverRemote(row.id)}
                onMouseLeave={() => onHoverRemote(null)}
                className="h-9 border-b border-border last:border-b-0 hover:bg-surface-hover"
              >
                {row.getVisibleCells().map((cell) => (
                  <td key={cell.id} className="px-3 py-1">
                    {flexRender(cell.column.columnDef.cell, cell.getContext())}
                  </td>
                ))}
              </tr>
            ))}
            {table.getRowModel().rows.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="px-3 py-6 text-center text-text-muted">
                  No connections match.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {local.length > 0 && (
        <div className="border-t border-border">
          <button
            onClick={() => setLocalOpen((o) => !o)}
            className="flex w-full items-center gap-1.5 px-3 py-2 text-xs font-medium text-text-muted hover:text-text"
          >
            <ChevronDown size={14} className={`transition-transform ${localOpen ? '' : '-rotate-90'}`} />
            Local connections ({local.length})
          </button>
          {localOpen && (
            <table className="w-full border-collapse text-sm">
              <tbody>
                {local.map((c) => (
                  <tr key={`${c.remoteIp}:${c.remotePort}`} className="h-9 border-t border-border">
                    <td className="px-3 py-1">{c.name}</td>
                    <td className="px-3 py-1 font-mono text-xs text-text-muted">
                      {c.remoteIp}:{c.remotePort} · {c.protocol}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  )
}
