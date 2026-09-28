import { AlertTriangle, Compass } from 'lucide-react'
import { useCallback, useState } from 'react'
import { useParams } from 'react-router-dom'
import { useClient, useConnections, useDns } from '../../api/useClientDetail'
import { ConnectionMap } from './ConnectionMap'
import { ConnectionsTable } from './ConnectionsTable'
import { DetailHeader } from './DetailHeader'
import { DnsPanel } from './DnsPanel'

function markerKeyFor(lat: number, lon: number): string {
  return `${Math.round(lat * 10) / 10},${Math.round(lon * 10) / 10}`
}

export function ClientDetailPage() {
  const { mac = '' } = useParams<{ mac: string }>()
  const { data: client, isPending: clientPending, isError: clientError } = useClient(mac)
  const { data: connections, isPending: connectionsPending } = useConnections(mac)
  const [minutes, setMinutes] = useState(60)
  const { data: dns, isPending: dnsPending } = useDns(mac, minutes, 50)

  const [locationFilterKey, setLocationFilterKey] = useState<string | null>(null)
  const [hoveredRemote, setHoveredRemote] = useState<string | null>(null)
  const [highlightedDomain, setHighlightedDomain] = useState<string | null>(null)

  const keyFor = useCallback((c: { lat: number | null; lon: number | null }) => {
    if (c.lat === null || c.lon === null) return ''
    return markerKeyFor(c.lat, c.lon)
  }, [])

  if (clientError) {
    return (
      <div className="p-6">
        <div className="flex items-center gap-2 rounded-md border border-danger/30 bg-danger/10 px-3 py-2 text-sm text-danger">
          <AlertTriangle size={16} />
          <span>Client not found.</span>
        </div>
      </div>
    )
  }

  if (clientPending || !client) {
    return <div className="h-4 w-32 animate-pulse rounded bg-surface-hover m-6" />
  }

  return (
    <div className="flex h-full flex-col">
      <DetailHeader client={client} />

      <div className="flex-1 space-y-4 p-6">
        {connections?.hints.includes('possibleOwnDns') && (
          <div className="flex items-center gap-2 rounded-md border border-warn/30 bg-warn/10 px-3 py-2 text-sm text-warn">
            <Compass size={16} />
            <span>This device may be using its own encrypted DNS, so names may be missing.</span>
          </div>
        )}

        {connections && !connections.sources.states.ok && (
          <div className="flex items-center gap-2 rounded-md border border-warn/30 bg-warn/10 px-3 py-2 text-sm text-warn">
            <AlertTriangle size={16} />
            <span>Couldn't reach the firewall state table. Connections may be stale.</span>
          </div>
        )}

        <ConnectionMap
          connections={connections?.connections ?? []}
          markers={connections?.markers ?? []}
          home={connections?.home ?? null}
          hoveredRemote={hoveredRemote}
          highlightedDomain={highlightedDomain}
          locationFilterKey={locationFilterKey}
          onMarkerClick={setLocationFilterKey}
          isLoading={connectionsPending}
          geoIpOk={connections?.sources.geoip.ok ?? true}
        />

        <div className="flex flex-col gap-4 lg:flex-row">
          <div className="lg:w-[60%]">
            <ConnectionsTable
              connections={connections?.connections ?? []}
              local={connections?.local ?? []}
              locationFilterKey={locationFilterKey}
              onClearLocationFilter={() => setLocationFilterKey(null)}
              hoveredRemote={hoveredRemote}
              onHoverRemote={setHoveredRemote}
              markerKeyFor={(c) => keyFor(c)}
            />
          </div>
          <div className="lg:w-[40%]">
            <DnsPanel
              minutes={minutes}
              onMinutesChange={setMinutes}
              data={dns}
              isPending={dnsPending}
              onDomainClick={setHighlightedDomain}
              sourceOk={connections?.sources.dns.ok ?? true}
            />
          </div>
        </div>
      </div>
    </div>
  )
}
