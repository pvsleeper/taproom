import {
  createColumnHelper,
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  useReactTable,
  type SortingFn,
  type SortingState,
} from '@tanstack/react-table'
import { ArrowDown, ArrowUp, ArrowUpDown, Wifi, Cable, CircleHelp } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import type { NetworkClient } from '../../api/types'
import { formatBytes, formatUptime } from '../../lib/format'
import { HighlightText } from './HighlightText'
import { SignalBars } from './SignalBars'

const columnHelper = createColumnHelper<NetworkClient>()

function ipToSortableNumber(ip: string | null): number {
  if (!ip) return -1
  const parts = ip.split('.').map(Number)
  if (parts.length !== 4 || parts.some(Number.isNaN)) return -1
  return parts.reduce((acc, part) => acc * 256 + part, 0)
}

const sortByIp: SortingFn<NetworkClient> = (rowA, rowB) =>
  ipToSortableNumber(rowA.original.ip) - ipToSortableNumber(rowB.original.ip)

interface ClientTableProps {
  clients: NetworkClient[]
  query: string
}

export function ClientTable({ clients, query }: ClientTableProps) {
  const navigate = useNavigate()
  const [sorting, setSorting] = useState<SortingState>([
    { id: 'online', desc: true },
    { id: 'name', desc: false },
  ])

  const columns = useMemo(
    () => [
      columnHelper.accessor('online', {
        header: 'Status',
        cell: (info) => (
          <span
            className={`inline-block h-2.5 w-2.5 rounded-full ${info.getValue() ? 'bg-online' : 'bg-offline'}`}
            title={info.getValue() ? 'Online' : 'Offline'}
          />
        ),
      }),
      columnHelper.accessor('name', {
        header: 'Name',
        cell: (info) => {
          const client = info.row.original
          return (
            <div>
              <div className="text-text">
                <HighlightText text={client.name} query={query} />
              </div>
              {client.hostname && client.hostname !== client.name && (
                <div className="text-xs text-text-muted">
                  <HighlightText text={client.hostname} query={query} />
                </div>
              )}
            </div>
          )
        },
      }),
      columnHelper.accessor('ip', {
        header: 'IP',
        sortingFn: sortByIp,
        cell: (info) => (
          <span className="font-mono text-xs">
            {info.getValue() ? <HighlightText text={info.getValue()!} query={query} /> : '—'}
          </span>
        ),
      }),
      columnHelper.accessor('mac', {
        header: 'MAC',
        cell: (info) => <span className="font-mono text-xs">{info.getValue()}</span>,
      }),
      columnHelper.accessor((row) => row.connection, {
        id: 'connection',
        header: 'Connection',
        cell: (info) => {
          const client = info.row.original
          if (client.connection === 'Wireless') {
            return (
              <div className="flex items-center gap-1.5 text-xs">
                <Wifi size={14} className="text-text-muted" />
                <span>
                  {client.ssid ?? '—'}
                  {client.band && <span className="text-text-muted"> · {client.band}</span>}
                </span>
              </div>
            )
          }
          if (client.connection === 'Wired') {
            return (
              <div className="flex items-center gap-1.5 text-xs text-text-muted">
                <Cable size={14} />
                <span>Ethernet</span>
              </div>
            )
          }
          return (
            <div className="flex items-center gap-1.5 text-xs text-text-muted" title="Offline and never seen by Omada — could be wired or wireless">
              <CircleHelp size={14} />
              <span>Unknown</span>
            </div>
          )
        },
      }),
      columnHelper.accessor('apName', {
        header: 'AP',
        cell: (info) => <span className="text-xs text-text-muted">{info.getValue() ?? ''}</span>,
      }),
      columnHelper.accessor('rssi', {
        header: 'Signal',
        cell: (info) => <SignalBars rssi={info.getValue()} />,
      }),
      columnHelper.display({
        id: 'traffic',
        header: 'Traffic',
        cell: (info) => {
          const client = info.row.original
          return (
            <span className="font-mono text-xs text-text-muted">
              ↓{formatBytes(client.rxBytes)} / ↑{formatBytes(client.txBytes)}
            </span>
          )
        },
      }),
      columnHelper.accessor('connectedSince', {
        header: 'Uptime',
        cell: (info) => <span className="text-xs text-text-muted">{formatUptime(info.getValue())}</span>,
      }),
    ],
    [query],
  )

  const table = useReactTable({
    data: clients,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    enableMultiSort: false,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
    getRowId: (row) => row.mac,
  })

  return (
    <div className="overflow-x-auto rounded-lg border border-border">
      <table className="w-full min-w-[900px] border-collapse text-sm">
        <thead className="sticky top-0 bg-surface">
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
              tabIndex={0}
              onClick={() => navigate(`/clients/${encodeURIComponent(row.original.mac)}`)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') navigate(`/clients/${encodeURIComponent(row.original.mac)}`)
              }}
              className="h-10 cursor-pointer border-b border-border last:border-b-0 hover:bg-surface-hover focus:bg-surface-hover focus:outline-none"
            >
              {row.getVisibleCells().map((cell) => (
                <td key={cell.id} className="px-3 py-1.5">
                  {flexRender(cell.column.columnDef.cell, cell.getContext())}
                </td>
              ))}
            </tr>
          ))}
          {table.getRowModel().rows.length === 0 && (
            <tr>
              <td colSpan={columns.length} className="px-3 py-8 text-center text-text-muted">
                No clients match your search.
              </td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  )
}
