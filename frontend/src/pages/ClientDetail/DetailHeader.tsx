import { ArrowLeft, Cable, CircleHelp, Wifi } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import type { NetworkClient } from '../../api/types'
import { SignalBars } from '../Clients/SignalBars'

interface DetailHeaderProps {
  client: NetworkClient
}

export function DetailHeader({ client }: DetailHeaderProps) {
  const navigate = useNavigate()

  return (
    <div className="border-b border-border px-6 py-4">
      <button
        onClick={() => navigate(-1)}
        className="mb-3 inline-flex items-center gap-1.5 text-xs text-text-muted hover:text-text"
      >
        <ArrowLeft size={14} />
        Back to Clients
      </button>

      <div className="flex flex-wrap items-center gap-4">
        <span className={`h-2.5 w-2.5 shrink-0 rounded-full ${client.online ? 'bg-online' : 'bg-offline'}`} />

        <div>
          <h1 className="text-lg font-semibold">{client.name}</h1>
          {client.hostname && client.hostname !== client.name && (
            <div className="text-xs text-text-muted">{client.hostname}</div>
          )}
        </div>

        <div className="flex flex-wrap items-center gap-4 text-xs text-text-muted">
          {client.ip && <span className="font-mono">{client.ip}</span>}
          <span className="font-mono">{client.mac}</span>

          {client.connection === 'Wireless' && (
            <span className="flex items-center gap-1.5">
              <Wifi size={14} />
              {client.ssid}
              {client.band && ` · ${client.band}`}
            </span>
          )}
          {client.connection === 'Wired' && (
            <span className="flex items-center gap-1.5">
              <Cable size={14} />
              Ethernet
            </span>
          )}
          {client.connection === 'Unknown' && (
            <span className="flex items-center gap-1.5">
              <CircleHelp size={14} />
              Unknown
            </span>
          )}

          {client.apName && <span>{client.apName}</span>}
          {client.rssi !== null && <SignalBars rssi={client.rssi} />}
        </div>
      </div>
    </div>
  )
}
