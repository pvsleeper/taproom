import { AlertTriangle } from 'lucide-react'
import { useEffect, useMemo, useRef, useState } from 'react'
import { useClientSnapshot } from '../../api/useClientSnapshot'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { normalizeMacQuery } from '../../lib/format'
import { ClientTable } from './ClientTable'
import { PageHeader } from './PageHeader'
import { SkeletonRows } from './SkeletonRows'
import { SummaryCards } from './SummaryCards'
import { Toolbar, type ClientFilter } from './Toolbar'

export function OverviewPage() {
  const { data, isPending, isError, error } = useClientSnapshot()
  const [query, setQuery] = useState('')
  const [filter, setFilter] = useState<ClientFilter>('all')
  const searchRef = useRef<HTMLInputElement>(null)

  const debouncedQuery = useDebouncedValue(query, 150)

  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === '/' && document.activeElement !== searchRef.current) {
        e.preventDefault()
        searchRef.current?.focus()
      }
    }
    window.addEventListener('keydown', onKeyDown)
    return () => window.removeEventListener('keydown', onKeyDown)
  }, [])

  const filteredClients = useMemo(() => {
    const clients = data?.clients ?? []
    const macQuery = normalizeMacQuery(debouncedQuery)
    const textQuery = debouncedQuery.trim().toLowerCase()

    return clients.filter((client) => {
      if (filter === 'wireless' && client.connection !== 'Wireless') return false
      if (filter === 'wired' && client.connection !== 'Wired') return false
      if (filter === 'offline' && client.online) return false

      if (!textQuery) return true

      const haystacks = [client.name, client.hostname ?? '', client.ip ?? ''].map((v) => v.toLowerCase())
      if (haystacks.some((h) => h.includes(textQuery))) return true

      return macQuery.length > 0 && normalizeMacQuery(client.mac).includes(macQuery)
    })
  }, [data, debouncedQuery, filter])

  return (
    <div className="flex h-full flex-col">
      <PageHeader snapshot={data} />

      <div className="flex-1 space-y-4 p-6">
        <SummaryCards clients={data?.clients ?? []} />

        <Toolbar ref={searchRef} query={query} onQueryChange={setQuery} filter={filter} onFilterChange={setFilter} />

        {isError && data && (
          <div className="flex items-center gap-2 rounded-md border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">
            <AlertTriangle size={16} />
            <span>Couldn't refresh data: {(error as Error).message}. Showing the last known state.</span>
          </div>
        )}

        {isError && !data && (
          <div className="flex items-center gap-2 rounded-md border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">
            <AlertTriangle size={16} />
            <span>Both sources are unreachable and no data has ever been loaded: {(error as Error).message}</span>
          </div>
        )}

        {isPending ? <SkeletonRows /> : <ClientTable clients={filteredClients} query={debouncedQuery} />}
      </div>
    </div>
  )
}
