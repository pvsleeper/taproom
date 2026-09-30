import { Link } from 'react-router-dom'
import type { DeviceRate } from '../../api/types'

interface MapLegendProps {
  devices: DeviceRate[]
  colorFor: (mac: string) => string | undefined
  soloMac: string | null
  onToggleSolo: (mac: string) => void
}

export function MapLegend({ devices, colorFor, soloMac, onToggleSolo }: MapLegendProps) {
  if (devices.length === 0) return null

  return (
    <div className="flex flex-wrap items-center gap-1.5 px-1">
      {devices.map((d) => {
        const color = colorFor(d.mac)
        if (!color) return null
        const isSolo = soloMac === d.mac
        const isDimmed = soloMac !== null && !isSolo

        return (
          <button
            key={d.mac}
            onClick={() => onToggleSolo(d.mac)}
            className={`flex items-center gap-1.5 rounded-full border px-2 py-1 text-xs transition-opacity ${
              isSolo ? 'border-accent bg-accent-muted text-accent' : 'border-border text-text-muted hover:text-text'
            } ${isDimmed ? 'opacity-40' : ''}`}
          >
            <span className="h-2 w-2 rounded-full" style={{ background: color }} />
            {d.name}
          </button>
        )
      })}
      {soloMac && (
        <Link
          to={`/clients/${encodeURIComponent(soloMac)}`}
          className="ml-1 text-xs text-accent hover:underline"
        >
          View device →
        </Link>
      )}
    </div>
  )
}
