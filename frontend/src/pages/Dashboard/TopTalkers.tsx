import { Link } from 'react-router-dom'
import type { DeviceRate } from '../../api/types'

interface TopTalkersProps {
  devices: DeviceRate[] | undefined
  isPending: boolean
  colorFor: (mac: string) => string | undefined
}

function formatMbps(bps: number): string {
  const mbps = bps / 1_000_000
  return mbps < 0.1 ? '<0.1 Mbps' : `${mbps < 10 ? mbps.toFixed(1) : Math.round(mbps)} Mbps`
}

export function TopTalkers({ devices, isPending, colorFor }: TopTalkersProps) {
  const maxRate = Math.max(1, ...(devices ?? []).map((d) => d.downBps + d.upBps))

  return (
    <div className="flex h-full flex-col rounded-lg border border-border p-4">
      <h2 className="text-sm font-semibold">Top talkers</h2>

      {isPending && !devices && <div className="mt-3 text-sm text-text-muted">Loading…</div>}

      {devices && devices.length === 0 && <div className="mt-3 text-sm text-text-muted">All quiet.</div>}

      {devices && devices.length > 0 && (
        <div className="mt-3 flex flex-col gap-3">
          {devices.map((d) => {
            const rate = d.downBps + d.upBps
            const color = colorFor(d.mac)
            return (
              <div key={d.mac} className="flex flex-col gap-1">
                <div className="flex items-center justify-between text-xs">
                  <Link to={`/clients/${encodeURIComponent(d.mac)}`} className="flex items-center gap-1.5 truncate hover:underline">
                    <span
                      className="h-2 w-2 shrink-0 rounded-full"
                      style={{ background: color ?? 'var(--color-text-muted)' }}
                    />
                    <span className="truncate">{d.name}</span>
                  </Link>
                  <span className="shrink-0 tabular-nums text-text-muted">
                    ↓ {formatMbps(d.downBps)} ↑ {formatMbps(d.upBps)}
                  </span>
                </div>
                <div className="h-1.5 rounded-full bg-surface">
                  <div
                    className="h-full rounded-full"
                    style={{ width: `${Math.max(2, (rate / maxRate) * 100)}%`, background: color ?? 'var(--color-accent)' }}
                  />
                </div>
              </div>
            )
          })}
        </div>
      )}
    </div>
  )
}
